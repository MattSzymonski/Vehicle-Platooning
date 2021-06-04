using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fuel : MonoBehaviour
{
    public float baseConsumption = 1.0f;
    public float airResistanceFactor = 0.4f; // Percentage of fuel consumption decrease when car is driving strictly in column

    [ReadOnly] public float currentConsumption;
    CarAgent carAgent;
    CommunicationAgent communicationAgent;

    [ReadOnly] public float totalFuelUsed;


    void Start()
    {
        carAgent = transform.GetComponent<CarAgent>();
        communicationAgent = transform.GetComponent<CommunicationAgent>();
    }

    void Update()
    {
        // https://journals.sagepub.com/doi/pdf/10.1177/0954407017729938 Figure 10
        // https://x-engineer.org/automotive-engineering/vehicle/electric-vehicles/ev-design-energy-consumption/

        float arcdFactor = communicationAgent.isStrictlyInColumn ? 1 - airResistanceFactor : 1; // If car is in strict column then resistance is smaller
        arcdFactor = communicationAgent.isColumnLeader ? 1 : arcdFactor; // If car is column leader then resistance is 100%
        float speedFactor = 1f; // Relation factor between speed and fuel consumption (Higher speed means higher consumption, question what is the relation)

        currentConsumption = carAgent.currentSpeed * speedFactor * arcdFactor;

        totalFuelUsed += currentConsumption * (Time.deltaTime * 10); // Fuel used corrected by time between frames (so it is not dependent on frame rate of the sumulation
    }
}
