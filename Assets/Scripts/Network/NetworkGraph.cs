using System.Collections.Generic;

public class NetworkGraph
{
    private Dictionary<string, NetworkNode> _nodes = new Dictionary<string, NetworkNode>();

    public void AddNode(NetworkNode node)
    {
        _nodes[node.IP] = node;
    }

    public NetworkNode GetNode(string ip)
    {
        return _nodes.ContainsKey(ip) ? _nodes[ip] : null;
    }
    public IEnumerable<NetworkNode> GetAllNodes()
    {
        return _nodes.Values;
    }
}