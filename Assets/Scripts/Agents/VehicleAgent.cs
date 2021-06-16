using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Utils;

public enum VehicleAgentState {
    SelfGuidedMoving,
    SystemGuidedMoving,
    Idling
}

public enum VehicleAgentPlatoonState
{
    CatchingUp,
    Waiting,
    Normal
}


// In this simulation the VehicleAgent communicates with CommunicationAgent using exposed system API, CommunicationAgent is a program installed in system computer

[ExecuteInEditMode]
public class VehicleAgent : MonoBehaviour
{
    [ReadOnly] public VehicleAgentState state = VehicleAgentState.Idling;
    [ReadOnly] public VehicleAgentPlatoonState platoonState = VehicleAgentPlatoonState.Normal;
    [ReadOnly] public string startNodeName; // Entered by user via UI screen
    [ReadOnly] public string destinationNodeName; // Entered by user via UI screen
    [ReadOnly] public NullableVector3 destinationPosition;
    [ReadOnly] public TS_DijkstraPath path = null;

    [ReadOnly] public NullableVector3 target;
    [ReadOnly] public string currentTargetNodeName;
    [ReadOnly] public GameObject currentTargetNode;
    [ReadOnly] public Vector3 currentTargetNodeNamePosition;

    [ReadOnly] public float currentSpeed = 0;
    public float baseSpeed = 100.0f;
    public float systemSpeed = 100.0f;

    public float arrivalDistance = 0.1f;

    NavSystem navSystem;

    // System guided mode
    private CommunicationAgent communicationAgent;

    void Start()
    {
        navSystem = GameObject.Find("Map").GetComponent<NavSystem>();
        FindPath();
        state = VehicleAgentState.SelfGuidedMoving;
    }

    void Update()
    {
        Move();
    }

    public void SetUp(float baseSpeed)
    {
        this.baseSpeed = baseSpeed;
    }

    private void Move()
    {
        if (state != VehicleAgentState.Idling)
        {
            if (target.HasValue)
            {
                if (Vector3.Distance(transform.position, currentTargetNode.transform.position) < arrivalDistance)
                {
                    int indexOfCurrentNode = path.nodes.IndexOf(currentTargetNode);
                    if (indexOfCurrentNode < path.nodes.Count - 1)
                    {
                        currentTargetNode = path.nodes[indexOfCurrentNode + 1];
                        currentTargetNodeName = currentTargetNode.name;
                        currentTargetNodeNamePosition = currentTargetNode.transform.position;
                    }
                    else // Reached destination
                    {
                        if (!communicationAgent) // This is only for simulation
                        {
                            EndRide();
                        }  
                    }
                }

                if (state == VehicleAgentState.SelfGuidedMoving)
                {
                    currentSpeed = baseSpeed;
                    target = currentTargetNode.transform.position;

                    float distanceToDestination = Vector3.Distance(transform.position, destinationPosition.Value);
                    if (distanceToDestination < arrivalDistance)
                    {
                        EndRide();
                    }
                }

                if (state == VehicleAgentState.SystemGuidedMoving)
                {
                    currentSpeed = systemSpeed;
                }

                transform.position = Vector3.MoveTowards(transform.position, target.Value, Time.deltaTime * currentSpeed / 500.0f);
            }



        }
    }

    private void FindPath()
    {
        path = navSystem.GetPath(startNodeName, destinationNodeName);
        currentTargetNode = path.nodes[0];
        target = currentTargetNode.transform.position;
        destinationPosition = navSystem.GetNodePosition(destinationNodeName).Value;
    }

    private void OnMouseDown()
    {
         

    }

    // --- API ---

    public void ToggleSystemGuidedMode(bool status)
    {
        if (status)
        {
            currentSpeed = systemSpeed;
            state = VehicleAgentState.SystemGuidedMoving;
        }
        else
        {
            // Recalculate path
            currentSpeed = baseSpeed;
            state = VehicleAgentState.SelfGuidedMoving;
        }
    }

    public void SetPlatoonState(VehicleAgentPlatoonState state)
    {
        platoonState = state;
    }

    public string GetDestinationNodeName()
    {
        return destinationNodeName;
    }

    public Vector3 GetCurrentTargetNodePosition()
    {
        return currentTargetNodeNamePosition;
    }

    public string GetCurrentTargetNodeName()
    {
        return currentTargetNodeName;
    }

    public Vector3? GetDestinationPosition()
    {
        return destinationPosition.Value;
    }

    public void DisconnectCommunicationAgent()
    {
        communicationAgent = null;
        state = VehicleAgentState.SelfGuidedMoving;     
    }

    public Vector3 GetTarget()
    {
        return target;
    }

    public VehicleAgent ConnectCommunicationAgent(CommunicationAgent communicationAgent)
    {
        this.communicationAgent = communicationAgent;
        return this;
    }

    public void SetSpeed(float speed)
    {
        systemSpeed = speed;
    }

    public float GetBaseSpeed()
    {
        return baseSpeed;
    }

    public List<string> GetPathNodesNames()
    {
        return path.nodes.Select(x => x.name).ToList();
    }

    public void SetTarget(Vector3 target)
    {
        this.target = target;
    }

    public Vector3 GetVehiclePosition()
    {
        return transform.position;
    }

    public void EndRide()
    {
        Destroy(this.gameObject);
    }

    // --- OTHER ---

    void DebugLog(string message)
    {
        if (Selection.Contains(gameObject))
        {
            Debug.Log("Vehicle " + gameObject.name + ": " + message);
        }
    }


}