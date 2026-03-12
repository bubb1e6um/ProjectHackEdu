using UnityEngine;

public class ExtractionPoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering is the Player (checking for GridMovement script)
        if (other.GetComponent<GridMovement>() != null)
        {
            var terminal = Object.FindAnyObjectByType<TerminalController>();
            if (terminal != null)
            {
                terminal.TriggerVictory();
            }
        }
    }
}