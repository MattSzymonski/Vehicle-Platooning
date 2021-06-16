using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class SpawningWave {
    public GameObject startNode;
    public int vehiclesToSpawn;
}

public class SimulationManager : MonoBehaviour
{
    [Header("Info")]
    [ReadOnly] public int spawnedCount = 0;
    [ReadOnly] public float speedMultiplier = 1.0f;
    [ReadOnly] public bool spawning;
    List<SpawningWave> spawningWaves;
    [ReadOnly] public float totalFuelUsed;

    int vehicleCount;
    
    [Header("References")]
    public GameObject vehicleAgentPrefab;
    public GameObject centralAgentPrefab;
    public NavSystem navSystem;
    public GameObject agentsParent;
    public AgentPlatform agentPlatform;
    public CentralAgent centralAgent;
    private EventSystem eventSystem;

    [Header("UI")]
    public Text simulationSpeedText;
    public Text agentsCountText;
    public Text spawnButtonText;
    public Text fuelUsedText;

    [Header("Vehicle Agent Settings")]
    [MinMaxSlider(20.0f, 200.0f)] public Vector2 speedRange = new Vector2(70.0f, 120.0f);

    [Header("Spawning Settings")]
    public int minVehiclesInSpawningWave = 1;
    public int maxVehiclesInSpawningWave = 4;

    public float waveSpawn_Timeout = 50.0f;
    float waveSpawn_Timer;

    public float spawn_Timeout = 50.0f;
    float spawn_Timer;


    void Start()
    {
        eventSystem = GameObject.Find("EventSystem").GetComponent<EventSystem>();
        Time.timeScale = speedMultiplier;
        spawningWaves = new List<SpawningWave>();
        SpawnCentralAgent();
    }

    void Update()
    {
        vehicleCount = agentPlatform.GetRegisteredAgents().Count();
        agentsCountText.text = vehicleCount.ToString();

        if (spawning)
        {
            SimulatedSpawning_SpawnWave();
        }
           
        SimulatedSpawning_ProcessSpawningWaves();
        SpawnAgentsManually();
        CalculateTotalFuelUsed();
    }

    public void ResetScene()
    {
        SceneManager.LoadScene(0);
    }

    void CalculateTotalFuelUsed()
    {
        foreach (Transform agent in agentPlatform.transform)
        {
            if (agent != null && agent.tag == "Vehicle")
            {
                totalFuelUsed += agent.transform.GetComponent<Fuel>().currentConsumption * (Time.deltaTime * 10);
            }
        }

        fuelUsedText.text = totalFuelUsed.ToString();
    }

    void SimulatedSpawning_SpawnWave()
    {
        if (waveSpawn_Timer > waveSpawn_Timeout)
        {
            if (vehicleCount < 100)
            {
                GameObject startNode = navSystem.nodes[Random.Range(0, navSystem.nodes.Count)];
                spawningWaves.Add(new SpawningWave() { startNode = startNode, vehiclesToSpawn = Random.Range(1, maxVehiclesInSpawningWave) });
                waveSpawn_Timer = 0;
            }
        }
        else
        {
            waveSpawn_Timer++;
        }
    }

