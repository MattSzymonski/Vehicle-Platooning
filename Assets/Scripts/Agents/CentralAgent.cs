using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class CentralAgent : Agent
{
    public Dictionary<string, CarUpdateData> communicationAgents;
    public float columnJoinRadius = 2;

    void Start()
    {
        name = agentName;
        communicationAgents = new Dictionary<string, CarUpdateData>();
        RegisterInAgentPlatform();           
    }

    void Update()
    {
        Message message = base.ReceiveMessage();
        Content receiveContent = message != null ? JsonUtility.FromJson<Content>(message.GetContent()) : null;

        // Process register requests
        if (message != null && receiveContent.action == SystemAction.CommunicationAgent_RegisterInCentralAgent.ToString())
        {    
            if (message.GetPerformative() == Peformative.Request.ToString())
            {
                // Add new agent
                communicationAgents.Add(message.GetSender(), null);

                // Send message
                string content = Utils.CreateContent(SystemAction.CommunicationAgent_RegisterInCentralAgent, "");
                base.SendMessage(Peformative.Accept.ToString(), content, agentName, message.GetSender());

                return;      
            }
        }

        // Process unregister requests
        if (message != null && receiveContent.action == SystemAction.CommunicationAgent_UnregisterInCentralAgent.ToString())
        {
            if (message.GetPerformative() == Peformative.Request.ToString())
            {
                // Remove agent
                communicationAgents.Remove(message.GetSender());

                return;
            }
        }

        // Process CommunicationAgent updates
        if (message != null && receiveContent.action == SystemAction.CommunicationAgent_UpdateInCentralAgent.ToString())
        {      
            if (message.GetPerformative() == Peformative.Inform.ToString())
            {
                CarUpdateData carUpdateData = JsonUtility.FromJson<CarUpdateData>(receiveContent.contentDetails);
                communicationAgents[message.GetSender()] = carUpdateData; // Update data in dictionary

                return;
            }
        }

        // Process nearby cars (column and lonely cars) query
        if (message != null && receiveContent.action == SystemAction.CommunicationAgent_NearbyCars.ToString())
        {       
            if (message.GetPerformative() == Peformative.Query.ToString())
            {
                string sender = message.GetSender();
                //string destination = receiveContent.contentDetails;

                // Get cars nearby which are in certain radius
                List<string> agents = communicationAgents.Keys.Where(x => (
                    communicationAgents[x] != null &&
                    x != sender && 
                    //communicationAgents[x].pathNodeNames.Contains(destination) && 
                    Vector3.Distance(communicationAgents[sender].position, communicationAgents[x].position) < columnJoinRadius)
                ).ToList();

                // List of names of agents (which are leaders), their paths, current target node and distance to sender
                List<CarDataBasic> columnLeaderCommunicationAgents = agents.Where(x => communicationAgents[x].isColumnLeader == true).Select(x => new CarDataBasic
                {
                    name = x,
                    pathNodesNames = communicationAgents[x].pathNodeNames,
                    currentTargetNodeName = communicationAgents[x].currentTargetNodeName,
                    distance = Vector3.Distance(communicationAgents[x].position, communicationAgents[sender].position)
                }).ToList();

                // List of names of agents (which are lonely), their paths, current target node and distance to sender
                List<CarDataBasic> lonelyCommunicationAgents = agents.Where(x => communicationAgents[x].inColumn == false).Select(x => new CarDataBasic
                {
                    name = x,
                    pathNodesNames = communicationAgents[x].pathNodeNames,
                    currentTargetNodeName = communicationAgents[x].currentTargetNodeName,
                    distance = Vector3.Distance(communicationAgents[x].position, communicationAgents[sender].position)
                }).ToList();

                ColumnQueryData columnQueryData = new ColumnQueryData()
                {
                    columnLeaderCommunicationAgents = columnLeaderCommunicationAgents,
                    lonelyCommunicationAgents = lonelyCommunicationAgents
                };

                // Send message
                string content = Utils.CreateContent(SystemAction.CommunicationAgent_NearbyCars, JsonUtility.ToJson(columnQueryData));
                base.SendMessage(Peformative.Inform.ToString(), content, agentName, message.GetSender());

                return;
            }
        }

    }
    
}
