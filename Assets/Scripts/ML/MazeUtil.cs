using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents.Sensors;

// helper stuff for the ml ghost: maze distances, which directions are open, and the observations
public static class MazeUtil
{
    public static readonly Vector2Int[] Dirs =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    public const int Width = 28;
    public const int Height = 29;
    public const int FarAway = 60;

    // same tiles pacstudent walks on but ghosts cant use the teleporter tiles
    public static bool CanWalk(Vector2Int c)
    {
        return LevelGrid.IsWalkable(c) && c.x > LevelGrid.MinX && c.x < LevelGrid.MaxX;
    }

    // how many steps it takes to get from start to every tile (-1 means you cant get there)
    public static int[,] Distances(Vector2Int start)
    {
        int[,] dist = new int[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                dist[x, y] = -1;
            }
        }

        if (!CanWalk(start)) return dist;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        dist[start.x - LevelGrid.MinX, start.y - LevelGrid.MinY] = 0;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            int d = dist[c.x - LevelGrid.MinX, c.y - LevelGrid.MinY];

            foreach (Vector2Int dir in Dirs)
            {
                Vector2Int n = c + dir;
                if (!CanWalk(n)) continue;
                if (dist[n.x - LevelGrid.MinX, n.y - LevelGrid.MinY] != -1) continue;

                dist[n.x - LevelGrid.MinX, n.y - LevelGrid.MinY] = d + 1;
                queue.Enqueue(n);
            }
        }

        return dist;
    }

    // look up a distance, FarAway if its a wall or not reachable
    public static int Dist(int[,] map, Vector2Int c)
    {
        if (!LevelGrid.InBounds(c)) return FarAway;

        int v = map[c.x - LevelGrid.MinX, c.y - LevelGrid.MinY];
        if (v < 0) return FarAway;
        return v;
    }

    // every tile you can reach from the top left corner
    public static List<Vector2Int> ReachableCells()
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        int[,] dist = Distances(new Vector2Int(-7, 1));

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (dist[x, y] >= 0) cells.Add(new Vector2Int(x + LevelGrid.MinX, y + LevelGrid.MinY));
            }
        }

        return cells;
    }

    // directions that are walkable and not straight back (back is only allowed at a dead end)
    public static bool[] OpenDirections(Vector2Int cell, Vector2Int lastDir)
    {
        bool[] open = new bool[4];
        bool any = false;

        for (int i = 0; i < 4; i++)
        {
            open[i] = CanWalk(cell + Dirs[i]) && Dirs[i] != -lastDir;
            if (open[i]) any = true;
        }

        if (!any)
        {
            for (int i = 0; i < 4; i++)
            {
                open[i] = CanWalk(cell + Dirs[i]);
            }
        }

        return open;
    }

    // a few tiles ahead of pacstudent, stops at the first wall
    public static Vector2Int LeadCell(Vector2Int pac, Vector2Int heading, int tiles)
    {
        Vector2Int c = pac;

        for (int i = 0; i < tiles; i++)
        {
            if (heading == Vector2Int.zero) break;
            if (!CanWalk(c + heading)) break;
            c += heading;
        }

        return c;
    }

    // 16 numbers the ghost gets to look at each time it has to pick a direction
    public const int ObservationSize = 16;

    public static void Observe(VectorSensor sensor, Vector2Int ghost, Vector2Int lastDir,
                               Vector2Int pac, Vector2Int pacHeading, int[,] toPac)
    {
        // where pacstudent is compared to the ghost (2)
        sensor.AddObservation((pac.x - ghost.x) / (float)Width);
        sensor.AddObservation((pac.y - ghost.y) / (float)Height);

        // which way pacstudent is going (2)
        sensor.AddObservation((float)pacHeading.x);
        sensor.AddObservation((float)pacHeading.y);

        bool[] open = OpenDirections(ghost, lastDir);
        int[,] toLead = Distances(LeadCell(pac, pacHeading, 4));

        // which directions are open: up, down, left, right (4)
        for (int i = 0; i < 4; i++)
        {
            sensor.AddObservation(open[i] ? 1f : 0f);
        }

        // maze distance to pacstudent after each move (4)
        for (int i = 0; i < 4; i++)
        {
            float d = open[i] ? Dist(toPac, ghost + Dirs[i]) / (float)FarAway : 1f;
            sensor.AddObservation(d);
        }

        // maze distance to the tile a bit ahead of pacstudent after each move (4)
        for (int i = 0; i < 4; i++)
        {
            float d = open[i] ? Dist(toLead, ghost + Dirs[i]) / (float)FarAway : 1f;
            sensor.AddObservation(d);
        }
    }
}
