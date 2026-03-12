using UnityEngine;

public class PatrolState : IEnemyState
{
    private int _currentWaypointIndex = 0;
    private float _reachThreshold = 0.1f;

    public void EnterState(EnemyController enemy)
    {
        if (enemy.waypoints.Length == 0) return;
        enemy.transform.LookAt(enemy.waypoints[_currentWaypointIndex]);
    }

    public void UpdateState(EnemyController enemy)
    {
        if (enemy.waypoints.Length == 0) return;

        Transform target = enemy.waypoints[_currentWaypointIndex];
        Vector3 dir = (target.position - enemy.transform.position).normalized;
        
        enemy.transform.position = Vector3.MoveTowards(
            enemy.transform.position, 
            target.position, 
            enemy.moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(enemy.transform.position, target.position) < _reachThreshold)
        {
            _currentWaypointIndex = (_currentWaypointIndex + 1) % enemy.waypoints.Length;
            enemy.transform.LookAt(enemy.waypoints[_currentWaypointIndex]);
        }
    }

    public void ExitState(EnemyController enemy)
    {
    }
}

public class AlertState : IEnemyState
{
    public void EnterState(EnemyController enemy)
    {
        var terminal = Object.FindAnyObjectByType<TerminalController>();
        if (terminal != null)
        {
            terminal.TriggerGameOver();
        }
        else
        {
            Debug.LogError("TerminalController not found in scene!");
        }
    }

    public void UpdateState(EnemyController enemy) { }
    public void ExitState(EnemyController enemy) { }
}

