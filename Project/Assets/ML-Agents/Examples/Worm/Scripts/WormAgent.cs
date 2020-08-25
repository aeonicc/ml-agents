using UnityEngine;
using Unity.MLAgents;
using Unity.Barracuda;
using Unity.MLAgents.Actuators;
using Unity.MLAgentsExamples;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(JointDriveController))] // Required to set joint forces
public class WormAgent : Agent
{
    public enum WormAgentBehaviorType
    {
        WormDynamic,
        WormStatic
    }

    public WormAgentBehaviorType typeOfWorm;

    const float m_maxWalkingSpeed = 10; //The max walking speed

    //Brains
    //A different brain will be used depending on the CrawlerAgentBehaviorType selected
    [Header("NN Models")] public NNModel wormDyBrain;
    public NNModel wormStBrain;

    [Header("Target Prefabs")] public Transform dynamicTargetPrefab;
    public Transform staticTargetPrefab;
    private Transform m_Target; //Target the agent will walk towards.

    [Header("Body Parts")] public Transform bodySegment0;
    public Transform bodySegment1;
    public Transform bodySegment2;
    public Transform bodySegment3;

    //This will be used as a stabilized model space reference point for observations
    //Because ragdolls can move erratically during training, using a stabilized reference transform improves learning
    OrientationCubeController m_OrientationCube;

    //The indicator graphic gameobject that points towards the target
    DirectionIndicator m_DirectionIndicator;
    JointDriveController m_JdController;

    Vector3 m_DirToTarget;
    float m_MovingTowardsDot;
    float m_FacingDot;
    Vector3 m_StartingPos;

    public override void Initialize()
    {
        var m_BehaviorParams = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        switch (typeOfWorm)
        {
            case WormAgentBehaviorType.WormDynamic:
            {
                m_BehaviorParams.BehaviorName = "WormDynamic";
                if (wormDyBrain)
                    m_BehaviorParams.Model = wormDyBrain;
                m_Target = Instantiate(dynamicTargetPrefab, transform.position, Quaternion.identity, transform);
                break;
            }
            case WormAgentBehaviorType.WormStatic:
            {
                m_BehaviorParams.BehaviorName = "WormStatic";
                if (wormStBrain)
                    m_BehaviorParams.Model = wormStBrain;
                var targetSpawnPos = transform.TransformPoint(new Vector3(0, 0, 1000));
                m_Target = Instantiate(staticTargetPrefab, targetSpawnPos, Quaternion.identity, transform);
                break;
            }
        }

        m_StartingPos = bodySegment0.position;
        m_OrientationCube = GetComponentInChildren<OrientationCubeController>();
        m_DirectionIndicator = GetComponentInChildren<DirectionIndicator>();
        m_JdController = GetComponent<JointDriveController>();

        UpdateOrientationObjects();

        //Setup each body part
        m_JdController.SetupBodyPart(bodySegment0);
        m_JdController.SetupBodyPart(bodySegment1);
        m_JdController.SetupBodyPart(bodySegment2);
        m_JdController.SetupBodyPart(bodySegment3);
    }

    /// <summary>
    /// Loop over body parts and reset them to initial conditions.
    /// </summary>
    public override void OnEpisodeBegin()
    {
        foreach (var bodyPart in m_JdController.bodyPartsList)
        {
            bodyPart.Reset(bodyPart);
        }

        //Random start rotation to help generalize
        bodySegment0.rotation = Quaternion.Euler(0, Random.Range(0.0f, 360.0f), 0);

        UpdateOrientationObjects();
    }

