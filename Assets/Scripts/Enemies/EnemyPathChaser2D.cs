using System.Collections.Generic;
using UnityEngine;

public class EnemyPathChaser2D : MonoBehaviour
{
    [Header("Refs")]
    public Path path;                          // Path courant
    public Rigidbody2D rb;                     // Rigidbody de l’ennemi
    public TriggerPoint triggerPoint;          // TriggerPoint sur l’ennemi

    [Header("Move")]
    public float moveSpeed = 4f;
    public float arriveDistance = 0.12f;

    [Header("Behavior")]
    public bool stopWhenTouchPlayer = true;
    public string playerTag = "Player";

    private bool _touchedPlayer = false;

    public MaskItem maskItem;

    private void Start()
    {
        path = PathManager.instance.GetPath(maskItem);
    }

    private void FixedUpdate()
    {
        if (path == null || path.points == null || path.points.Count < 2) return;
        if (stopWhenTouchPlayer && _touchedPlayer) return;

        Point myPoint = triggerPoint.currentPoint;
        Point playerPoint = PlayerMovement.instance.triggerPoint.currentPoint;

        // Indexs sur le path
        int myIdx = GetIndexOrClosest(path.points, myPoint, rb.position);
        int playerIdx = GetIndexOrClosest(
            path.points,
            playerPoint,
            PlayerMovement.instance.triggerPoint.transform.position
        );

        if (myIdx < 0 || playerIdx < 0) return;

        int dir;
        if (playerIdx > myIdx) dir = +1;
        else if (playerIdx < myIdx) dir = -1;
        else
        {
            float dx = PlayerMovement.instance.triggerPoint.transform.position.x - rb.position.x;
            dir = (dx >= 0f) ? +1 : -1;
        }

        int nextIdx = GetNextWalkableIndex(path.points, myIdx, dir);
        if (nextIdx < 0) return;

        Vector2 goal = path.points[nextIdx].transform.position;

        Vector2 pos = rb.position;
        Vector2 to = goal - pos;
        float dist = to.magnitude;

        if (dist <= arriveDistance)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float step = moveSpeed * Time.fixedDeltaTime;
        Vector2 newPos = Vector2.MoveTowards(pos, goal, step);
        rb.MovePosition(newPos);
    }

    private int GetIndexOrClosest(List<Point> pts, Point p, Vector2 fallbackPos)
    {
        if (p != null)
        {
            int idx = pts.IndexOf(p);
            if (idx >= 0) return idx;
        }

        int best = -1;
        float bestD2 = float.PositiveInfinity;

        for (int i = 0; i < pts.Count; i++)
        {
            if (pts[i] == null) continue;
            if (!pts[i].walkable) continue;

            float d2 = ((Vector2)pts[i].transform.position - fallbackPos).sqrMagnitude;
            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = i;
            }
        }

        return best;
    }

    private int GetNextWalkableIndex(List<Point> pts, int from, int dir)
    {
        int i = from + dir;

        while (i >= 0 && i < pts.Count)
        {
            Point p = pts[i];
            if (p != null && p.walkable)
                return i;

            i += dir;
        }

        return -1;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!stopWhenTouchPlayer) return;

        if (other.CompareTag(playerTag))
            _touchedPlayer = true;
    }
}
