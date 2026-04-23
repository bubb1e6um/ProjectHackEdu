using UnityEngine;

public class PatrolState : IEnemyState
{
    private int   _currentWaypointIndex;
    private float _reachThreshold = 0.1f;

    public PatrolState(int startIndex = 0) => _currentWaypointIndex = startIndex;

    public void EnterState(EnemyController enemy)
    {
        if (enemy.waypoints.Length == 0) return;
        enemy.transform.LookAt(enemy.waypoints[_currentWaypointIndex]);
    }

    public void UpdateState(EnemyController enemy)
    {
        if (enemy.CanSeePlayer)
        {
            enemy.ChangeState(new DetectedState(enemy.detectionTime, _currentWaypointIndex));
            return;
        }

        if (enemy.waypoints.Length == 0) return;

        Transform target = enemy.waypoints[_currentWaypointIndex];
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

    public void ExitState(EnemyController enemy) { }
}

public class DetectedState : IEnemyState
{
    readonly float         _duration;
    readonly int           _resumeWaypoint;
    float                  _elapsed;
    ScanBarController      _bar;

    public DetectedState(float duration, int resumeWaypoint)
    {
        _duration       = duration;
        _resumeWaypoint = resumeWaypoint;
    }

    public void EnterState(EnemyController enemy)
    {
        _elapsed = 0f;
        _bar     = ScanBarController.Create(enemy.transform);
        _bar.SetProgress(0f);
    }

    public void UpdateState(EnemyController enemy)
    {
        if (!enemy.CanSeePlayer)
        {
            enemy.ChangeState(new PatrolState(_resumeWaypoint));
            return;
        }

        _elapsed += Time.deltaTime;
        _bar.SetProgress(_elapsed / _duration);

        if (_elapsed >= _duration)
            enemy.ChangeState(new AlertState());
    }

    public void ExitState(EnemyController enemy)
    {
        if (_bar != null)
        {
            Object.Destroy(_bar.gameObject);
            _bar = null;
        }
    }
}

public class AlertState : IEnemyState
{
    public void EnterState(EnemyController enemy)
    {
        var terminal = Object.FindAnyObjectByType<TerminalController>();
        if (terminal != null)
            terminal.TriggerGameOver();
        else
            Debug.LogError("TerminalController not found in scene!");
    }

    public void UpdateState(EnemyController enemy) { }
    public void ExitState(EnemyController enemy)   { }
}