    /// <summary>
    /// Add relevant information on each body part to observations.
    /// </summary>
    public void CollectObservationBodyPart(BodyPart bp, VectorSensor sensor)
    {
        //GROUND CHECK
        sensor.AddObservation(bp.groundContact.touchingGround ? 1 : 0); // Whether the bp touching the ground

        //Get velocities in the context of our orientation cube's space
        //Note: You can get these velocities in world space as well but it may not train as well.
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(bp.rb.velocity));
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(bp.rb.angularVelocity));


        if (bp.rb.transform != bodySegment0)
        {
            //Get position relative to hips in the context of our orientation cube's space
            sensor.AddObservation(
                m_OrientationCube.transform.InverseTransformDirection(bp.rb.position - bodySegment0.position));
            sensor.AddObservation(bp.rb.transform.localRotation);
        }

        if (bp.joint)
            sensor.AddObservation(bp.currentStrength / m_JdController.maxJointForceLimit);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        RaycastHit hit;
        float maxDist = 10;
        if (Physics.Raycast(bodySegment0.position, Vector3.down, out hit, maxDist))
        {
            sensor.AddObservation(hit.distance / maxDist);
        }
        else
            sensor.AddObservation(1);

        var cubeForward = m_OrientationCube.transform.forward;
        var velGoal = cubeForward * m_maxWalkingSpeed;
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformDirection(velGoal));
        sensor.AddObservation(Quaternion.Angle(m_OrientationCube.transform.rotation,
                                  m_JdController.bodyPartsDict[bodySegment0].rb.rotation) / 180);
        sensor.AddObservation(Quaternion.FromToRotation(bodySegment0.forward, cubeForward));

        //Add pos of target relative to orientation cube
        sensor.AddObservation(m_OrientationCube.transform.InverseTransformPoint(m_Target.transform.position));

        foreach (var bodyPart in m_JdController.bodyPartsList)
        {
            CollectObservationBodyPart(bodyPart, sensor);
        }
    }

    /// <summary>
    /// Agent touched the target
    /// </summary>
    public void TouchedTarget()
    {
        AddReward(1f);
    }

    //Returns the average velocity of all of the body parts
    //Using the velocity of the hips only has shown to result in more erratic movement from the limbs, so...
    //...using the average helps prevent this erratic movement
    Vector3 GetAvgVelocity()
    {
        Vector3 velSum = Vector3.zero;
        Vector3 avgVel = Vector3.zero;

        //ALL RBS
        int numOfRB = 0;
        foreach (var item in m_JdController.bodyPartsList)
        {
            numOfRB++;
            velSum += item.rb.velocity;
        }

        avgVel = velSum / numOfRB;
        return avgVel;
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // The dictionary with all the body parts in it are in the jdController
        var bpDict = m_JdController.bodyPartsDict;

        var i = -1;
        var continuousActions = actionBuffers.ContinuousActions;
        // Pick a new target joint rotation
        bpDict[bodySegment0].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[bodySegment1].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);
        bpDict[bodySegment2].SetJointTargetRotation(continuousActions[++i], continuousActions[++i], 0);

        // Update joint strength
        bpDict[bodySegment0].SetJointStrength(continuousActions[++i]);
        bpDict[bodySegment1].SetJointStrength(continuousActions[++i]);
        bpDict[bodySegment2].SetJointStrength(continuousActions[++i]);

        //Reset if Worm fell through floor;
        if (bodySegment0.position.y < m_StartingPos.y - 2)
        {
            EndEpisode();
        }
    }

    void FixedUpdate()
    {
        UpdateOrientationObjects();

        var velReward =
            GetMatchingVelocityReward(m_OrientationCube.transform.forward * m_maxWalkingSpeed,
                GetAvgVelocity());
//                m_JdController.bodyPartsDict[bodySegment0].rb.velocity);

        //Angle delta of rotation between cube and body.
        var rotAngle = Quaternion.Angle(m_OrientationCube.transform.rotation,
            m_JdController.bodyPartsDict[bodySegment0].rb.rotation);

        //The reward for facing the target
        var facingRew = 0f;
        //If we are within 30 degrees of facing the target
        if (rotAngle < 30)
        {
            //Set normalized facingReward
            //Facing the target perfectly yields a reward of 1
            facingRew = 1 - (rotAngle / 180);
        }

        //Add the product of these two rewards
        AddReward(velReward * facingRew);
    }

    //normalized value of the difference in avg speed vs goal walking speed.
    public float GetMatchingVelocityReward(Vector3 velocityGoal, Vector3 actualVelocity)
    {
        //distance between our actual velocity and goal velocity
        var velDeltaMagnitude = Mathf.Clamp(Vector3.Distance(actualVelocity, velocityGoal), 0, m_maxWalkingSpeed);

        //return the value on a declining sigmoid shaped curve that decays from 1 to 0
        //This reward will approach 1 if it matches perfectly and approach zero as it deviates
        return Mathf.Pow(1 - Mathf.Pow(velDeltaMagnitude / m_maxWalkingSpeed, 2), 2);
    }

    //Update OrientationCube and DirectionIndicator
    void UpdateOrientationObjects()
    {
        m_OrientationCube.UpdateOrientation(bodySegment0, m_Target);
        if (m_DirectionIndicator)
        {
            m_DirectionIndicator.MatchOrientation(m_OrientationCube.transform);
        }
    }
}
