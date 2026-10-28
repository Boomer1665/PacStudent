using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

// helper stuff for the ml ghosts: maze distances, open directions, and the observations
public static class MazeUtil
{
    public static readonly Vector2Int[] Dirs =
        { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    public const int Width = 28;
    public const int Height = 29;
    public const int FarAway = 60;
    public const int TotalPellets = 224;      // 220 pellets + 4 power pellets
    public const int SpreadRadius = 2;        // ghosts this many steps apart (or closer) try not to pick the same tiles

    // same tiles pacstudent walks on but ghosts cant use the teleporter tiles
    public static bool CanWalk(Vector2Int c)
    {
        return LevelGrid.IsWalkable(c) && c.x > LevelGrid.MinX && c.x < LevelGrid.MaxX;
    }

    // every distance map is worked out once and then kept, so picking a direction doesnt make
    // piles of garbage for unity to clean up (the maze never changes so they never go stale).
    // NOTE: dont change the maps that come back from here, just read them
    private static readonly Dictionary<Vector2Int, int[,]> cache = new Dictionary<Vector2Int, int[,]>();

    public static int[,] Distances(Vector2Int start)
    {
        int[,] map;
        if (cache.TryGetValue(start, out map)) return map;

        map = Search(start);
        cache[start] = map;
        return map;
    }

    // how many steps from the closest of these tiles to every tile
    public static int[,] Distances(List<Vector2Int> starts)
    {
        if (starts.Count == 1) return Distances(starts[0]);

        int[,] result = new int[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                result[x, y] = -1;
            }
        }

        foreach (Vector2Int s in starts)
        {
            int[,] one = Distances(s);

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (one[x, y] < 0) continue;
                    if (result[x, y] < 0 || one[x, y] < result[x, y]) result[x, y] = one[x, y];
                }
            }
        }

        return result;
    }

    // how many steps from start to every tile (-1 means you cant get there)
    static int[,] Search(Vector2Int start)
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

    // which tiles still have a pellet (reads the real level grid)
    public static bool[,] PelletsFromGrid()
    {
        bool[,] pellets = new bool[Width, Height];

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                pellets[x, y] = LevelGrid.HasPellet(new Vector2Int(x + LevelGrid.MinX, y + LevelGrid.MinY));
            }
        }

        return pellets;
    }

    // the pellet that is the fewest steps away on this distance map
    public static bool NearestPellet(bool[,] pellets, int[,] dist, out Vector2Int result)
    {
        result = Vector2Int.zero;
        int best = int.MaxValue;
        bool found = false;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (!pellets[x, y]) continue;
                if (dist[x, y] < 0) continue;

                if (dist[x, y] < best)
                {
                    best = dist[x, y];
                    result = new Vector2Int(x + LevelGrid.MinX, y + LevelGrid.MinY);
                    found = true;
                }
            }
        }

        return found;
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

    // stops a brain picking walls (or going backwards)
    public static void MaskInvalid(IDiscreteActionMask mask, Vector2Int cell, Vector2Int lastDir)
    {
        bool[] open = OpenDirections(cell, lastDir);

        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            if (open[i]) count++;
        }
        if (count == 0) return;

        for (int i = 0; i < 4; i++)
        {
            if (!open[i]) mask.SetActionEnabled(0, i, false);
        }
    }

    // the direction the brain picked, or the first open one if it somehow picked a wall
    public static Vector2Int SafeDirection(Vector2Int cell, Vector2Int lastDir, int action)
    {
        Vector2Int dir = Dirs[action];
        if (CanWalk(cell + dir)) return dir;

        bool[] open = OpenDirections(cell, lastDir);
        for (int i = 0; i < 4; i++)
        {
            if (open[i]) return Dirs[i];
        }

        return dir;
    }

    // if the way a ghost picked leads onto a tile that a nearby ghost is on (or heading to),
    // take the next best open way that doesnt. if there isnt one it keeps its own pick
    public static Vector2Int AvoidClaimed(Vector2Int cell, Vector2Int lastDir, Vector2Int choice,
                                          List<Vector2Int> claimed, int[,] toPac)
    {
        if (!claimed.Contains(cell + choice)) return choice;

        bool[] open = OpenDirections(cell, lastDir);
        Vector2Int best = choice;
        int bestScore = int.MaxValue;

        for (int i = 0; i < 4; i++)
        {
            Vector2Int next = cell + Dirs[i];
            if (!open[i] || claimed.Contains(next)) continue;

            int score = Dist(toPac, next);
            if (score < bestScore)
            {
                bestScore = score;
                best = Dirs[i];
            }
        }

        return best;
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

    // the 49 numbers a ghost gets to look at each time it picks a direction
    public static void Observe(VectorSensor sensor, Vector2Int ghost, Vector2Int lastDir,
                               Vector2Int pac, Vector2Int pacHeading, int[,] toPac,
                               List<Vector2Int> mates, bool[,] pellets)
    {
        bool[] open = OpenDirections(ghost, lastDir);
        int[,] toLead = Distances(LeadCell(pac, pacHeading, 4));

        // where pacstudent is compared to me (2)
        sensor.AddObservation((pac.x - ghost.x) / (float)Width);
        sensor.AddObservation((pac.y - ghost.y) / (float)Height);

        // which way pacstudent is going (2)
        sensor.AddObservation((float)pacHeading.x);
        sensor.AddObservation((float)pacHeading.y);

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

        // maze distance to a tile a bit ahead of pacstudent after each move (4)
        for (int i = 0; i < 4; i++)
        {
            float d = open[i] ? Dist(toLead, ghost + Dirs[i]) / (float)FarAway : 1f;
            sensor.AddObservation(d);
        }

        // how far away I am from pacstudent (1)
        sensor.AddObservation(Dist(toPac, ghost) / (float)FarAway);

        // the other ghosts, closest to pacstudent first: where they are and how far from pacstudent (9)
        List<Vector2Int> sorted = new List<Vector2Int>(mates);
        sorted.Sort((a, b) => Dist(toPac, a).CompareTo(Dist(toPac, b)));

        for (int i = 0; i < 3; i++)
        {
            if (i < sorted.Count)
            {
                Vector2Int m = sorted[i];
                sensor.AddObservation((m.x - ghost.x) / (float)Width);
                sensor.AddObservation((m.y - ghost.y) / (float)Height);
                sensor.AddObservation(Dist(toPac, m) / (float)FarAway);
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(1f);
            }
        }

        // count the pellets, overall and in a 4x4 grid of the map
        int count = 0;
        int[] region = new int[16];

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (!pellets[x, y]) continue;

                count++;
                int rx = x * 4 / Width;
                int ry = (Height - 1 - y) * 4 / Height;
                region[ry * 4 + rx]++;
            }
        }

        // how many pellets are left (1)
        sensor.AddObservation(count / (float)TotalPellets);

        // the pellet pacstudent is probably going for (the nearest one): where it is, and how far after each move (2 + 4)
        Vector2Int target;
        bool found = NearestPellet(pellets, toPac, out target);

        if (found)
        {
            sensor.AddObservation((target.x - pac.x) / (float)Width);
            sensor.AddObservation((target.y - pac.y) / (float)Height);

            int[,] toTarget = Distances(target);
            for (int i = 0; i < 4; i++)
            {
                float d = open[i] ? Dist(toTarget, ghost + Dirs[i]) / (float)FarAway : 1f;
                sensor.AddObservation(d);
            }
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            for (int i = 0; i < 4; i++)
            {
                sensor.AddObservation(1f);
            }
        }

        // where the pellets are left on the map, 4x4 grid (16)
        for (int i = 0; i < 16; i++)
        {
            sensor.AddObservation(System.Math.Min(1f, region[i] / 25f));
        }
    }
}
