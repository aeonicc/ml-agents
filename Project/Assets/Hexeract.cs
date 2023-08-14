using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Random = UnityEngine.Random;

public class Hexeract : Agent
{
    [SerializeField] private Transform targetPosition;

    private float moveSpeed = 10;

    public int IChingCoinToss()
    {
        int totalValue = 0;
        for (int i = 0; i < 3; i++)
        {
            totalValue += Random.Range(0, 2) == 0 ? 2 : 3;  // 0 represents tails (2 points) and 1 represents heads (3 points)
        }

        if (totalValue == 6 || totalValue == 9)  // these values represent changing lines
        {
            return totalValue;
        }
        else  // other values do not represent changing lines
        {
            return 0;
        }
    }

    public int GetChangingLine()
    {
        int changingLine = 0;
        for (int i = 0; i < 6; i++)  // for each of the six lines of the hexagram
        {
            int result = IChingCoinToss();
            if (result != 0)
            {
                changingLine = i;  // this is the changing line (0-based index, i.e., line 0 to 5)
                break;
            }
        }
        return changingLine;  // 0 means no changing lines
    }
    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(Random.Range(-10f, +10f), 1, Random.Range(-10f, +10f));
        targetPosition.localPosition = new Vector3(Random.Range(-10f, +10f), 2, Random.Range(-10f, +10f));


    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(targetPosition.localPosition);

    }
/*
    public override void OnActionReceived(ActionBuffers actions)
    {
        //Debug.Log(actions.ContinuousActions[0]);

        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        transform.localPosition += new Vector3(moveX, 0, moveZ) * Time.deltaTime * moveSpeed;
    }
    */
    public override void OnActionReceived(ActionBuffers actions)
    {
        int changingLine = TossCoins();
        Vector3 moveDirection = Vector3.zero;

        switch(changingLine)
        {
            case 0: moveDirection = new Vector3(1, 0, 0); break;  // Right
            case 1: moveDirection = new Vector3(-1, 0, 0); break; // Left
            case 2: moveDirection = new Vector3(0.5f, 0, Mathf.Sqrt(3) / 2); break; // Top right
            case 3: moveDirection = new Vector3(-0.5f, 0, Mathf.Sqrt(3) / 2); break; // Top left
            case 4: moveDirection = new Vector3(0.5f, 0, -Mathf.Sqrt(3) / 2); break; // Bottom right
            case 5: moveDirection = new Vector3(-0.5f, 0, -Mathf.Sqrt(3) / 2); break; // Bottom left
        }

        transform.localPosition += moveDirection * Time.deltaTime * moveSpeed;
    }

    private int TossCoins()
    {
        List<int> hexagram = new List<int>();
        int changingLine = -1;

        for (int i = 0; i < 6; i++)
        {
            int line = TossThreeCoins();

            if (line == 6 || line == 9) // 6 and 9 are the changing lines
            {
                changingLine = i;
            }

            hexagram.Add(line);
        }

        // If no changing lines were found, return a default value or you could randomize it
        if (changingLine == -1)
        {
            return Random.Range(0, 6);
        }

        return changingLine;
    }

    private int TossThreeCoins()
    {
        int countOfTails = 0;
        for (int i = 0; i < 3; i++)
        {
            if (Random.value <= 0.5f) countOfTails++;
        }

        if (countOfTails == 0) return 9; // 3 heads
        if (countOfTails == 1) return 8;
        if (countOfTails == 2) return 7;
        return 6; // 3 tails
    }
/*
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }
    */

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        if (Input.GetKey(KeyCode.D)) discreteActions[0] = 0;        // Right
        else if (Input.GetKey(KeyCode.A)) discreteActions[0] = 1;   // Left
        else if (Input.GetKey(KeyCode.E)) discreteActions[0] = 2;   // Top right
        else if (Input.GetKey(KeyCode.Q)) discreteActions[0] = 3;   // Top left
        else if (Input.GetKey(KeyCode.C)) discreteActions[0] = 4;   // Bottom right
        else if (Input.GetKey(KeyCode.Z)) discreteActions[0] = 5;   // Bottom left
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
