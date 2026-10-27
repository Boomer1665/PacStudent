using System.Collections.Generic;
using UnityEngine;

public enum GhostState { Normal, Scared, Recovering, Dead }

public class GhostController : MonoBehaviour
{
    // All the ghosts share the orange cat animator
    private const string Prefix = "OrangeCat_";

    // Speeds are worked out from pacstudent's speed
    private const float NormalSpeed = 0.9f;
    private const float ScaredSpeed = 0.5f;

    // Gaps in the ghost house wall and the tiles just outside of them
    private const int GapLeft = 5;
    private const int GapRight = 6;
    private const int TopOutside = -9;
    private const int BottomOutside = -15;

    [SerializeField] private int ghostNumber = 1;

    // Every tile + direction on the outside wall loop (ghost 4 use this)
    private static HashSet<(Vector2Int, Vector2Int)> outerWall;

    private Animator anim;
    private PacStudentController pac;
    private GhostBrain brain;          // only on the smart ghosts (innovation scene)
    private static readonly List<GhostController> all = new List<GhostController>();   // every ghost, so they can see each other
    private bool waitingForBrain;

    private Vector3 startPos;
    private Vector2Int startCell;

    private Vector2Int cell;
    private Vector2Int nextCell;
    private Vector2Int lastDir = Vector2Int.zero;
    private bool moving;
    private float progress;
    private string facing = "Down";

    private List<Vector2Int> exitPath = new List<Vector2Int>();
    private int exitStep;
    private bool inHouse;

    public GhostState State { get; private set; } = GhostState.Normal;

    void Awake()
    {
        anim = GetComponent<Animator>();
        startPos = transform.position;
        startCell = Vector2Int.RoundToInt(startPos);
        cell = startCell;
    }

    void Start()
    {
        anim.enabled = true;
        pac = FindAnyObjectByType<PacStudentController>();
        brain = GetComponent<GhostBrain>();
        all.Add(this);

        if (outerWall == null) FindOuterWall();

        SetupExit();
        SetState(GhostState.Normal);
    }

    void OnDestroy()
    {
        all.Remove(this);
    }

    void Update()
    {
        // wait for the countdown, and stop if pacstudent is dying or the game is over
        if (!GameManager.Instance.GameActive || GameManager.Instance.GhostsFrozen) return;

        if (State == GhostState.Dead)
        {
            GoBackToSpawn();
            return;
        }

        // smart ghosts wait here until the brain has picked a direction
        if (waitingForBrain)
        {
            if (!brain.HasAnswer) return;

            waitingForBrain = false;
            nextCell = cell + AvoidSameChoice(brain.Choice);
            StartMove();
        }

        if (!moving) PickNextMove();
        if (waitingForBrain) return;

        progress += GetSpeed() * Time.deltaTime;

        // keep the extra time so the speed stays right on slow frames
        while (progress >= 1f)
        {
            progress -= 1f;
            cell = nextCell;
            transform.position = ToWorld(cell);
            PickNextMove();
            if (waitingForBrain) return;
        }

        transform.position = Vector3.Lerp(ToWorld(cell), ToWorld(nextCell), progress);
    }

    // ----- states -----

    public void SetState(GhostState newState)
    {
        State = newState;
        PlayAnim();
    }

    // Called when pacstudent eats the ghost
    public void Eat()
    {
        waitingForBrain = false;
        moving = false;
        progress = 0f;
        inHouse = false;
        SetState(GhostState.Dead);
    }

    // called when pacstudent dies
    public void ResetGhost()
    {
        waitingForBrain = false;
        transform.position = startPos;
        cell = startCell;
        moving = false;
        progress = 0f;
        lastDir = Vector2Int.zero;
        facing = "Down";

        SetupExit();
        SetState(GhostState.Normal);
    }

    void PlayAnim()
    {
        if (State == GhostState.Normal) anim.Play(Prefix + "Normal_" + facing);
        else if (State == GhostState.Scared) anim.Play(Prefix + "Scared_" + facing);
        else if (State == GhostState.Recovering) anim.Play(Prefix + "Recovering");
        else anim.Play(Prefix + "Dead");
    }

    float GetSpeed()
    {
        float speed = pac.TilesPerSecond * NormalSpeed;
        if (State != GhostState.Normal) speed *= ScaredSpeed;
        return speed;
    }

    // ----- Dead ghosts -----

    // Straight line back to where it started, goes through walls
    void GoBackToSpawn()
    {
        float speed = pac.TilesPerSecond * NormalSpeed * ScaredSpeed;
        transform.position = Vector3.MoveTowards(transform.position, startPos, speed * Time.deltaTime);

        if (transform.position == startPos)
        {
            cell = startCell;
            moving = false;
            progress = 0f;
            lastDir = Vector2Int.zero;

            SetupExit();

            // comes back as whatever the other ghosts are right now
            SetState(GameManager.Instance.GlobalGhostState);
            GameManager.Instance.UpdateMusic();
        }
    }

    // ----- leaving the ghost house -----

