using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public enum CommunicationAgentState
{
    RegisteringInAgentPlatform,
    RegisteringInCentralAgent_Send,
    RegisteringInCentralAgent_Wait,
    ConnectingToCarAgent,
    CentralAgentInitialDataUpdate_Send,
    ColumnCreateProposal_Wait,
    CreateColumnConfirmation_Wait,
    ColumnSearching_Send,
    ColumnSearching_Wait,
    JoiningColumn_Send,
    JoiningColumn_Wait,
    CreatingColumn_Send,
    CreatingColumn_Wait,
    CreatingNewColumn,
    MovingInColumn,
}

// What if request to create agent comes from other agent??

public class CommunicationAgent : Agent
{
    [Header("General Info")]
    [ReadOnly] public CommunicationAgentState state = CommunicationAgentState.RegisteringInAgentPlatform;
    [ReadOnly] public bool registeredInCentralAgent;

    [Header("Column Moving Info")]
    [ReadOnly] public bool isInColumn;
    [ReadOnly] public bool isColumnLeader;
    [ReadOnly] public ColumnData currentColumnData; // Data about the column in which car is moving (sent by CommunicationAgent of column leader car)
    [ReadOnly] public Vector3? target; // Data about target to which car should be moving to keep column formation (sent by CommunicationAgent of car in front)
    [ReadOnly] public string followingAgentTargetNodeName; // Next node on agent's that this agent follows path
    [ReadOnly] public string lastCommonColumnNodeName = "";
    [ReadOnly] public List<string> columnCarsNames; // Only for leader

    private CarAgent carAgent;
    private List<string> pendingAcceptingCars; // List of CommunicationAgents names that accepted column creation proposal

    [Header("Settings")]
    public float columnJoinRadius = 1.0f;
    public float reachDestinationRadius = 0.3f;
    public string centralAgentName; // Entered via UI by user (mobile app or on car's cockpit screen)
    public float columnSpeed = 0.2f;
    public float waitForColumnSpeed = 0.1f; // Speed of car when it needs to wait to be overtook by the column it joined
    public float catchUpColumnSpeed = 0.4f; // Speed of car when it is further than catchUpColumnDistance from car it is following, if distance is smaller then column speed is used
    public float catchUpColumnDistance = 0.15f; 
    public float betweenCarDistances = 0.1f; // Distance from car in direction oposite to its driving direction (it should be similar to catchUpColumnDistance)
    public int maxColumnSize = 5; // Maximal number of cars in column

    [HideInInspector] public string carAgentName = ""; // Entered via UI by user (mobile app or on car's cockpit screen)
    [HideInInspector] public string carAgentPassword = "";  // Entered via UI by user (mobile app or on car's cockpit screen)

    List<(string, int)> columnsInProximity; // Names of column leaders to join found when asking central agent, ordered on the stack from best to worst candidate in terms of most common nodes on the path (Name of leader and number of common points)
    List<string> lonelyCarsInProximity; // Names of communication agents not in columns found when asking central agent

    [Header("Update Settings")]
    public float registeringInCentralAgent_Wait_Timeout = 1.0f;
    float registeringInCentralAgent_Wait_Timer = 0.0f;

    public float columnSearching_Wait_Timeout = 1.0f;
    float columnSearching_Wait_Timer = 0.0f;

    public float joiningColumn_Wait_Timeout = 1.0f;
    float joiningColumn_Wait_Timer = 0.0f;

    public float creatingColumn_Wait_Timeout = 1.0f;
    float creatingColumn_Wait_Timer = 0.0f;

    public float creatingColumnProposal_Wait_Timeout = 1.0f;
    public float creatingColumnProposal_Wait_Timeout_Randomizer = 3.0f;
    float creatingColumnProposal_Wait_Timer = 0.0f;

    public float creatingColumnConfirmation_Wait_Timeout = 1.0f;
    float creatingColumnConfirmation_Wait_Timer = 0.0f;

    public float updateCarDataInCentralAgent_Timeout = 1.0f;
    float updateCarDataInCentralAgent_Timer = 0.0f;

    public float updateCarBehind_Timeout = 1.0f;
    float updateCarBehind_Timer = 0.0f;

    CommunicationAgentState[] setupStates = { 
        CommunicationAgentState.RegisteringInAgentPlatform, 
        CommunicationAgentState.RegisteringInCentralAgent_Send, 
        CommunicationAgentState.RegisteringInCentralAgent_Wait, 
        CommunicationAgentState.ConnectingToCarAgent,
        CommunicationAgentState.CentralAgentInitialDataUpdate_Send
    };

    void Start()
    {
        name = agentName;
        gameObject.name = agentName;

        columnCarsNames = new List<string>();
        pendingAcceptingCars = new List<string>();
        columnsInProximity = new List<(string, int)>();
        lonelyCarsInProximity = new List<string>();

        creatingColumnProposal_Wait_Timer = Random.Range(0.0f, creatingColumnProposal_Wait_Timeout); // Randomize timer starting point to avoid stagnation when multiple agents are created in the same time
    }