    void SimulatedSpawning_ProcessSpawningWaves()
    {
        if (spawn_Timer > spawn_Timeout)
        {
            for (int i = spawningWaves.Count - 1; i >= 0; i--)
            {
                if (spawningWaves[i].vehiclesToSpawn > 0)
                {
                    GameObject destinationNode = spawningWaves[i].startNode;
                    while (destinationNode == spawningWaves[i].startNode)
                        destinationNode = navSystem.nodes[Random.Range(minVehiclesInSpawningWave, navSystem.nodes.Count)];
                    SpawnVehicle(spawningWaves[i].startNode, destinationNode, Mathf.Lerp(speedRange.x, speedRange.y, Random.Range(0.0f, 1.0f)));
                    spawningWaves[i].vehiclesToSpawn--;
                }
                else
                {
                    spawningWaves.RemoveAt(i);
                }
            }
            spawn_Timer = Random.Range(0.0f, 1.0f) > 0.5f ? 0 : 0.5f; // Randomize timer
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
        centralAgent = newCentralAgent.GetComponent<CentralAgent>();
        centralAgent.agentName = "CentralAgent";
    }

    void SpawnVehicleAtRandomNode()
    {
        GameObject startNode = navSystem.nodes[Random.Range(0, navSystem.nodes.Count)];

        GameObject destinationNode = startNode;
        while (destinationNode == startNode)
            destinationNode = navSystem.nodes[Random.Range(0, navSystem.nodes.Count)];

        SpawnVehicle(startNode, destinationNode, Mathf.Lerp(speedRange.x, speedRange.y, Random.Range(0.0f, 1.0f)));
    }

    void SpawnVehicle(GameObject startNode, GameObject destinationNode, float baseSpeed, bool platooningSystemEnabled = true)
    {
        GameObject newVehicle = Instantiate(vehicleAgentPrefab, startNode.transform.position, Quaternion.identity);
        newVehicle.transform.parent = agentsParent.transform;

        // Setup rendering
        newVehicle.transform.GetChild(0).GetComponent<MeshRenderer>().material.SetColor("_BaseColor", navSystem.colors[navSystem.nodes.IndexOf(destinationNode)]);
        newVehicle.tag = "Vehicle";

        // Setup VehicleAgent
        var vehicleAgent = newVehicle.GetComponent<VehicleAgent>();
        vehicleAgent.startNodeName = startNode.name;
        vehicleAgent.destinationNodeName = destinationNode.name;
        vehicleAgent.SetUp(baseSpeed);

        // Setup CommunicationAgent
        if (platooningSystemEnabled)
        {
            var communicationAgent = newVehicle.GetComponent<CommunicationAgent>();
            communicationAgent.agentName = "CommunicationAgent_" + spawnedCount.ToString();
            communicationAgent.centralAgentName = "CentralAgent";
            communicationAgent.agentPlatform = agentPlatform;
        }
        else
        {
            Destroy(newVehicle.GetComponent<CommunicationAgent>());
        }

        spawnedCount++;
    }

    void SpawnAgentsManually()
    {
        if (eventSystem.IsPointerOverGameObject())
            return;

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

                SpawnVehicle(closestNode, destinationNode, Mathf.Lerp(speedRange.x, speedRange.y, Random.Range(0.0f, 1.0f)));
            }
        }
    }

    public void SimulationSpeedUp()
    {
        speedMultiplier = Mathf.Min(speedMultiplier + 0.5f, 10.0f);
        simulationSpeedText.text = speedMultiplier.ToString();
        Time.timeScale = speedMultiplier;
    }

    public void SimulationSpeedDown()
    {
        speedMultiplier = Mathf.Max(0.0f, speedMultiplier - 0.5f);
        simulationSpeedText.text = speedMultiplier.ToString();
        Time.timeScale = speedMultiplier;
    }

    public void SpawnButtonToggle()
    {
        spawning = !spawning;
        spawnButtonText.text = spawning ? "Stop" : "Start";
    }

    public void LaunchSimulationScenario(int index)
    {
        IEnumerator coroutine = null;
        switch (index)
        {
            case 1:
                coroutine = SpawnScenario1();
                break;
            case 2:
                coroutine = SpawnScenario2();
                break;
            default:
                break;
        }
        StartCoroutine(coroutine);
    }

    IEnumerator SpawnScenario1()
    {
        // 3 agents start at node 1, form a platoon and move to node 7
        // 3 agents start at node 3, form a platoon, move to last common point which is node 4, two of them go to node 5 and one of them move to node 7
        // 1 agent start at node 2 move to node 1 where it joins platoon and move with it to node 4 and ends there

        yield return new WaitForSeconds(0.0f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (1)"), navSystem.nodes.Find(o => o.name == "Node (7)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.5f));
        yield return new WaitForSeconds(0.5f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (1)"), navSystem.nodes.Find(o => o.name == "Node (7)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.6f));
        yield return new WaitForSeconds(0.7f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (1)"), navSystem.nodes.Find(o => o.name == "Node (7)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.7f));

        yield return new WaitForSeconds(0.5f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (3)"), navSystem.nodes.Find(o => o.name == "Node (5)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.5f));
        yield return new WaitForSeconds(1.0f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (3)"), navSystem.nodes.Find(o => o.name == "Node (5)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.6f));
        yield return new WaitForSeconds(1.5f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (3)"), navSystem.nodes.Find(o => o.name == "Node (7)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.7f));

        yield return new WaitForSeconds(1.0f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (2)"), navSystem.nodes.Find(o => o.name == "Node (4)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.7f));
    }

    IEnumerator SpawnScenario2()
    {
        // Same as scenario 1 but with platooning system disabled

        yield return new WaitForSeconds(0.0f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (1)"), navSystem.nodes.Find(o => o.name == "Node (7)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.5f), false);
        yield return new WaitForSeconds(0.5f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (1)"), navSystem.nodes.Find(o => o.name == "Node (7)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.6f), false);
        yield return new WaitForSeconds(0.7f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (1)"), navSystem.nodes.Find(o => o.name == "Node (7)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.7f), false);

        yield return new WaitForSeconds(0.5f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (3)"), navSystem.nodes.Find(o => o.name == "Node (5)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.5f), false);
        yield return new WaitForSeconds(1.0f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (3)"), navSystem.nodes.Find(o => o.name == "Node (5)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.6f), false);
        yield return new WaitForSeconds(1.5f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (3)"), navSystem.nodes.Find(o => o.name == "Node (7)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.7f), false);

        yield return new WaitForSeconds(1.0f);
        SpawnVehicle(navSystem.nodes.Find(o => o.name == "Node (2)"), navSystem.nodes.Find(o => o.name == "Node (4)"), Mathf.Lerp(speedRange.x, speedRange.y, 0.7f), false);
    }
}

