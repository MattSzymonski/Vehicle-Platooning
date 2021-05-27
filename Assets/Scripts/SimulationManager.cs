using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SpawningWave {
    public GameObject startNode;
    public int carsToSpawn;
}

public class SimulationManager : MonoBehaviour
{
    [Header("Info")]
    [ReadOnly] public float speedMultiplier = 1.0f;
    [ReadOnly] public bool spawning;
    [ReadOnly] public List<SpawningWave> spawningWaves;

    [Header("Settings")]
    public int carCount;
    public Material columnDebugMaterial;

    [Header("References")]
    public GameObject carAgentPrefab;
    public GameObject centralAgentPrefab;
    public NavSystem navSystem;
    public GameObject agentsParent;
    public AgentPlatform agentPlatform;

    [Header("UI")]
    public Text simulationSpeedText;
    public Text agentsCountText;
    public Text spawnButtonText;


    [Header("Car Agent Settings")]
    public float arrivalDistance = 0.1f;
    [MinMaxSlider(0.0f, 10.0f)] public Vector2 speedRange;
    public float columnJoinRadius = 1.0f;

    [Header("Spawning Settings")]
    public int minCarsInSpawningWave = 1;
    public int maxCarsInSpawningWave = 4;

    public float waveSpawn_Timeout = 1.0f;
    [ReadOnly] public float waveSpawn_Timer;

    public float spawn_Timeout = 1.0f;
    [ReadOnly] public float spawn_Timer;



    int spawnedCount = 0;

    
    
    void Start()
    {
        Time.timeScale = speedMultiplier;
        spawningWaves = new List<SpawningWave>();
        SpawnCentralAgent();
    }

    void Update()
    {
        carCount = agentPlatform.GetRegisteredAgents().Count();
        agentsCountText.text = carCount.ToString();

        if (Input.GetKeyDown(KeyCode.T))
        {
            spawning = !spawning;
        }

        if (spawning)
        {
            SimulatedSpawning1_SpawnWave();
        }
        SimulatedSpawning1_ProcessSpawningWaves();



        if (Input.GetKeyDown(KeyCode.Y))
        {
            SpawnCarAtRandomNode();
        }

        SpawnAgentsManually();
    }



    void SimulatedSpawning1_SpawnWave()
    {
        if (waveSpawn_Timer > waveSpawn_Timeout)
        {
            if (carCount < 100)
            {
                GameObject startNode = navSystem.nodes[Random.Range(0, navSystem.nodes.Count)];
                spawningWaves.Add(new SpawningWave() { startNode = startNode, carsToSpawn = Random.Range(1, maxCarsInSpawningWave) });
                waveSpawn_Timer = 0;
            }
        }
        else
        {
            waveSpawn_Timer++;
        }
    }

    void SimulatedSpawning1_ProcessSpawningWaves()
    {
        if (spawn_Timer > spawn_Timeout)
        {
            for (int i = spawningWaves.Count - 1; i >= 0; i--)
            {
                if (spawningWaves[i].carsToSpawn > 0)
                {
                    GameObject destinationNode = spawningWaves[i].startNode;
                    while (destinationNode == spawningWaves[i].startNode)
                        destinationNode = navSystem.nodes[Random.Range(minCarsInSpawningWave, navSystem.nodes.Count)];
                    SpawnCar(spawningWaves[i].startNode, destinationNode);
                    spawningWaves[i].carsToSpawn--;
                }
                else
                {
                    spawningWaves.RemoveAt(i);
                }
            }
            spawn_Timer = Random.Range(0.0f, 1.0f) > 0.5f ? 0 : 0.5f; // Randomize timer
            //spawn_Timer = 0;
        }
        else
        {
            spawn_Timer++;
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
        communicationAgent.agentName = "CommunicationAgent_" + spawnedCount.ToString();
        communicationAgent.columnJoinRadius = 1.0f;
        communicationAgent.reachDestinationRadius = 0.1f;
        communicationAgent.centralAgentName = "CentralAgent";
        communicationAgent.agentPlatform = agentPlatform;

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

    public void SpawnButtonToggle()
    {
        spawning = !spawning;
        spawnButtonText.text = spawning ? "Stop" : "Start";
    }

}

