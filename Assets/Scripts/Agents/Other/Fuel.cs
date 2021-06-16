using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fuel : MonoBehaviour
{
	//public float baseConsumption = 1.0f;
	//public float airResistanceFactor = 0.4f; // Percentage of fuel consumption decrease when vehicle is driving strictly in platoon

	[ReadOnly] public float currentConsumption;
	VehicleAgent vehicleAgent;
	CommunicationAgent communicationAgent;

	[ReadOnly] public float totalFuelUsed;
	public float distanceBetweenVehiclesInPlatoon = 1;

	public float xxx;

	void Start()
	{
		xxx = (-Mathf.Log(10 + 1) * 25 + 68) / 100;

		vehicleAgent = transform.GetComponent<VehicleAgent>();
		communicationAgent = transform.GetComponent<CommunicationAgent>();
	}

	void Update()
	{
		// https://journals.sagepub.com/doi/pdf/10.1177/0954407017729938 Figure 10
		// https://x-engineer.org/automotive-engineering/vehicle/electric-vehicles/ev-design-energy-consumption/

		float airResistanceDropInFunctionOfDistanceBetweenVehiclesInPlatoon = 1;

		// Air resistance, speed, fuel consumption
		// https://www.semanticscholar.org/paper/A-General-Simulation-Framework-for-Modeling-and-of-Deng/646204958f06527a480c9d3c3018b161e361fab7 Figure 1				
		if (communicationAgent.isPlatoonLeader) // Leader
		{
			airResistanceDropInFunctionOfDistanceBetweenVehiclesInPlatoon = 1;
		}
		else // Drafting (behind other vehicle)
		{
			if (communicationAgent.isStrictlyInPlatoon)
			{
				airResistanceDropInFunctionOfDistanceBetweenVehiclesInPlatoon = (-Mathf.Log(communicationAgent.betweenVehicleDistances + 1) * 25 + 68) / 100;
			}
			else
			{
				airResistanceDropInFunctionOfDistanceBetweenVehiclesInPlatoon = 1;
			}
		}

		// Engine, speed, fuel consumption
		// https://www.researchgate.net/publication/311703927_Urban_Transportation_Solutions_for_the_CO2_Emissions_Reduction_Contributions Figure 4
		float fuelConsumptionInFunctionOfSpeed = 0.0019f * Mathf.Pow(vehicleAgent.currentSpeed, 2) - 0.2506f * vehicleAgent.currentSpeed + 13.74f; // For 100% air resistance

		currentConsumption = fuelConsumptionInFunctionOfSpeed * airResistanceDropInFunctionOfDistanceBetweenVehiclesInPlatoon;

		totalFuelUsed += currentConsumption * (Time.deltaTime * 10); // Fuel used corrected by time between frames (so it is not dependent on frame rate of the sumulation
	}
}