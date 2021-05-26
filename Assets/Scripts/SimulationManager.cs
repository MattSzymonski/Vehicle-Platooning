using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SimulationManager : MonoBehaviour
{
    [Header("Info")]
    [ReadOnly] public float speedMultiplier = 1.0f;
    [ReadOnly] public bool spawning;

    [Header("Settings")]
    public int carCount;
    public float spawnDelay = 1;

    public Material columnDebugMaterial;

    [Header("References")]
    public GameObject carAgentPrefab;
    public GameObject centralAgentPrefab;
    public NavSystem navSystem;
    public GameObject agentsParent;
    public AgentPlatform agentPlatform;



    [Header("Car Agent Settings")]
    //public float speed = 1;
    public float arrivalDistance = 0.1f;
    [MinMaxSlider(0.0f, 10.0f)] public Vector2 speedRange;
    public float columnJoinRadius = 1.0f;


    float timer;
    int spawnedCount = 0;

    [Header("UI")]
    public Text simulationSpeedText;
    

    void Start()
    {
        Time.timeScale = speedMultiplier;
        SpawnCentralAgent();
    }

   

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            spawning = !spawning;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            SpawnCarAtRandomNode();
        }

        SpawnAgentsManually();

        if (spawning)
        {
            if(timer > spawnDelay)
            {
                if (spawnedCount < 1)
                {
                    SpawnCarAtRandomNode();
                }
                
                timer = 0;
            }
            else
            {
                timer++;
            }
        }    
    }


    void SpawnCentralAgent()
    {
        GameObject newCentralAgent = Instantiate(centralAgentPrefab, Vector3.zero, Quaternion.identity);
        newCentralAgent.transform.parent = agentsParent.transform;

        // Setup CentralAgent
        var centralAgent = newCentralAgent.GetComponent<CentralAgent>();
        centralAgent.agentName = "CentralAgent";
    }

    void SpawnCarAtRandomNode()
    {
        GameObject startNode = navSystem.nodes[Random.Range(0, navSystem.nodes.Count)];

        GameObject destinationNode = startNode;
        while (destinationNode == startNode)
            destinationNode = navSystem.nodes[Random.Range(0, navSystem.nodes.Count)];

        SpawnCar(startNode, destinationNode);
    }

    void SpawnCar(GameObject startNode, GameObject destinationNode)
    {
        GameObject newCar = Instantiate(carAgentPrefab, startNode.transform.position, Quaternion.identity);
        newCar.transform.parent = agentsParent.transform;

        // Setup rendering
        newCar.transform.GetChild(0).GetComponent<MeshRenderer>().material.SetColor("_BaseColor", navSystem.colors[navSystem.nodes.IndexOf(destinationNode)]);

        // Setup CarAgent
        var carAgent = newCar.GetComponent<CarAgent>();
        carAgent.startNodeName = startNode.name;
        carAgent.destinationNodeName = destinationNode.name;
        carAgent.SetUp(Mathf.Lerp(speedRange.x, speedRange.y, Random.Range(0.0f, 1.0f)), arrivalDistance);

        // Setup CommunicationAgent
        var communicationAgent = newCar.GetComponent<CommunicationAgent>();
        communicationAgent.agentName = "CommunicationAgent_" + spawnedCount.ToString(); //System.Guid.NewGuid().ToString();
        communicationAgent.columnJoinRadius = 1.0f;
        communicationAgent.reachDestinationRadius = 0.1f;
        communicationAgent.centralAgentName = "CentralAgent";
        communicationAgent.agentPlatform = agentPlatform;
        communicationAgent.columnDebugMaterial = columnDebugMaterial;

        spawnedCount++;
    }

    void SpawnAgentsManually()
    {


        if (Input.GetMouseButtonDown(0))
        {
            //create a ray cast and set it to the mouses cursor position in game
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 1000, LayerMask.GetMask("Ground")))
            {
                //draw invisible ray cast/vector
                Debug.DrawLine(ray.origin, hit.point);
                //Debug.Log(hit.point);

                // Find closest node
                var closestNode = navSystem.nodes.OrderBy(o => Vector3.Distance(o.transform.position, hit.point)).First();
               
                // Find other random node
                GameObject destinationNode = closestNode;
                while (destinationNode == closestNode)
                    destinationNode = navSystem.nodes[Random.Range(0, navSystem.nodes.Count)];

                SpawnCar(closestNode, destinationNode);
            }
        }
    }

    public void SimulationSpeedUp()
    {
        speedMultiplier = Mathf.Min(speedMultiplier + 0.25f, 3.0f);
        simulationSpeedText.text = speedMultiplier.ToString();
        Time.timeScale = speedMultiplier;
    }

    public void SimulationSpeedDown()
    {
        speedMultiplier = Mathf.Max(0.25f, speedMultiplier - 0.25f);
        simulationSpeedText.text = speedMultiplier.ToString();
        Time.timeScale = speedMultiplier;
    }



}

