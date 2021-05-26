using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Utils;

public enum CarAgentState {
    SelfGuidedMoving,
    SystemGuidedMoving,
    Idling
}

public enum CarAgentColumnState
{
    CatchingUp,
    Waiting,
    Normal
}


// In this simulation the CarAgent communicates with CommunicationAgent using exposed system API, CommunicationAgent is a program installed in system computer

[ExecuteInEditMode]
public class CarAgent : MonoBehaviour
{
    [ReadOnly] public CarAgentState state = CarAgentState.Idling;
    [ReadOnly] public CarAgentColumnState columnState = CarAgentColumnState.Normal;
    [ReadOnly] public string startNodeName; // Entered by user via UI screen
    [ReadOnly] public string destinationNodeName; // Entered by user via UI screen
    [ReadOnly] public NullableVector3 destinationPosition;
    [ReadOnly] public TS_DijkstraPath path = null;

    [ReadOnly] public NullableVector3 target;
    [ReadOnly] public string currentTargetNodeName;
    [ReadOnly] public GameObject currentTargetNode;
    [ReadOnly] public Vector3 currentTargetNodeNamePosition;

    [ReadOnly] public float currentSpeed = 1;
    [ReadOnly] public float baseSpeed = 0.2f;
    [ReadOnly] public float systemSpeed = 0.2f;

    [ReadOnly] public float arrivalDistance = 0.1f;

    NavSystem navSystem;


    // System guided mode
    [ReadOnly] public CommunicationAgent communicationAgent;

    void Start()
    {
        navSystem = GameObject.Find("Map").GetComponent<NavSystem>();
        FindPath();
        state = CarAgentState.SelfGuidedMoving;
    }

    void Update()
    {
        Move();
    }

    public void SetUp(float baseSpeed, float arrivalDistance)
    {
        this.baseSpeed = baseSpeed;
        this.arrivalDistance = arrivalDistance;
    }

    private void Move()
    {
        if (state != CarAgentState.Idling)
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

                if (state == CarAgentState.SelfGuidedMoving)
                {
                    currentSpeed = baseSpeed;
                    target = currentTargetNode.transform.position;

                    float distanceToDestination = Vector3.Distance(transform.position, destinationPosition.Value);
                    if (distanceToDestination < arrivalDistance)
                    {
                        EndRide();
                    }
                }

                if (state == CarAgentState.SystemGuidedMoving)
                {
                    currentSpeed = systemSpeed;
                }
                transform.position = Vector3.MoveTowards(transform.position, target.Value, Time.deltaTime * currentSpeed);
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
            state = CarAgentState.SystemGuidedMoving;
        }
        else
        {
            // Recalculate path
            currentSpeed = baseSpeed;
            state = CarAgentState.SelfGuidedMoving;
        }
    }

    public void SetColumnState(CarAgentColumnState state)
    {
        columnState = state;
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
        state = CarAgentState.SelfGuidedMoving;     
    }

    public Vector3 GetTarget()
    {
        return target;
    }

    public CarAgent ConnectCommunicationAgent(CommunicationAgent communicationAgent)
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

    public Vector3 GetCarPosition()
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
            Debug.Log("Car " + gameObject.name + ": " + message);
        }
    }


}