using System.Collections.Generic;
using UnityEngine;

// fake pacstudent for training. goes for the nearest pellet and runs away when a ghost gets close
public class PacBot : MonoBehaviour
{
    [SerializeField] private TrainingArea area;
    [SerializeField] private float tilesPerSecond = 5f;
    [SerializeField] private bool randomisePerEpisode = true;
    [SerializeField] [Range(0f, 1f)] private float smartness = 0.9f;   // how often it runs away
    [SerializeField] private int fleeRange = 8;                        // how close a ghost has to be

    private Vector2Int cell;
    private Vector2Int nextCell;
    private Vector2Int heading = Vector2Int.zero;
    private Vector2Int target;

    private bool placed;
    private bool moving;
    private bool hasTarget;
    private float progress;

    public Vector2Int Heading { get { return heading; } }

    public void PlaceAt(Vector2Int c)
    {
        cell = c;
        nextCell = c;
        heading = Vector2Int.zero;
        progress = 0f;
        moving = false;
        hasTarget = false;
        placed = true;
        transform.position = new Vector3(c.x, c.y, 0f);

        // a different kind of player every episode so the ghosts dont just learn one bot
        if (randomisePerEpisode)
        {
            smartness = Random.Range(0.3f, 1f);
            fleeRange = Random.Range(5, 11);
        }
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
            area.EatPellet(cell);
            ChooseMove();
        }

        transform.position = Vector3.Lerp(new Vector3(cell.x, cell.y, 0f),
                                          new Vector3(nextCell.x, nextCell.y, 0f), progress);
    }

    void ChooseMove()
    {
        bool[] open = MazeUtil.OpenDirections(cell, heading);

        int[,] fromGhosts = MazeUtil.Distances(area.GhostCells());
        int ghostDist = MazeUtil.Dist(fromGhosts, cell);

        int[,] scoreMap = null;
        bool wantHigh = false;

        if (ghostDist <= fleeRange && Random.value < smartness)
        {
            // a ghost is close so run away from the nearest one
            scoreMap = fromGhosts;
            wantHigh = true;
        }
        else
        {
            // otherwise go for the nearest pellet and stick with it until its eaten
            if (!hasTarget || !area.HasPellet(target))
            {
                int[,] fromMe = MazeUtil.Distances(cell);
                hasTarget = area.NearestPellet(fromMe, out target);
            }

            if (hasTarget)
            {
                scoreMap = MazeUtil.Distances(target);
            }
        }

        // pick the best open direction, random if its a tie (or no goal at all)
        List<int> best = new List<int>();
        int bestScore = 0;

        for (int i = 0; i < 4; i++)
        {
            if (!open[i]) continue;

            int s = 0;
            if (scoreMap != null) s = MazeUtil.Dist(scoreMap, cell + MazeUtil.Dirs[i]);

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
