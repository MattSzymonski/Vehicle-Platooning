using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AgentPlatform : MonoBehaviour
{
    public Dictionary<string, Agent> registeredAgents;

    void Start()
    {
        registeredAgents = new Dictionary<string, Agent>();
    }

    public (bool, AgentPlatform) RegisterAgent(string agentName, Agent agent)
    {
        registeredAgents.Add(agentName, agent);
        return (true, this);
    }
   
    void Update()
    {
        
    }

    public bool ForwardMessage(Message message)
    {
        // Find receiver and forward message to it
        var receiver = registeredAgents[message.GetReceiver()];
        receiver.EnqueueMessage(message);
        return true;
    }
}
