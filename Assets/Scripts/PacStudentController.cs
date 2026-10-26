using System.Collections;
using UnityEngine;

public class PacStudentController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource moveAudioSource;
    [SerializeField] private AudioSource pelletAudioSource;
    [SerializeField] private AudioSource wallAudioSource;
    [SerializeField] private AudioSource cherryAudioSource;
    [SerializeField] private AudioSource deathAudioSource;
    [SerializeField] private ParticleSystem dust;
    [SerializeField] private ParticleSystem wallBump;
    [SerializeField] private ParticleSystem deathEffect;
    [SerializeField] private float tilesPerSecond = 5f;

    // Top-left walkable tile
    private readonly Vector2Int startCell = new Vector2Int(-7, 1);

    private Vector2Int lastInput = Vector2Int.zero;     // last key pressed, never cleared
    private Vector2Int currentInput = Vector2Int.zero;  // direction we are actually moving in

    private Vector2Int cell;       // grid cell we are on / left from
    private Vector2Int targetCell; // grid cell we are lerping to
    private bool lerping;
    private bool dead;            // true during the death sequence
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
        if (dead || !GameManager.Instance.GameActive) return;

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

    // Laft - right tunnel teleporting
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

        // First frame of being blocked after moving play the bump once
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
        if (dead || !GameManager.Instance.GameActive) return;

        if (other.CompareTag("Pellet"))
        {
            GameManager.Instance.AddScore(10);
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("PowerPellet"))
        {
            GameManager.Instance.AddScore(50);
            Destroy(other.gameObject);
            GameManager.Instance.StartScaredTimer();
        }
        else if (other.CompareTag("Cherry"))
        {
            GameManager.Instance.AddScore(100);
            Destroy(other.gameObject);
            if (cherryAudioSource != null) cherryAudioSource.Play();
        }
        else if (other.CompareTag("Ghost"))
        {
            GhostController ghost = other.GetComponent<GhostController>();
            if (ghost.State == GhostState.Scared || ghost.State == GhostState.Recovering)
                GameManager.Instance.EatGhost(ghost);
            else if (ghost.State == GhostState.Normal)
                Die();
        }
    }

    void Die()
    {
        dead = true;
        lerping = false;
        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        bool outOfLives = GameManager.Instance.LoseLife();   // hides one life icon
        GameManager.Instance.GhostsFrozen = true;

        SetMovingFeedback(false);

        if (deathEffect != null)
        {
            deathEffect.transform.position = transform.position;
            deathEffect.Play();
        }
        if (deathAudioSource != null) deathAudioSource.Play();

        animator.speed = 1f;
        animator.Play("Vacuum_Dead");

        yield return new WaitForSeconds(DeathAnimationLength());

        if (outOfLives) yield break;   // game over is added in Stage 3

        Respawn();
    }

    float DeathAnimationLength()
    {
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == "Vacuum_Dead") return clip.length;
        return 1f;
    }

    void Respawn()
    {
        cell = startCell;
        transform.position = CellToWorld(cell);

        lastInput = Vector2Int.zero;
        currentInput = Vector2Int.zero;
        lerping = false;
        bumped = false;
        progress = 0f;

        // Face right and wait for input, like at the start of the game
        animator.Play("Vacuum_Right");
        animator.speed = 0f;

        GameManager.Instance.ResetGhosts();
        dead = false;
    }

    static Vector3 CellToWorld(Vector2Int c)
    {
        return new Vector3(c.x, c.y, 0f);
    }
}