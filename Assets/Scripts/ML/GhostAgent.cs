using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class GhostAgent : Agent
{
    [SerializeField] private TrainingArea area;
    [SerializeField] private float tilesPerSecond = 4.5f;   // 90% of pacstudent

    private Vector2Int cell;
    private Vector2Int nextCell;
    private Vector2Int lastDir = Vector2Int.zero;

    private bool placed;
    private bool moving;
    private bool waiting;   // waiting for the brain to pick a direction
    private bool ended;     // episode is over, waiting for the reset
    private float progress;

    private int[,] toPac;
    private int wish = -1;   // last wasd key pressed (only used for testing with the keyboard)

    public Vector2Int Cell { get { return cell; } }

    // called by the training area at the start of every episode
    public void PlaceAt(Vector2Int c)
    {
        cell = c;
        nextCell = c;
        lastDir = Vector2Int.zero;
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
        // remember the last key, so a quick tap still counts when the ghost picks its next direction
        if (Input.GetKeyDown(KeyCode.W)) wish = 0;
        if (Input.GetKeyDown(KeyCode.S)) wish = 1;
        if (Input.GetKeyDown(KeyCode.A)) wish = 2;
        if (Input.GetKeyDown(KeyCode.D)) wish = 3;

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
            Caught();
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

    void Caught()
    {
        if (ended) return;
        ended = true;
        AddReward(1f);
        EndEpisode();
    }

    // ran out of time, not the ghosts fault so it isnt a normal episode end
    public void TimeOut()
    {
        if (ended) return;
        ended = true;
        EpisodeInterrupted();
    }

    // ----- what the brain sees -----

    public override void CollectObservations(VectorSensor sensor)
    {
        toPac = MazeUtil.Distances(area.PacCell);
        MazeUtil.Observe(sensor, cell, lastDir, area.PacCell, area.PacHeading, toPac);
    }

    // stops the brain picking walls (or going backwards)
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        bool[] open = MazeUtil.OpenDirections(cell, lastDir);

        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            if (open[i]) count++;
        }
        if (count == 0) return;

        for (int i = 0; i < 4; i++)
        {
            if (!open[i]) actionMask.SetActionEnabled(0, i, false);
        }
    }

    // ----- what the brain does -----

    public override void OnActionReceived(ActionBuffers actions)
    {
        Vector2Int dir = MazeUtil.Dirs[actions.DiscreteActions[0]];

        // just in case it somehow picked a wall
        if (!MazeUtil.CanWalk(cell + dir))
        {
            bool[] open = MazeUtil.OpenDirections(cell, lastDir);
            for (int i = 0; i < 4; i++)
            {
                if (open[i]) { dir = MazeUtil.Dirs[i]; break; }
            }
        }

        if (toPac == null) toPac = MazeUtil.Distances(area.PacCell);

        // small reward for getting closer, small penalty for getting further
        int before = MazeUtil.Dist(toPac, cell);
        int after = MazeUtil.Dist(toPac, cell + dir);
        AddReward(0.02f * (before - after));

        // tiny penalty every move so it hurries up
        AddReward(-0.005f);

        lastDir = dir;
        nextCell = cell + dir;
        moving = true;
        waiting = false;
    }

    // lets you drive the ghost with wasd to test (Behavior Type = Heuristic Only)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        bool[] open = MazeUtil.OpenDirections(cell, lastDir);
        int pick = wish;

        // if that way is blocked keep going straight, or just take the first open way
        if (pick < 0 || !open[pick])
        {
            pick = -1;
            for (int i = 0; i < 4; i++)
            {
                if (open[i] && MazeUtil.Dirs[i] == lastDir) pick = i;
            }
            if (pick < 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (open[i]) { pick = i; break; }
                }
            }
        }

        if (pick < 0) pick = 0;

        ActionSegment<int> discrete = actionsOut.DiscreteActions;
        discrete[0] = pick;
    }
}
