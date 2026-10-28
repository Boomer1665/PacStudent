using System.Collections.Generic;
using UnityEngine;

// one training world: a fake pacstudent and four ghosts, like the real game
public class TrainingArea : MonoBehaviour
{
    [SerializeField] private PacBot pac;
    [SerializeField] private GhostAgent[] ghosts;
    [SerializeField] private float maxSeconds = 30f;
    [SerializeField] private int minStartDistance = 12;
    [SerializeField] [Range(0f, 1f)] private float realStartChance = 0.5f;

    // for testing a trained model (turn logStats on)
    [SerializeField] private bool logStats = false;
    [SerializeField] private int logEvery = 100;
    [SerializeField] private float testTimeScale = 1f;

    // where the ghosts are in the real game just after they leave the house
    private static readonly Vector2Int[] realGhostCells =
        { new Vector2Int(5, -9), new Vector2Int(6, -9), new Vector2Int(5, -15), new Vector2Int(6, -15) };

    private List<Vector2Int> cells;
    private bool[,] pellets;
    private float timer;
    private bool needsReset = true;

    // totals over every area, only used for the test printouts
    private static int totalEpisodes;
    private static int totalCatches;
    private static float totalCatchTime;
    private static long stackSamples;
    private static long stackedGhosts;
    private static long bunchedGhosts;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetTotals()
    {
        totalEpisodes = 0;
        totalCatches = 0;
        totalCatchTime = 0f;
        stackSamples = 0;
        stackedGhosts = 0;
        bunchedGhosts = 0;
    }

    public Vector2Int PacCell { get { return Vector2Int.RoundToInt(pac.transform.position); } }
    public Vector2Int PacHeading { get { return pac.Heading; } }
    public Vector3 PacPosition { get { return pac.transform.position; } }
    public bool[,] Pellets { get { return pellets; } }
    public GhostAgent[] Ghosts { get { return ghosts; } }

    void Awake()
    {
        LevelGrid.Load();
        cells = MazeUtil.ReachableCells();
        pellets = MazeUtil.PelletsFromGrid();

        if (testTimeScale > 1f) Time.timeScale = testTimeScale;
    }

    public Vector2Int RandomCell()
    {
        return cells[Random.Range(0, cells.Count)];
    }

    // ----- pellets -----

    public bool HasPellet(Vector2Int c)
    {
        if (!LevelGrid.InBounds(c)) return false;
        return pellets[c.x - LevelGrid.MinX, c.y - LevelGrid.MinY];
    }

    public void EatPellet(Vector2Int c)
    {
        if (!LevelGrid.InBounds(c)) return;
        pellets[c.x - LevelGrid.MinX, c.y - LevelGrid.MinY] = false;
    }

    public bool NearestPellet(int[,] dist, out Vector2Int result)
    {
        return MazeUtil.NearestPellet(pellets, dist, out result);
    }

    // ----- ghosts -----

    public List<Vector2Int> GhostCells()
    {
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (GhostAgent g in ghosts)
        {
            list.Add(Vector2Int.RoundToInt(g.transform.position));
        }
        return list;
    }