    void Update() // Each frame
    {
        MainLoop();
    }

    void MainLoop()
    {
        Message message = base.ReceiveMessage();
        Content receiveContent = message != null ? JsonUtility.FromJson<Content>(message.GetContent()) : null;

        // --- Setup states --- 

        // Register this agent in Agent Platform (needed to send messages)
        if (state == CommunicationAgentState.RegisteringInAgentPlatform)
        {
            var response = RegisterInAgentPlatform();
            if (response)
            {
                state = CommunicationAgentState.RegisteringInCentralAgent_Send;
            }

            return;
        }

        // Send registration request to Central Agent to register this agent in the System
        if (state == CommunicationAgentState.RegisteringInCentralAgent_Send)
        {
            string content = Utils.CreateContent(SystemAction.CommunicationAgent_RegisterInCentralAgent, "");
            base.SendMessage(Peformative.Request.ToString(), content, agentName, centralAgentName);
            state = CommunicationAgentState.RegisteringInCentralAgent_Wait;

            return;
        }

        // Wait for response for registration from Central Agent (Accept or Reject)
        if (state == CommunicationAgentState.RegisteringInCentralAgent_Wait)
        {
            // Read message from central agent
            if (message != null && receiveContent.action == SystemAction.CommunicationAgent_RegisterInCentralAgent.ToString())
            {
                if (message.GetPerformative() == nameof(Peformative.Accept))
                {
                    registeredInCentralAgent = true;
                    state = CommunicationAgentState.ConnectingToCarAgent;
                }
                else if (message.GetPerformative() == nameof(Peformative.Reject))
                {
                    state = CommunicationAgentState.RegisteringInCentralAgent_Send; // Try once again

                    return;
                }
            }
            
            // Wait for some time for answers then try to send once again
            if (registeringInCentralAgent_Wait_Timer >= registeringInCentralAgent_Wait_Timeout)
            {
                state = CommunicationAgentState.RegisteringInCentralAgent_Send;
                registeringInCentralAgent_Wait_Timer = 0;

                return;
            }
            else
            {
                registeringInCentralAgent_Wait_Timer += Time.deltaTime;
            }
        }

        // Connect to Car Agent using its exposed API
        if (state == CommunicationAgentState.ConnectingToCarAgent)
        {
            ConnectToCarAgent(carAgentName, carAgentPassword);
            state = CommunicationAgentState.CentralAgentInitialDataUpdate_Send;

            return;
        }

        // Send initial car data update to Central Agent
        if (state == CommunicationAgentState.CentralAgentInitialDataUpdate_Send)
        {
            UpdateCarDataInCentralAgent();
            state = CommunicationAgentState.ColumnCreateProposal_Wait;

            return;
        }

        // --- Main states ---

        // Wait for column create proposals from other agents, respond with accept or reject
        if (state == CommunicationAgentState.ColumnCreateProposal_Wait)
        {
            if (message != null && receiveContent.action == SystemAction.CommunicationAgent_CreateColumn.ToString())
            {
                if (message.GetPerformative() == Peformative.Propose.ToString())
                {
                    ColumnCreateData columnCarData = JsonUtility.FromJson<ColumnCreateData>(receiveContent.contentDetails);

                    // If column goes in the same direction then accept the proposal
                    List<string> path = carAgent.GetPathNodesNames();
                    string currentNode = carAgent.GetCurrentTargetNodeName();
                    
                    List<string> currentPath = path.GetRange(path.IndexOf(currentNode) - 1, path.Count - path.IndexOf(currentNode) + 1); // Nodes which are left on the path (has not been reached yet) (and last visited node)
                    List<string> currentColumnPath = columnCarData.pathNodesNames.GetRange(columnCarData.pathNodesNames.IndexOf(columnCarData.currentTargetNodeName) - 1, columnCarData.pathNodesNames.Count - columnCarData.pathNodesNames.IndexOf(columnCarData.currentTargetNodeName) + 1); // Nodes which are left on the path (has not been reached yet)

                    // Calculate common points count in the same direction
                    List<string> pathLeft1 = currentColumnPath.Count >= currentPath.Count ? currentColumnPath : currentPath;
                    List<string> pathLeft2 = currentColumnPath.Count < currentPath.Count ? currentColumnPath : currentPath;
                    int pathCurrentIndex = 0;
                    for (int i = 0; i < pathLeft1.Count; i++) // Iterate longer list and count same items along the way on the shorter list
                    {
                        if (pathCurrentIndex <= pathLeft2.Count - 1) // Check if index is in range
                        {
                            if (pathLeft1[i] == pathLeft2[pathCurrentIndex])
                            {
                                pathCurrentIndex++;
                            }
                        }
                    }
                    int commonPointsCount = pathCurrentIndex;
                    
                    if (commonPointsCount > 1) // Have common points and same direction so accept proposal
                    {
                        string content = Utils.CreateContent(SystemAction.CommunicationAgent_CreateColumn, "");
                        base.SendMessage(Peformative.Accept.ToString(), content, agentName, message.GetSender());
                        state = CommunicationAgentState.CreateColumnConfirmation_Wait;
                        creatingColumnProposal_Wait_Timer = 0;
                    }
                    else // Not same direction so reject proposal
                    {
                        string content = Utils.CreateContent(SystemAction.CommunicationAgent_CreateColumn, "");
                        base.SendMessage(Peformative.Reject.ToString(), content, agentName, message.GetSender());
                    }

                    return;
                }
            }
            
            // Wait for some time for answers then try to find or create column
            if (creatingColumnProposal_Wait_Timer >= creatingColumnProposal_Wait_Timeout + creatingColumnProposal_Wait_Timeout_Randomizer)
            {
                state = CommunicationAgentState.ColumnSearching_Send;
                creatingColumnProposal_Wait_Timer = 0;

                // It may happen that multiple Communication Agents will fall into CreateColumnProposal_Wait-CreatingColumn_Send loop at the same time
                // In such case they will never form a column because they will be asking each other and ignoring proposal messages
                // So the job of this timer is to prevent such situation by randomly delaying CreatingColumnProposal_Wait
                creatingColumnProposal_Wait_Timeout_Randomizer = (Random.Range(0, 1) == 1) ? 4.0f : 0.0f;

                return;
            }
            else
            {
                creatingColumnProposal_Wait_Timer += Time.deltaTime;
            }
        }
        // Reject all proposals (because now agent is in different state)
        else
        {
            if (message != null && receiveContent.action == SystemAction.CommunicationAgent_CreateColumn.ToString())
            {
                if (message.GetPerformative() == Peformative.Propose.ToString())
                {
                    string content = Utils.CreateContent(SystemAction.CommunicationAgent_CreateColumn, "");
                    base.SendMessage(Peformative.Reject.ToString(), content, agentName, message.GetSender());
                }
            }          
        }

        // Wait for column create confirmation from future column leader (the one that has send the proposal) when proposal has been accepted
        if (state == CommunicationAgentState.CreateColumnConfirmation_Wait)
        {
            if (message != null && receiveContent.action == SystemAction.CommunicationAgent_CreateColumn.ToString())
            {
                if (message.GetPerformative() == Peformative.Confirm.ToString())
                {
                    currentColumnData = JsonUtility.FromJson<ColumnData>(receiveContent.contentDetails); // Receive and store info about this column
                    isInColumn = true;
                    lastCommonColumnNodeName = LastCommonNodeOnPath();
                    state = CommunicationAgentState.MovingInColumn;
                    creatingColumnConfirmation_Wait_Timer = 0;
                        
                    return;
                }
            }
            
            // Wait for some time for answers then try to find or create column
            if (creatingColumnConfirmation_Wait_Timer >= creatingColumnConfirmation_Wait_Timeout)
            {
                state = CommunicationAgentState.ColumnSearching_Send;
                creatingColumnConfirmation_Wait_Timer = 0;

                return;
            }
            else
            {
                creatingColumnConfirmation_Wait_Timer += Time.deltaTime;
            }
        }

        // Send query to Central Agent to find columns and lonely cars in proximity
        if (state == CommunicationAgentState.ColumnSearching_Send)
        {
            string content = Utils.CreateContent(SystemAction.CommunicationAgent_NearbyCars, "");
            base.SendMessage(Peformative.Query.ToString(), content, agentName, centralAgentName);
            state = CommunicationAgentState.ColumnSearching_Wait;

            return;
        }

        // Wait for response with columns and lonely cars in proximity from Central Agent, and find best column to join or create new if not columns in proximity
        if (state == CommunicationAgentState.ColumnSearching_Wait) 
        {
            if (message != null && receiveContent.action == SystemAction.CommunicationAgent_NearbyCars.ToString()) 
            {
                if (message.GetPerformative() == Peformative.Inform.ToString())
                {
                    ColumnQueryData columnCarData = JsonUtility.FromJson<ColumnQueryData>(receiveContent.contentDetails);

                    DebugLog(columnCarData.columnLeaderCommunicationAgents.Count + " - " + columnCarData.lonelyCommunicationAgents.Count);

                    // If column found - try to join
                    {
                        if (columnCarData.columnLeaderCommunicationAgents.Count > 0)
                        {
                            // First find columns that have the greatest number of common nodes with path of this agent and are going in the same direction
                            // Store them ordered in list, if list is not empty then call best agent, remove it from list and wait for response. 
                            // If list is emptied ask central agent again for columns in proximity
                            
                            List<string> path = carAgent.GetPathNodesNames();
                            string currentNode = carAgent.GetCurrentTargetNodeName();
                            List<string> currentPath = path.GetRange(path.IndexOf(currentNode) - 1, path.Count - path.IndexOf(currentNode) + 1); // Nodes which are left on the path (has not been reached yet) (and last visited node)
                          
                            foreach (var agent in columnCarData.columnLeaderCommunicationAgents)
                            {
                                List<string> currentColumnPath = agent.pathNodesNames.GetRange(agent.pathNodesNames.IndexOf(agent.currentTargetNodeName) - 1, agent.pathNodesNames.Count - agent.pathNodesNames.IndexOf(agent.currentTargetNodeName) + 1); // Nodes which are left on the path (has not been reached yet)

                                // Calculate common points count in the same direction
                                List<string> pathLeft1 = currentColumnPath.Count >= currentPath.Count ? currentColumnPath : currentPath;
                                List<string> pathLeft2 = currentColumnPath.Count < currentPath.Count ? currentColumnPath : currentPath;
                                int pathCurrentIndex = 0;
                                for (int i = 0; i < pathLeft1.Count; i++) // Iterate longer list and count same items along the way on the shorter list
                                {
                                    if (pathCurrentIndex <= pathLeft2.Count - 1) // Check if index is in range
                                    {
                                        if (pathLeft1[i] == pathLeft2[pathCurrentIndex])
                                        {
                                            pathCurrentIndex++;
                                        }
                                    }
                                }
                                int commonPointsCount = pathCurrentIndex;
                                if (commonPointsCount > 1)
                                {
                                    columnsInProximity.Add((agent.name, commonPointsCount));
                                }     
                            }

                            if (columnsInProximity != null && columnsInProximity.Count > 0)
                            {
                                columnsInProximity.OrderBy(x => x.Item2); // Order list in terms of greatest number of common points
                                state = CommunicationAgentState.JoiningColumn_Send;
                                return;
                            }
                        }
                    }

                    // If no columns found but some lonely cars found - try to form new column, send proposal to all nearby cars
                    {
                        columnCarData.lonelyCommunicationAgents.Sort((x, y) => x.distance.CompareTo(y.distance)); // Sort by distance
                        if (columnCarData.lonelyCommunicationAgents.Count > 0)
                        {
                            lonelyCarsInProximity = columnCarData.lonelyCommunicationAgents.Select(x => x.name).ToList();  
                            state = CommunicationAgentState.CreatingColumn_Send;

                            return;
                        }
                    } 
                }
            }
            
            // Wait for some time for answers then try to send once again
            if (columnSearching_Wait_Timer >= columnSearching_Wait_Timeout)
            {
                state = CommunicationAgentState.ColumnCreateProposal_Wait;
                columnSearching_Wait_Timer = 0;
            }
            else
            {
                columnSearching_Wait_Timer += Time.deltaTime;
            }
        }

        // Send join request to the best column candidate from list and remove it from list
        if (state == CommunicationAgentState.JoiningColumn_Send)
        {
            string columnLeaderName = columnsInProximity[columnsInProximity.Count - 1].Item1;
            columnsInProximity.RemoveAt(columnsInProximity.Count - 1);

            // Send request to join the column
            string content = Utils.CreateContent(SystemAction.CommunicationAgent_JoinColumn, "");
            base.SendMessage(Peformative.Request.ToString(), content, agentName, columnLeaderName);
            state = CommunicationAgentState.JoiningColumn_Wait;
        }

        // Wait for response from column leader (if no message coming or all were rejecting then start waiting for creating proposals again) and join column if receive acceptation
        if (state == CommunicationAgentState.JoiningColumn_Wait) 
        {
            if (message != null && receiveContent.action == SystemAction.CommunicationAgent_JoinColumn.ToString()) // Receive decision about joining column from its leader
            {
                if (message.GetPerformative() == Peformative.Accept.ToString()) // Join column
                {
                    currentColumnData = JsonUtility.FromJson<ColumnData>(receiveContent.contentDetails); // Receive and store info about this column
                    isInColumn = true;
                    columnsInProximity.Clear();
                    lastCommonColumnNodeName = LastCommonNodeOnPath();
                    state = CommunicationAgentState.MovingInColumn;

                    return;
                }
                if (message.GetPerformative() == Peformative.Reject.ToString())
                {
                    if (columnsInProximity.Count > 0)
                    {
                        state = CommunicationAgentState.JoiningColumn_Send;
                    }
                    else
                    {
                        state = CommunicationAgentState.ColumnCreateProposal_Wait;
                    }

                    joiningColumn_Wait_Timer = 0;
                    return;
                }
            }
            
            // Wait for some time for answers then try to find or create column again
            if (joiningColumn_Wait_Timer >= joiningColumn_Wait_Timeout)
            {
                if (columnsInProximity.Count > 0)
                {
                    state = CommunicationAgentState.JoiningColumn_Send;
                }
                else
                {
                    state = CommunicationAgentState.ColumnCreateProposal_Wait;
                }
                joiningColumn_Wait_Timer = 0;
                return;
            }
            else
            {
                joiningColumn_Wait_Timer += Time.deltaTime;
            }       
        }

        // Send create new column proposal to all lonely cars in proximity (discovered earlier via querying the Central Agent)
        if (state == CommunicationAgentState.CreatingColumn_Send)
        {
            ColumnCreateData columnCreateData = new ColumnCreateData()
            {
                leaderName = agentName,
                pathNodesNames = carAgent.GetPathNodesNames(),
                currentTargetNodeName = carAgent.GetCurrentTargetNodeName()
            };
            string contentDetails = JsonUtility.ToJson(columnCreateData);
            string content = Utils.CreateContent(SystemAction.CommunicationAgent_CreateColumn, contentDetails);
            lonelyCarsInProximity = lonelyCarsInProximity.Count() > maxColumnSize - 1 ? lonelyCarsInProximity.GetRange(0, maxColumnSize - 1) : lonelyCarsInProximity; // Limit number of cars in column
            foreach (var agent in lonelyCarsInProximity)
            {
                base.SendMessage(Peformative.Propose.ToString(), content, agentName, agent);
            }
            lonelyCarsInProximity.Clear();

            state = CommunicationAgentState.CreatingColumn_Wait;
        }

        // Wait for response from lonely cars in proximity and create new column (if no message coming or all were rejecting then start waiting for column creating proposals again) 
        if (state == CommunicationAgentState.CreatingColumn_Wait)
        {
            // Receive decision about joining this column from lonely cars
            if (message != null && receiveContent.action == SystemAction.CommunicationAgent_CreateColumn.ToString())
            {
                if (message.GetPerformative() == Peformative.Accept.ToString())
                {
                    pendingAcceptingCars.Add(message.GetSender());
                }
            }
            
            // Wait for some time for answers then if there are answers create column, if there are not wait for creating column proposals again
            if (creatingColumn_Wait_Timer >= creatingColumn_Wait_Timeout)
            {
                if (pendingAcceptingCars.Count > 0)
                {
                    columnCarsNames.Add(agentName); // Add self to list of cars in column

                    for (int i = 0; i < pendingAcceptingCars.Count; i++)
                    {            
                        ColumnData columnData = new ColumnData()
                        {
                            leaderName = agentName,
                            pathNodesNames = carAgent.GetPathNodesNames(),
                            followAgentName = columnCarsNames[columnCarsNames.Count - 1], // Follow last car
                            behindAgentName = i + 1 <= pendingAcceptingCars.Count - 1 ? pendingAcceptingCars[i+1] : "" // Car behind or nothing if i is last
                        };

                        string contentDetails = JsonUtility.ToJson(columnData);
                        string content = Utils.CreateContent(SystemAction.CommunicationAgent_CreateColumn, contentDetails);
                        base.SendMessage(Peformative.Confirm.ToString(), content, agentName, pendingAcceptingCars[i]);
                        columnCarsNames.Add(pendingAcceptingCars[i]);
                    }

                    // Create new column and add itself to it as leader
                    isInColumn = true;
                    isColumnLeader = true;
                    currentColumnData = new ColumnData()
                    {
                        leaderName = agentName,
                        pathNodesNames = carAgent.GetPathNodesNames(),
                        followAgentName = "",
                        behindAgentName = columnCarsNames[1]
                    };
                    lastCommonColumnNodeName = currentColumnData.pathNodesNames[currentColumnData.pathNodesNames.Count - 1];
                    pendingAcceptingCars.Clear();
                    creatingColumn_Wait_Timer = 0;
                    state = CommunicationAgentState.MovingInColumn;

                    return;
                }
                else
                {

                    creatingColumn_Wait_Timer = 0;
                    state = CommunicationAgentState.ColumnCreateProposal_Wait;

                    return;
                }
            }
            else
            {
                creatingColumn_Wait_Timer += Time.deltaTime;
            }
        }

        // Moving in column
        if (state == CommunicationAgentState.MovingInColumn)
        {
            carAgent.ToggleSystemGuidedMode(true);
                        
            if (isColumnLeader)
            {
                // Respond to join column requests
                if (message != null && receiveContent.action == SystemAction.CommunicationAgent_JoinColumn.ToString())
                {
                    if (message.GetPerformative() == Peformative.Request.ToString())
                    {
                        // Accept
                        if (columnCarsNames.Count < maxColumnSize)
                        {
                            // Update data in last car in the column
                            {
                                ColumnData columnData = new ColumnData()
                                {
                                    leaderName = agentName,
                                    pathNodesNames = carAgent.GetPathNodesNames(),
                                    followAgentName = columnCarsNames[columnCarsNames.Count - 2], // Same as before
                                    behindAgentName = message.GetSender() // New
                                };
                                string contentDetails = JsonUtility.ToJson(columnData);
                                string content = Utils.CreateContent(SystemAction.CommunicationAgent_UpdateColumn, contentDetails);
                                base.SendMessage(Peformative.Inform.ToString(), content, agentName, columnCarsNames[columnCarsNames.Count - 1]);
                            }

                            // Add new car to column
                            {
                                columnCarsNames.Add(message.GetSender());
                                ColumnData columnData = new ColumnData()
                                {
                                    leaderName = agentName,
                                    pathNodesNames = carAgent.GetPathNodesNames(),
                                    followAgentName = columnCarsNames[columnCarsNames.Count - 2], // Follow last car (except self)
                                    behindAgentName = "" // Nothing is behind
                                };

                                string contentDetails = JsonUtility.ToJson(columnData);
                                string content = Utils.CreateContent(SystemAction.CommunicationAgent_JoinColumn, contentDetails);
                                base.SendMessage(Peformative.Accept.ToString(), content, agentName, message.GetSender());
                            }
                        }
                        // Reject request if column size reached the limit
                        else
                        {
                            string content = Utils.CreateContent(SystemAction.CommunicationAgent_JoinColumn, "");
                            base.SendMessage(Peformative.Reject.ToString(), content, agentName, message.GetSender());  
                        }   
                    }
                }
                
                // Respond to column leave by other car
                if (message != null && receiveContent.action == SystemAction.CommunicationAgent_LeaveColumn_NotifyLeader.ToString())
                {
                    if (message.GetPerformative() == Peformative.Inform.ToString())
                    {
                        // Remove car from column and notify other cars (update their data about follow and behind CarAgents)
                        {
                            // Delete column (since only this agent and agent that is about to be removed are left)
                            if (columnCarsNames.Count <= 2)
                            {
                                currentColumnData = null;
                                isInColumn = false;
                                columnCarsNames.Clear();
                                lastCommonColumnNodeName = "";
                                state = CommunicationAgentState.ColumnCreateProposal_Wait;

                                return;
                            }

                            int leavingCarIndex = columnCarsNames.IndexOf(message.GetSender());

                            // Update data in car in front of leaving car
                            {
                                if (leavingCarIndex - 1 != 0) //  if car is not this agent (leader)
                                {
                                    ColumnData columnData = new ColumnData()
                                    {
                                        leaderName = agentName,
                                        pathNodesNames = carAgent.GetPathNodesNames(),
                                        followAgentName = leavingCarIndex - 2 >= 0 ? columnCarsNames[leavingCarIndex - 2] : "", // Same as before leave
                                        behindAgentName = leavingCarIndex + 1 <= columnCarsNames.Count - 1 ? columnCarsNames[leavingCarIndex + 1] : "" // New
                                    };
                                    string contentDetails = JsonUtility.ToJson(columnData);
                                    string content = Utils.CreateContent(SystemAction.CommunicationAgent_UpdateColumn, contentDetails);
                                    base.SendMessage(Peformative.Inform.ToString(), content, agentName, columnCarsNames[leavingCarIndex - 1]);
                                }
                                else // if car is this agent (leader)
                                {
                                    currentColumnData.behindAgentName = columnCarsNames[leavingCarIndex + 1];
                                }
                            }

                            // Update data in car behind leaving car if any
                            {
                                if (leavingCarIndex != columnCarsNames.Count - 1) // Check if there is car behind
                                {
                                    ColumnData columnData = new ColumnData()
                                    {
                                        leaderName = agentName,
                                        pathNodesNames = carAgent.GetPathNodesNames(),
                                        followAgentName = leavingCarIndex - 1 >= 0 ? columnCarsNames[leavingCarIndex - 1] : "", // New
                                        behindAgentName = leavingCarIndex + 2 <= columnCarsNames.Count - 1 ? columnCarsNames[leavingCarIndex + 2] : "" // Same as before leave
                                    };
                                    string contentDetails = JsonUtility.ToJson(columnData);
                                    string content = Utils.CreateContent(SystemAction.CommunicationAgent_UpdateColumn, contentDetails);
                                    base.SendMessage(Peformative.Inform.ToString(), content, agentName, columnCarsNames[leavingCarIndex + 1]);
                                }      
                            }

                            // Remove leaving car from list
                            columnCarsNames.RemoveAt(leavingCarIndex);
                        }
                    }
                }

                carAgent.SetTarget(carAgent.GetCurrentTargetNodePosition()); // Just follow its path, node by node because for the leader there is no agent in front to follow
                carAgent.SetSpeed(columnSpeed);
            }
            else
            {
                // Receive data updates from the Communication Agent in the car in front and update target position based on it
                if (message != null && receiveContent.action == SystemAction.CommunicationAgent_UpdateCarBehind.ToString())
                {
                    if (message.GetPerformative() == Peformative.Inform.ToString())
                    {
                        ColumnUpdateData columnUpdateData = JsonUtility.FromJson<ColumnUpdateData>(receiveContent.contentDetails);
                        target = columnUpdateData.position;
                        followingAgentTargetNodeName = columnUpdateData.targetNodeName;

                        if (followingAgentTargetNodeName != carAgent.GetCurrentTargetNodeName()) 
                        {
                            carAgent.SetTarget(carAgent.GetCurrentTargetNodePosition()); // Go to node, do not follow agent (without this car will leave path when following agent will be on different edge)
                        }
                        else
                        {
                            Vector3 toTarget = (target.Value - carAgent.GetCarPosition()).normalized;
                            if (Vector3.Dot(toTarget, transform.forward) > 0) // If target is in front (direction to target node) then follow it
                            {
                                carAgent.SetTarget(target.Value); // Follow agent

                                if (Vector3.Distance(carAgent.GetCarPosition(), target.Value) > catchUpColumnDistance)
                                {
                                    carAgent.SetSpeed(catchUpColumnSpeed);
                                }
                                else
                                {
                                    carAgent.SetSpeed(columnSpeed);
                                }
                            }
                            // Slow down and move to current target node waiting to be overtook by column
                            else
                            {
                                carAgent.SetTarget(carAgent.GetCurrentTargetNodePosition());
                                carAgent.SetSpeed(waitForColumnSpeed);
                            }
                            
                        }
                        
                    }
                }

                // Respond to data update sent by leader when car other left then column
                if (message != null && receiveContent.action == SystemAction.CommunicationAgent_UpdateColumn.ToString())
                {
                    if (message.GetPerformative() == Peformative.Inform.ToString())
                    {
                        currentColumnData = JsonUtility.FromJson<ColumnData>(receiveContent.contentDetails);
                        lastCommonColumnNodeName = LastCommonNodeOnPath(); // Recalculate last common node
                    }
                }
                
                // Respond to transfer leadership request
                if (message != null && receiveContent.action == SystemAction.CommunicationAgent_LeaveColumn_TransferLeadership.ToString())
                {
                    if (message.GetPerformative() == Peformative.Request.ToString())
                    {
                        List<string> columnCarsNames = JsonUtility.FromJson<StringList>(receiveContent.contentDetails).list; // Get list of names of cars in column from message
                        
                        // Delete column (since only previous leader and this car are left, and previous leader is leaving)
                        if (columnCarsNames.Count <= 2)
                        {
                            isInColumn = false;
                            currentColumnData = null;
                            columnCarsNames.Clear();
                            lastCommonColumnNodeName = "";
                            state = CommunicationAgentState.ColumnCreateProposal_Wait;
                            return;
                        }
                        // Take over leadership
                        else
                        {
                            this.columnCarsNames = columnCarsNames;
                            this.columnCarsNames.Remove(message.GetSender()); // Remove old leader
                            isColumnLeader = true;

                            // Update self
                            currentColumnData = new ColumnData()
                            {
                                leaderName = agentName,
                                pathNodesNames = carAgent.GetPathNodesNames(),
                                followAgentName = "",
                                behindAgentName = columnCarsNames[1]
                            };

                            // Update data in all CommunicationAgents left
                            for (int i = 1; i < columnCarsNames.Count; i++)
                            {
                                ColumnData columnData = new ColumnData()
                                {
                                    leaderName = agentName,
                                    pathNodesNames = carAgent.GetPathNodesNames(),
                                    followAgentName = columnCarsNames[i - 1],
                                    behindAgentName = (i + 1) <= columnCarsNames.Count - 1 ? columnCarsNames[i + 1] : ""
                                };
                                string contentDetails = JsonUtility.ToJson(columnData);
                                string content = Utils.CreateContent(SystemAction.CommunicationAgent_UpdateColumn, contentDetails);
                                base.SendMessage(Peformative.Inform.ToString(), content, agentName, columnCarsNames[i]);
                            }

                            return;
                        }    
                    }
                }
            }

            // Send data updates to car behind
            if (updateCarBehind_Timer >= updateCarBehind_Timeout)
            {
                if(currentColumnData.behindAgentName != "")
                {
                    Vector3 moveDirection = (carAgent.GetTarget() - carAgent.GetCarPosition()).normalized;

                    ColumnUpdateData columnUpdateData = new ColumnUpdateData()
                    {
                        position = carAgent.GetCarPosition() - moveDirection * betweenCarDistances,
                        targetNodeName = carAgent.GetCurrentTargetNodeName()
                    };
                    string content = Utils.CreateContent(SystemAction.CommunicationAgent_UpdateCarBehind, JsonUtility.ToJson(columnUpdateData));
                    base.SendMessage(Peformative.Inform.ToString(), content, agentName, currentColumnData.behindAgentName);
                    updateCarBehind_Timer = 0;
                }     
            }
            else
            {
                updateCarBehind_Timer += Time.deltaTime;
            }

        }
        // When not in column just follow the calculated path
        else
        {
            try
            {
                carAgent.SetTarget(carAgent.GetCurrentTargetNodePosition()); // Just follow its path, node by node
            }
            catch
            {
                //DebugLog("Test");
            }
        }

        // --- Other ---

        // Update car data in Central Agent
        {
            if (!setupStates.Contains(state)) // Is not in any setup state
            {           
                if (updateCarDataInCentralAgent_Timer >= updateCarDataInCentralAgent_Timeout)
                {
                    UpdateCarDataInCentralAgent();
                    updateCarDataInCentralAgent_Timer = 0;
                }
                else
                {
                    updateCarDataInCentralAgent_Timer += Time.deltaTime;
                }
            }
        }

        // Reach current target
        {
            if (!setupStates.Contains(state)) // Is not in any setup state
            {
                float distanceToCurrentTargetNode = Vector3.Distance(carAgent.GetCarPosition(), carAgent.GetCurrentTargetNodePosition());
                if (distanceToCurrentTargetNode < reachDestinationRadius)
                {
                    if (carAgent.GetCurrentTargetNodeName() == carAgent.GetDestinationNodeName()) // Reaching destination, leave column and end ride
                    {
                        if (state == CommunicationAgentState.MovingInColumn)
                        {
                            // Hand over the leadership to car behind
                            if (isColumnLeader)
                            {
                                StringList stringList = new StringList() { list = columnCarsNames };

                                string contentDetails = JsonUtility.ToJson(stringList);
                                string content = Utils.CreateContent(SystemAction.CommunicationAgent_LeaveColumn_TransferLeadership, contentDetails);
                                base.SendMessage(Peformative.Request.ToString(), content, agentName, currentColumnData.behindAgentName);
                            }
                            // Notify leader about leaving
                            else
                            {
                                string content = Utils.CreateContent(SystemAction.CommunicationAgent_LeaveColumn_NotifyLeader, "");
                                base.SendMessage(Peformative.Inform.ToString(), content, agentName, currentColumnData.leaderName);
                            }
                        }

                        // Deregister agent in CentralAgent
                        {
                            string content = Utils.CreateContent(SystemAction.CommunicationAgent_UnregisterInCentralAgent, "");
                            base.SendMessage(Peformative.Request.ToString(), content, agentName, centralAgentName);
                        }

                        carAgent.EndRide();
                    }
                    // Reaching other node
                    else 
                    {
                        if (carAgent.GetCurrentTargetNodeName() == lastCommonColumnNodeName) // Reaching node last common node with column, leave column and go other side
                        {
                            // Hand over the leadership to car behind
                            if (isColumnLeader)
                            {
                                StringList stringList = new StringList() { list = columnCarsNames };

                                string contentDetails = JsonUtility.ToJson(stringList);
                                string content1 = Utils.CreateContent(SystemAction.CommunicationAgent_LeaveColumn_TransferLeadership, contentDetails);
                                base.SendMessage(Peformative.Request.ToString(), content1, agentName, currentColumnData.behindAgentName);

                                isColumnLeader = false;
                                isInColumn = false;
                                currentColumnData = null;
                                columnCarsNames.Clear();
                                lastCommonColumnNodeName = "";
                            }
                            // Just leave column
                            else
                            {
                                string content = Utils.CreateContent(SystemAction.CommunicationAgent_LeaveColumn_NotifyLeader, "");
                                base.SendMessage(Peformative.Inform.ToString(), content, agentName, currentColumnData.leaderName);

                                isInColumn = false;
                                currentColumnData = null;
                                columnCarsNames.Clear();
                                lastCommonColumnNodeName = "";
                            }
                            
                            state = CommunicationAgentState.ColumnCreateProposal_Wait;

                            return;
                        }
                    }

                }
            }
        }
       
    }

