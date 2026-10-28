using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

// the trained brain for a ghost in the real game. the ghost controller asks it which way to go
public class GhostBrain : Agent
{
    // every ghost with a brain, so they can see each other
    private static readonly List<GhostBrain> all = new List<GhostBrain>();

    private PacStudentController pac;
    private Vector2Int cell;
    private Vector2Int lastDir;

    public bool HasAnswer { get; private set; }
    public Vector2Int Choice { get; private set; }

    // called by the ghost controller at the start of every tile
    public void Ask(Vector2Int ghostCell, Vector2Int ghostLastDir)
    {
        if (pac == null) pac = FindAnyObjectByType<PacStudentController>();
        if (!all.Contains(this)) all.Add(this);

        cell = ghostCell;
        lastDir = ghostLastDir;
        HasAnswer = false;
        RequestDecision();
    }

    // has to be exactly the same numbers it saw in training
    public override void CollectObservations(VectorSensor sensor)
    {
        int[,] toPac = MazeUtil.Distances(pac.Cell);

        List<Vector2Int> mates = new List<Vector2Int>();
        for (int i = all.Count - 1; i >= 0; i--)
        {
            if (all[i] == null) { all.RemoveAt(i); continue; }
            if (all[i] != this) mates.Add(Vector2Int.RoundToInt(all[i].transform.position));
        }

        MazeUtil.Observe(sensor, cell, lastDir, pac.Cell, pac.Heading, toPac,
                         mates, MazeUtil.PelletsFromGrid());
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        MazeUtil.MaskInvalid(actionMask, cell, lastDir);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        Choice = MazeUtil.SafeDirection(cell, lastDir, actions.DiscreteActions[0]);
        HasAnswer = true;
    }
}