    // everyone except this ghost
    public List<Vector2Int> MateCells(GhostAgent self)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (GhostAgent g in ghosts)
        {
            if (g != self) list.Add(Vector2Int.RoundToInt(g.transform.position));
        }
        return list;
    }

    // ----- episodes -----

    // every ghost calls this when its episode starts, only the first one does the work
    public void ResetEpisode()
    {
        if (!needsReset) return;
        needsReset = false;
        timer = 0f;

        pellets = MazeUtil.PelletsFromGrid();

        if (Random.value < realStartChance)
        {
            // like the real game: pacstudent in the corner, ghosts just out of the house
            pac.PlaceAt(new Vector2Int(-7, 1));
            for (int i = 0; i < ghosts.Length; i++)
            {
                Vector2Int c = realGhostCells[i % 4];
                Vector2Int dir = (i % 4 < 2) ? Vector2Int.up : Vector2Int.down;
                ghosts[i].PlaceAt(c, dir);
            }
        }
        else
        {
            // anywhere, with some of the pellets already eaten
            Vector2Int p = RandomCell();
            int[,] fromPac = MazeUtil.Distances(p);
            pac.PlaceAt(p);

            foreach (GhostAgent g in ghosts)
            {
                Vector2Int c = RandomCell();
                for (int tries = 0; tries < 50; tries++)
                {
                    if (MazeUtil.Dist(fromPac, c) >= minStartDistance) break;
                    c = RandomCell();
                }
                g.PlaceAt(c, Vector2Int.zero);
            }

            ClearSomePellets();
        }

        EatPellet(PacCell);
    }

    // takes out a few chunks of pellets so the ghosts see all kinds of situations
    void ClearSomePellets()
    {
        bool[] cleared = new bool[16];
        for (int i = 0; i < 16; i++)
        {
            cleared[i] = Random.value < 0.35f;
        }

        for (int x = 0; x < MazeUtil.Width; x++)
        {
            for (int y = 0; y < MazeUtil.Height; y++)
            {
                if (!pellets[x, y]) continue;

                int rx = x * 4 / MazeUtil.Width;
                int ry = (MazeUtil.Height - 1 - y) * 4 / MazeUtil.Height;

                if (cleared[ry * 4 + rx] && Random.value < 0.8f) pellets[x, y] = false;
            }
        }
    }

    // a ghost touched pacstudent, so the whole team wins
    public void TeamCaught()
    {
        if (needsReset) return;
        needsReset = true;

        Record(true);

        // bigger reward the faster it was
        float reward = 1f + 0.5f * (1f - timer / maxSeconds);

        // freeze everyone first. ml-agents resets a ghost straight away inside EndEpisode,
        // which resets the whole area, so all of them have to be ended without checking any flags
        foreach (GhostAgent g in ghosts)
        {
            g.Freeze();
        }
        foreach (GhostAgent g in ghosts)
        {
            g.EndWithReward(reward);
        }
    }

    void Update()
    {
        if (needsReset) return;

        timer += Time.deltaTime;

        CountStacking();

        if (timer > maxSeconds)
        {
            needsReset = true;
            Record(false);

            foreach (GhostAgent g in ghosts)
            {
                g.Freeze();
            }
            foreach (GhostAgent g in ghosts)
            {
                g.TimeOut();
            }
        }
    }

    // every frame: how many ghosts are on the same tile as another ghost (stacked),
    // and how many are within 2 tiles of another ghost (bunched up in a line)
    void CountStacking()
    {
        int sharing = 0;
        int bunched = 0;

        for (int i = 0; i < ghosts.Length; i++)
        {
            Vector2Int a = Vector2Int.RoundToInt(ghosts[i].transform.position);
            bool same = false;
            bool close = false;

            for (int j = 0; j < ghosts.Length; j++)
            {
                if (i == j) continue;

                Vector2Int b = Vector2Int.RoundToInt(ghosts[j].transform.position);
                if (b == a) same = true;
                if (Mathf.Abs(b.x - a.x) + Mathf.Abs(b.y - a.y) <= 2) close = true;
            }

            if (same) sharing++;
            if (close) bunched++;
        }

        stackedGhosts += sharing;
        bunchedGhosts += bunched;
        stackSamples++;
    }

    void Record(bool caught)
    {
        totalEpisodes++;
        if (caught)
        {
            totalCatches++;
            totalCatchTime += timer;
        }

        if (logStats && totalEpisodes % logEvery == 0)
        {
            float rate = totalCatches / (float)totalEpisodes;
            float mean = totalCatches > 0 ? totalCatchTime / totalCatches : 0f;
            float stacked = stackSamples > 0 ? stackedGhosts / (float)stackSamples : 0f;
            float bunched = stackSamples > 0 ? bunchedGhosts / (float)stackSamples : 0f;
            Debug.Log("[test] episodes " + totalEpisodes + "  catch rate " + rate.ToString("0.00") +
                      "  mean time to catch " + mean.ToString("0.0") + "s" +
                      "  ghosts stacked " + stacked.ToString("0.00") + " of 4" +
                      "  within 2 tiles " + bunched.ToString("0.00") + " of 4");
        }
    }
}
