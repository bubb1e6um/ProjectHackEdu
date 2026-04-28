using UnityEngine;

public class ExtractionPoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
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