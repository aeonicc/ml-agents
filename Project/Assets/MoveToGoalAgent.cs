using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Random = UnityEngine.Random;

public class MoveToGoalAgent : Agent
{
    [SerializeField] private Transform targetPosition;

    private float moveSpeed = 10;

    public override void OnEpisodeBegin()
    {
        //transform.localPosition = new Vector3(0, 0, 0);
        transform.localPosition = new Vector3(Random.Range(-10f, +10f), 1, Random.Range(-10f, +10f));
        targetPosition.localPosition = new Vector3(Random.Range(-10f, +10f), 2, Random.Range(-10f, +10f));
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(targetPosition.localPosition);

    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        //Debug.Log(actions.ContinuousActions[0]);

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        transform.localPosition += new Vector3(moveX, 0, moveZ) * Time.deltaTime * moveSpeed;
    }



    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }


    private void OnTriggerEnter(Collider other)
    {
        //
       // EndEpisode();
       if (other.TryGetComponent(out Goal goal))
       {
           SetReward(+1f);
           EndEpisode();
       }

       if (other.TryGetComponent(out MoveToGoalAgent moveToGoalAgent))
       {
           SetReward(+1f);
           EndEpisode();
       }
       if (other.TryGetComponent(out Wall wall))
       {
           SetReward(-1f);
           EndEpisode();
       }

    }


}
