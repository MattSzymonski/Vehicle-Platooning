using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This is logic that alters car model position so it looks like it is following different lanes of the same highway
// It does this skipping message passing logic! But it is only for visualization purposes, not simulation!
// So do not treat it as a part of simulation!
public class Visualizer : MonoBehaviour
{
    CommunicationAgent communicationAgent;
    CarAgent carAgent;
    GameObject model;

    [Header("Lane Randomization")]
    [MinMaxSlider(0.0f, 5.0f)] public Vector2 laneRandomizationRange = new Vector2(0.2f, 2.0f);
    public float laneRandomizationSmoothness = 0.5f;
    [ReadOnly] public float baseLaneRandomization;
    [ReadOnly] public float currentLaneRandomization;

    [Header("Rotation")]
    public float rotationSmoothness = 2.5f; // Rotation in movement direction turn speed

    [Header("Column Line")]
    public Material columnLineDebugMaterial;
    LineRenderer lineRenderer;

    void Start()
    {
        communicationAgent = transform.parent.GetComponent<CommunicationAgent>();
        carAgent = transform.parent.GetComponent<CarAgent>();
        model = this.gameObject;

        baseLaneRandomization = Random.Range(laneRandomizationRange.x, laneRandomizationRange.y);

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.enabled = false;  
        lineRenderer.material = columnLineDebugMaterial;
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.SetPosition(0, new Vector3(0, -10, 0));
        lineRenderer.SetPosition(1, new Vector3(0, -10, 0));
    }

    void Update()
    {
        RotateTowardsMoveDirection();
        RandomizeLane();
        DrawColumnLine();
    }

    void RotateTowardsMoveDirection()
    {
        var lookPos = carAgent.GetCurrentTargetNodePosition() - transform.parent.position;
        lookPos.y = 0;
        var rotation = Quaternion.LookRotation(lookPos);
        transform.parent.rotation = Quaternion.Slerp(transform.parent.rotation, rotation, Time.deltaTime * rotationSmoothness);
    }

    void RandomizeLane()
    {
        if (communicationAgent.isInColumn) // Is in column
        {
            if (communicationAgent.isColumnLeader)
            {
                currentLaneRandomization = baseLaneRandomization;
            }
            else // Set randomization to the same as leader of column
            {
                currentLaneRandomization = GameObject.Find(communicationAgent.currentColumnData.leaderName).transform.GetChild(0).GetComponent<Visualizer>().currentLaneRandomization;
            }
        }

        Vector3 targetModelPosition = new Vector3(currentLaneRandomization, transform.localPosition.y, transform.localPosition.z);
        model.transform.localPosition = Vector3.Lerp(transform.localPosition, targetModelPosition, Time.deltaTime * laneRandomizationSmoothness);
    }

    void DrawColumnLine()
    {
        if (communicationAgent.isInColumn) // Is in column
        {
            if (!communicationAgent.isColumnLeader)
            {
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, transform.position);
                lineRenderer.SetPosition(1, GameObject.Find(communicationAgent.currentColumnData.followAgentName).transform.GetChild(0).position);
            }
        }
    }
}