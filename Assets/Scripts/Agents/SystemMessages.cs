using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Content
{
    public string action;
    public string contentDetails;
}

[Serializable]
public class CarUpdateData // For updating data in centralAgent (communicationAgent-centralAgent)
{
    public Vector3 position;
    public bool inColumn;
    public bool isColumnLeader;
    public string destinationNodeName;
    public List<string> pathNodeNames;
    public string currentTargetNodeName;
    public List<string> columnCarsNames; // Only for leader
}

[Serializable]
public class ColumnQueryData // For asking about nearby columns and lonely cars (communicationAgent-centralAgent)
{
    public List<CarDataBasic> columnLeaderCommunicationAgents;
    public List<CarDataBasic> lonelyCommunicationAgents;
}

[Serializable]
public class CarDataBasic
{
    public string name;
    public List<string> pathNodesNames;
    public string currentTargetNodeName;
    public float distance;
}

[Serializable]
public class ColumnData // For creating new column (send by leader to new member when it joins column or when leader is changing) (communicationAgent-communicationAgent)
{
    public string leaderName;
    public List<string> pathNodesNames;
    public string followAgentName; // Name of agent to follow
    public string behindAgentName; // Name of agent behind
}

[Serializable]
public class ColumnCreateData // For creating new column (send by leader to possible member as invitation) (communicationAgent-communicationAgent)
{
    public string leaderName;
    public List<string> pathNodesNames;
    public string currentTargetNodeName;
}

[Serializable]
public class StringList // For passing columnCarsNames when handing over the leadership (communicationAgent-communicationAgent)
{
    public List<string> list;
}

[Serializable]
public class ColumnUpdateData // For intercolumn communication (communicationAgent-communicationAgent)
{
    public Vector3 position;
    public string targetNodeName;
}