    void UpdateCarDataInCentralAgent()
    {
        CarUpdateData carUpdateData = new CarUpdateData()
        {
            position = carAgent.GetCarPosition(),
            inColumn = isInColumn,
            isColumnLeader = isColumnLeader,
            destinationNodeName = carAgent.GetDestinationNodeName(),
            pathNodeNames = carAgent.GetPathNodesNames(),
            currentTargetNodeName = carAgent.GetCurrentTargetNodeName(),
            columnCarsNames = isColumnLeader ? columnCarsNames : null // Only for leader
        };

        string contentDetails = JsonUtility.ToJson(carUpdateData);
        string content = Utils.CreateContent(SystemAction.CommunicationAgent_UpdateInCentralAgent, contentDetails);
        base.SendMessage(Peformative.Inform.ToString(), content, agentName, centralAgentName);     
    }

    void ConnectToCarAgent(string name, string password)
    {
        carAgent = gameObject.GetComponent<CarAgent>().ConnectCommunicationAgent(this); // Connect to CarAgent API which is located in the car system
        carAgent.ToggleSystemGuidedMode(true);  
    }

    void DebugLog(string message)
    {
        if (Selection.Contains(gameObject))
        {
            Debug.Log(agentName + ": " + message);
        }
    }

    string LastCommonNodeOnPath()
    {
        var path = carAgent.GetPathNodesNames();
        string result = "";
        for (int i = 0; i < path.Count; i++)
        {
            if (currentColumnData.pathNodesNames.Contains(path[i]))
            {
                result = path[i];
            }   
        }

        return result;
    }
}
