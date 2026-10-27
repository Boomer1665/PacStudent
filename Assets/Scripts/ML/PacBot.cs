using System.Collections.Generic;
using UnityEngine;

// fake pacstudent for training. walks towards random spots and sometimes runs from the ghost
public class PacBot : MonoBehaviour
{
    [SerializeField] private TrainingArea area;
    [SerializeField] private float tilesPerSecond = 5f;
    [SerializeField] [Range(0f, 1f)] private float smartness = 0.9f;   // how often it runs away
    [SerializeField] private int fleeRange = 8;                        // how close the ghost has to be

    private Vector2Int cell;
    private Vector2Int nextCell;
    private Vector2Int heading = Vector2Int.zero;
    private Vector2Int goal;

    private bool placed;
    private bool moving;
    private bool hasGoal;
    private float progress;

    public Vector2Int Heading { get { return heading; } }

    public void PlaceAt(Vector2Int c)
    {
        cell = c;
        nextCell = c;
        heading = Vector2Int.zero;
        progress = 0f;
        moving = false;
        hasGoal = false;
        placed = true;
        transform.position = new Vector3(c.x, c.y, 0f);
    }

    void Update()
    {
        if (!placed) return;

        if (!moving) ChooseMove();

        progress += tilesPerSecond * Time.deltaTime;

        // keep the leftover time so the speed stays right on slow frames
        while (progress >= 1f)
        {
            progress -= 1f;
            cell = nextCell;
            moving = false;
            ChooseMove();
        }

        transform.position = Vector3.Lerp(new Vector3(cell.x, cell.y, 0f),
                                          new Vector3(nextCell.x, nextCell.y, 0f), progress);
    }

    void ChooseMove()
    {
        bool[] open = MazeUtil.OpenDirections(cell, heading);

        int[,] fromGhost = MazeUtil.Distances(area.GhostCell);
        int ghostDist = MazeUtil.Dist(fromGhost, cell);

        int[,] scoreMap;
        bool wantHigh;

        if (ghostDist <= fleeRange && Random.value < smartness)
        {
            // ghost is close so run away from it
            scoreMap = fromGhost;
            wantHigh = true;
        }
        else
        {
            // otherwise head for a random spot on the map
            if (!hasGoal || cell == goal)
            {
                goal = area.RandomCell();
                hasGoal = true;
            }
            scoreMap = MazeUtil.Distances(goal);
            wantHigh = false;
        }

        // pick the best open direction, random if its a tie
        List<int> best = new List<int>();
        int bestScore = 0;

        for (int i = 0; i < 4; i++)
        {
            if (!open[i]) continue;

            int s = MazeUtil.Dist(scoreMap, cell + MazeUtil.Dirs[i]);

            if (best.Count == 0 || (wantHigh && s > bestScore) || (!wantHigh && s < bestScore))
            {
                best.Clear();
                best.Add(i);
                bestScore = s;
            }
            else if (s == bestScore)
            {
                best.Add(i);
            }
        }

        if (best.Count == 0) return;

        int pick = best[Random.Range(0, best.Count)];
        heading = MazeUtil.Dirs[pick];
        nextCell = cell + heading;
        moving = true;
    }
}
