using System;
using System.Collections.Generic;
using UnityEngine;

public class NetworkNode
{
    public string IP { get; }
    public string DeviceName { get; }
    public bool IsLocked { get; set; }
    
    public Transform PhysicalTransform { get; }

    public List<NetworkNode> Connections { get; } = new List<NetworkNode>();
    public Action OnUnlock;

    public NetworkNode(string ip, string name, bool isLocked = false, Transform transform = null)
    {
        IP = ip;
        DeviceName = name;
        IsLocked = isLocked;
        PhysicalTransform = transform;
    }

    public void ConnectTo(NetworkNode other)
    {
        if (!Connections.Contains(other)) Connections.Add(other);
        if (!other.Connections.Contains(this)) other.Connections.Add(this);
    }
}