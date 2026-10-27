using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

// which brain to use when Behavior Type is Heuristic Only (for testing and comparing)
public enum TestBrain { Greedy = 1, Ghost2 = 2 }   // numbers kept so saved scene settings still match

public class GhostAgent : Agent
{
    [SerializeField] private TrainingArea area;
    [SerializeField] private float tilesPerSecond = 4.5f;   // 90% of pacstudent
    [SerializeField] private TestBrain testBrain = TestBrain.Greedy;
    [SerializeField] private bool avoidSameChoice = false;   // the same tie-break rule as in the real game

    private Vector2Int cell;
    private Vector2Int nextCell;
    private Vector2Int lastDir = Vector2Int.zero;

    private bool placed;
    private bool moving;
    private bool waiting;   // waiting for the brain to pick a direction
    private bool ended;     // episode is over, waiting for the reset
    private float progress;

    private int[,] toPac;

    // called by the training area at the start of every episode
    public void PlaceAt(Vector2Int c, Vector2Int dir)
    {
        cell = c;
        nextCell = c;
        lastDir = dir;
        progress = 0f;
        moving = false;
        waiting = false;
        ended = false;
        placed = true;
        transform.position = new Vector3(c.x, c.y, 0f);
    }

    public override void OnEpisodeBegin()
    {
        area.ResetEpisode();
    }

    void Update()
    {
        if (!placed || ended || waiting) return;

        if (!moving)
        {
            AskForDecision();
            return;
        }

        progress += tilesPerSecond * Time.deltaTime;

        if (progress >= 1f)
        {
            progress -= 1f;
            cell = nextCell;
            moving = false;
            transform.position = new Vector3(cell.x, cell.y, 0f);
        }
        else
        {
            transform.position = Vector3.Lerp(new Vector3(cell.x, cell.y, 0f),
                                              new Vector3(nextCell.x, nextCell.y, 0f), progress);
        }

        // did we catch pacstudent?
        if (Vector3.Distance(transform.position, area.PacPosition) < 0.6f)
        {
            area.TeamCaught();
        }
        else if (!moving)
        {
            AskForDecision();
        }
    }

    void AskForDecision()
    {
        waiting = true;
        RequestDecision();
    }

    // stops this ghost moving until the area has reset everyone
    public void Freeze()
    {
        ended = true;
    }

    // the whole team caught pacstudent
    public void EndWithReward(float reward)
    {
        AddReward(reward);
        EndEpisode();
    }

    // ran out of time, not the ghosts fault so it isnt a normal episode end
    public void TimeOut()
    {
        EpisodeInterrupted();
    }

    // ----- what the brain sees -----

    public override void CollectObservations(VectorSensor sensor)
    {
        toPac = MazeUtil.Distances(area.PacCell);
        MazeUtil.Observe(sensor, cell, lastDir, area.PacCell, area.PacHeading, toPac,
                         area.MateCells(this), area.Pellets);
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        MazeUtil.MaskInvalid(actionMask, cell, lastDir);
    }

    // ----- what the brain does -----

    public override void OnActionReceived(ActionBuffers actions)
    {
        Vector2Int dir = MazeUtil.SafeDirection(cell, lastDir, actions.DiscreteActions[0]);

        if (toPac == null) toPac = MazeUtil.Distances(area.PacCell);

        // if another ghost on this tile already picked this way, take the next best way
        if (avoidSameChoice) dir = AvoidSameChoice(dir);

        // small reward for getting closer, small penalty for getting further
        int before = MazeUtil.Dist(toPac, cell);
        int after = MazeUtil.Dist(toPac, cell + dir);
        AddReward(0.01f * (before - after));

        // tiny penalty every move so it hurries up
        AddReward(-0.003f);

        lastDir = dir;
        nextCell = cell + dir;
        moving = true;
        waiting = false;
    }

    // if another ghost on this tile already picked the same way, take the next best way instead
    Vector2Int AvoidSameChoice(Vector2Int choice)
    {
        List<Vector2Int> claimed = new List<Vector2Int>();

        foreach (GhostAgent other in area.Ghosts)
        {
            if (other == this) continue;

            if (other.moving && other.cell == cell && other.nextCell != other.cell)
            {
                claimed.Add(other.nextCell - other.cell);
            }
        }

        if (!claimed.Contains(choice)) return choice;

        // closest to pacstudent out of the open ways nobody else took
        bool[] open = MazeUtil.OpenDirections(cell, lastDir);
        Vector2Int best = choice;
        int bestScore = int.MaxValue;

        for (int i = 0; i < 4; i++)
        {
            if (!open[i] || claimed.Contains(MazeUtil.Dirs[i])) continue;

            int score = MazeUtil.Dist(toPac, cell + MazeUtil.Dirs[i]);
            if (score < bestScore)
            {
                bestScore = score;
                best = MazeUtil.Dirs[i];
            }
        }

        return best;
    }

    // simple brains to test with and to compare the trained one against
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        bool[] open = MazeUtil.OpenDirections(cell, lastDir);

        List<int> options = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            if (open[i]) options.Add(i);
        }

        int pick = 0;

        if (options.Count > 0)
        {
            pick = options[Random.Range(0, options.Count)];

            if (testBrain == TestBrain.Greedy)
            {
                // always the move that gets closest by the maze
                int[,] dist = MazeUtil.Distances(area.PacCell);
                List<int> best = new List<int>();
                int bestScore = int.MaxValue;

                foreach (int i in options)
                {
                    int s = MazeUtil.Dist(dist, cell + MazeUtil.Dirs[i]);
                    if (s < bestScore) { bestScore = s; best.Clear(); best.Add(i); }
                    else if (s == bestScore) best.Add(i);
                }

                pick = best[Random.Range(0, best.Count)];
            }
            else
            {
                // like ghost 2 in level 1: random move that doesnt get further away in a straight line
                float now = Vector2.Distance(cell, area.PacPosition);
                List<int> good = new List<int>();

                foreach (int i in options)
                {
                    if (Vector2.Distance(cell + MazeUtil.Dirs[i], area.PacPosition) <= now) good.Add(i);
                }

                if (good.Count > 0) pick = good[Random.Range(0, good.Count)];
            }
        }

        ActionSegment<int> discrete = actionsOut.DiscreteActions;
        discrete[0] = pick;
    }
}
