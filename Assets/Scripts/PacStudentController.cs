using UnityEngine;

public class PacStudentController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource moveAudioSource;
    [SerializeField] private AudioSource pelletAudioSource;
    [SerializeField] private AudioSource wallAudioSource;
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private ParticleSystem wallBump;
    [SerializeField] private float tilesPerSecond = 5f;

    // Top-left walkable tile
    private readonly Vector2Int startCell = new Vector2Int(-7, 1);

    private Vector2Int lastInput = Vector2Int.zero;     // last key pressed, never cleared
    private Vector2Int currentInput = Vector2Int.zero;  // direction we are actually moving in

    private Vector2Int cell;       // grid cell we are on / left from
    private Vector2Int targetCell; // grid cell we are lerping to
    private bool lerping;
    private bool bumped;           // true once the wall bump for this stop has played
    private float progress;        // 0 -> 1 across one tile

    void Awake()
    {
        LevelGrid.Load();
    }

    void Start()
    {
        cell = startCell;
        transform.position = CellToWorld(cell);
        LevelGrid.ClearPellet(cell);

        // Face right and wait for input
        animator.Play("Vacuum_Right");
        animator.speed = 0f;
    }

    void Update()
    {
        ReadInput();

        if (!lerping) TryStartMove();

        if (!lerping)
        {
            StopMoving();
            return;
        }

        progress += tilesPerSecond * Time.deltaTime;

        // Carry leftover time into the next tile so speed stays exact at any frame rate
        while (progress >= 1f)
        {
            progress -= 1f;
            cell = targetCell;
            Teleport();
            transform.position = CellToWorld(cell);
            LevelGrid.ClearPellet(cell);
            lerping = false;

            TryStartMove();
            if (!lerping)
            {
                StopMoving();
                return;
            }
        }

        transform.position = Vector3.Lerp(CellToWorld(cell), CellToWorld(targetCell), progress);
    }

    void ReadInput()
    {
        if (Input.GetKeyDown(KeyCode.W)) lastInput = Vector2Int.up;
        if (Input.GetKeyDown(KeyCode.A)) lastInput = Vector2Int.left;
        if (Input.GetKeyDown(KeyCode.S)) lastInput = Vector2Int.down;
        if (Input.GetKeyDown(KeyCode.D)) lastInput = Vector2Int.right;
    }

    // Only called when PacStudent is not lerping
    void TryStartMove()
    {
        if (lastInput != Vector2Int.zero && LevelGrid.IsWalkable(cell + lastInput))
        {
            currentInput = lastInput;
            StartLerp(currentInput);
        }
        else if (currentInput != Vector2Int.zero && LevelGrid.IsWalkable(cell + currentInput))
        {
            StartLerp(currentInput);
        }
        // otherwise do nothing: PacStudent stops
    }

    void StartLerp(Vector2Int dir)
    {
        targetCell = cell + dir;
        lerping = true;
        bumped = false;

        PlayDirectionAnimation(dir);
        SetMovingFeedback(true);
        PlayMoveAudio(LevelGrid.HasPellet(targetCell));
    }

    // Reached the end of a tunnel: jump to the other side and keep moving inwards
    void Teleport()
    {
        if (cell.x == LevelGrid.MinX && currentInput == Vector2Int.left)
            cell = new Vector2Int(LevelGrid.MaxX, cell.y);
        else if (cell.x == LevelGrid.MaxX && currentInput == Vector2Int.right)
            cell = new Vector2Int(LevelGrid.MinX, cell.y);
    }

    void StopMoving()
    {
        progress = 0f;
        SetMovingFeedback(false);

        // First frame of being blocked after moving: play the bump once
        if (!bumped && currentInput != Vector2Int.zero)
        {
            bumped = true;
            PlayWallBump();
        }
    }

    void PlayWallBump()
    {
        // The wall edge in the direction we were travelling
        Vector3 point = CellToWorld(cell) + new Vector3(currentInput.x, currentInput.y, 0f) * 0.5f;

        if (wallBump != null)
        {
            wallBump.transform.position = point;

            // Aim the spray back away from the wall
            Vector3 away = new Vector3(-currentInput.x, -currentInput.y, 0f);
            wallBump.transform.rotation = Quaternion.LookRotation(away, Vector3.forward);

            wallBump.Play();
        }
        if (wallAudioSource != null) wallAudioSource.Play();
    }

    void PlayDirectionAnimation(Vector2Int dir)
    {
        string state = "Vacuum_Right";
        if (dir == Vector2Int.left) state = "Vacuum_Left";
        else if (dir == Vector2Int.up) state = "Vacuum_Up";
        else if (dir == Vector2Int.down) state = "Vacuum_Down";

        // Only restart the clip when the direction changes
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName(state))
            animator.Play(state);
    }

    void PlayMoveAudio(bool pelletNext)
    {
        if (pelletNext)
        {
            // One sound per pellet, layered on top of each other
            pelletAudioSource.PlayOneShot(pelletAudioSource.clip);

            // Walking loop pauses while pellets are being eaten
            if (moveAudioSource.isPlaying) moveAudioSource.Stop();
        }
        else
        {
            // Continuous walking loop, only started if not already playing
            if (!moveAudioSource.isPlaying) moveAudioSource.Play();
        }
    }

    void SetMovingFeedback(bool moving)
    {
        animator.speed = moving ? 1f : 0f;

        if (!moving && moveAudioSource.isPlaying) moveAudioSource.Stop();

        if (dust != null)
        {
            if (moving && !dust.isPlaying) dust.Play();
            else if (!moving && dust.isPlaying) dust.Stop();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Pellet"))
        {
            GameManager.Instance.AddScore(10);
            Destroy(other.gameObject);
        }
    }

    static Vector3 CellToWorld(Vector2Int c)
    {
        return new Vector3(c.x, c.y, 0f);
    }
}