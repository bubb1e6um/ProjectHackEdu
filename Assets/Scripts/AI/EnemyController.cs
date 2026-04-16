using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public Transform[] waypoints;
    public float visionDistance = 12f; // 3 grid cells (3 * 4)
    public LayerMask obstacleMask;
    public LayerMask playerMask;
    public Transform eyes;
    [Header("Network Settings")]
    public string enemyIP = "192.168.1.50";
    public string enemyName = "Patrol_Drone";
    public GameObject extractionCube;

    private IEnemyState _currentState;

    /// <summary>Read by EnemyFOV to drive visual state.</summary>
    public bool IsAlert => _currentState is AlertState;

void Start()
    {
        ChangeState(new PatrolState());

        // Network Registration
        var terminal = Object.FindAnyObjectByType<TerminalController>();
        if (terminal != null && terminal.GlobalNetwork != null)
        {
            NetworkNode enemyNode = new NetworkNode(enemyIP, enemyName, true, transform);            
            enemyNode.OnUnlock += OnHacked;
            
            terminal.GlobalNetwork.AddNode(enemyNode);
            
            NetworkNode playerNode = terminal.GlobalNetwork.GetNode("127.0.0.1");
            if (playerNode != null)
            {
                playerNode.ConnectTo(enemyNode);
            }
        }
    }

    private void OnHacked()
    {
        Debug.Log("Enemy hacked! Shutting down...");
        ChangeState(null); 
        this.enabled = false; 
        
        if (extractionCube != null)
        {
            extractionCube.transform.position = transform.position;
            extractionCube.SetActive(true);
        }

        // HIDE THE ENEMY
        gameObject.SetActive(false); 
    }

    void Update()
    {
        _currentState?.UpdateState(this);
        CheckLineOfSight();
    }

    public void ChangeState(IEnemyState newState)
    {
        _currentState?.ExitState(this);
        _currentState = newState;
        _currentState?.EnterState(this);
    }

    private void CheckLineOfSight()
    {
        Vector3 origin = eyes != null ? eyes.position : transform.position; 
        Vector3 direction = transform.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, visionDistance, playerMask | obstacleMask))
        {
            if (((1 << hit.collider.gameObject.layer) & playerMask) != 0)
            {
                if (!(_currentState is AlertState))
                {
                    ChangeState(new AlertState());
                }
            }
        }
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 origin = eyes != null ? eyes.position : transform.position;
        Gizmos.DrawRay(origin, transform.forward * visionDistance);
    }
}
