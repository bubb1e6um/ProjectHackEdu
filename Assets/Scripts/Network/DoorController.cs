using UnityEngine;

public class DoorController : MonoBehaviour
{
    public string doorIP = "192.168.1.10";
    public string doorName = "Sector_A_Gate";
    
    private void Start()
    {
        var terminal = Object.FindAnyObjectByType<TerminalController>();
        if (terminal == null) return; 

        NetworkNode doorNode = new NetworkNode(doorIP, doorName, true, transform);
        doorNode.OnUnlock += OpenDoor;
        
        terminal.GlobalNetwork.AddNode(doorNode);
        
        NetworkNode playerNode = terminal.GlobalNetwork.GetNode("127.0.0.1");
        if (playerNode != null)
        {
            playerNode.ConnectTo(doorNode);
        }
    }

    private void OpenDoor()
    {
        GetComponent<Collider>().enabled = false;
        gameObject.SetActive(false); 
    }
}