    // ghosts 1 and 3 go out the top, 2 and 4 go out the bottom
    void SetupExit()
    {
        exitPath.Clear();
        exitStep = 0;
        inHouse = true;

        bool top = (ghostNumber == 1 || ghostNumber == 3);
        int gapX = (cell.x <= GapLeft) ? GapLeft : GapRight;
        int outsideY = top ? TopOutside : BottomOutside;

        // across to the gap first
        int xStep = (gapX > cell.x) ? 1 : -1;
        for (int x = cell.x; x != gapX; x += xStep)
        {
            exitPath.Add(new Vector2Int(x + xStep, cell.y));
        }

        // then up or down through it
        int yStep = top ? 1 : -1;
        for (int y = cell.y; y != outsideY; y += yStep)
        {
            exitPath.Add(new Vector2Int(gapX, y + yStep));
        }
    }

    // ----- picking where to go -----

    void PickNextMove()
    {
        if (inHouse && exitStep >= exitPath.Count) inHouse = false;

        if (inHouse)
        {
            nextCell = exitPath[exitStep];
            exitStep++;
        }
        else if (brain != null && brain.enabled && State == GhostState.Normal)
        {
            // smart ghost: ask the trained brain, it answers a moment later
            waitingForBrain = true;
            brain.Ask(cell, lastDir);
            return;
        }
        else
        {
            nextCell = cell + PickDirection();
        }

        StartMove();
    }

    void StartMove()
    {
        lastDir = nextCell - cell;
        moving = true;

        // only change the animation if the ghost turned
        string newFacing = DirName(lastDir);
        if (newFacing != facing)
        {
            facing = newFacing;
            PlayAnim();
        }
    }

    // if another ghost on this tile already picked the same way, take the next best way instead
    Vector2Int AvoidSameChoice(Vector2Int choice)
    {
        List<Vector2Int> claimed = new List<Vector2Int>();

        foreach (GhostController other in all)
        {
            if (other == this || other.State != GhostState.Normal) continue;

            if (other.moving && other.cell == cell && other.nextCell != other.cell)
            {
                claimed.Add(other.nextCell - other.cell);
            }
        }

        if (!claimed.Contains(choice)) return choice;

        // closest to pacstudent out of the open ways nobody else took
        bool[] open = MazeUtil.OpenDirections(cell, lastDir);
        int[,] toPac = MazeUtil.Distances(pac.Cell);
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

    Vector2Int PickDirection()
    {
        List<Vector2Int> options = GetOptions();

        // Scared and recovering ghosts all act like ghost 1
        int type = ghostNumber;
        if (State != GhostState.Normal) type = 1;

        // Ghost 4 follows the outside wall once it is on it
        if (type == 4 && outerWall.Contains((cell, lastDir)))
        {
            return LeftHand(cell, lastDir);
        }

        // Ghost 1 = pacstudent further away (or same), ghost 2 = closer (or same)
        if (type == 1 || type == 2)
        {
            float now = Vector2.Distance(cell, pac.transform.position);
            List<Vector2Int> good = new List<Vector2Int>();

            foreach (Vector2Int d in options)
            {
                float after = Vector2.Distance(cell + d, pac.transform.position);

                if (type == 1 && after >= now) good.Add(d);
                if (type == 2 && after <= now) good.Add(d);
            }

            if (good.Count > 0) options = good;
        }

        // Ghost 3 (or no good options) just picks anything
        return options[Random.Range(0, options.Count)];
    }

    // Directions the ghost can go, only turns around if dead end
    List<Vector2Int> GetOptions()
    {
        bool[] open = MazeUtil.OpenDirections(cell, lastDir);
        List<Vector2Int> list = new List<Vector2Int>();

        for (int i = 0; i < 4; i++)
        {
            if (open[i]) list.Add(MazeUtil.Dirs[i]);
        }

        if (list.Count == 0) list.Add(Vector2Int.zero);

        return list;
    }

    // ----- the outside wall (ghost 4) -----

    // wall on the left: try left, then straight, then right, then back
    static Vector2Int LeftHand(Vector2Int pos, Vector2Int heading)
    {
        Vector2Int left = new Vector2Int(-heading.y, heading.x);
        Vector2Int right = new Vector2Int(heading.y, -heading.x);
        Vector2Int[] order = { left, heading, right, -heading };

        foreach (Vector2Int d in order)
        {
            if (MazeUtil.CanWalk(pos + d)) return d;
        }

        return -heading;
    }

    // walks round the outside wall once from the top left and remembers every step
    static void FindOuterWall()
    {
        outerWall = new HashSet<(Vector2Int, Vector2Int)>();

        Vector2Int pos = new Vector2Int(-7, 1);
        Vector2Int heading = Vector2Int.up;

        while (outerWall.Add((pos, heading)))
        {
            heading = LeftHand(pos, heading);
            pos += heading;
        }
    }

    // ----- small helpers -----

    static string DirName(Vector2Int d)
    {
        if (d == Vector2Int.up) return "Up";
        if (d == Vector2Int.left) return "Left";
        if (d == Vector2Int.right) return "Right";
        return "Down";
    }

    static Vector3 ToWorld(Vector2Int c)
    {
        return new Vector3(c.x, c.y, 0f);
    }
}