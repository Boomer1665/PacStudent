using System.Collections;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text ghostTimerText;
    [SerializeField] private GhostController[] ghosts;
    [SerializeField] private MusicManager music;
    [SerializeField] private AudioSource ghostEatAudio;
    [SerializeField] private GameObject[] lifeIndicators;   // the 3 life icons
    [SerializeField] private GameObject overlayPanel;
    [SerializeField] private TMP_Text overlayText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private float scaredDuration = 10f;
    [SerializeField] private float recoveringTime = 3f;   // Recovering starts with this many seconds left

    // What every non-dead ghost should currently be
    public GhostState GlobalGhostState { get; private set; } = GhostState.Normal;

    // Ghost movement scripts should check this and stand still while it is true
    public bool GhostsFrozen { get; set; }

    // False during the countdown (and later at game over): nothing is allowed to move
    public bool GameActive { get; private set; }

    private int score;
    private int lives = 3;
    private float scaredTimeLeft;
    private float elapsedTime;

    void Awake()
    {
        Instance = this;
        UpdateScoreText();
    }

    void Start()
    {
        StartCoroutine(RoundStart());
    }

    IEnumerator RoundStart()
    {
        GameActive = false;
        timerText.text = FormatTime(0f);
        overlayPanel.SetActive(true);
        music.PlayIntro();

        string[] steps = { "3", "2", "1", "GO!" };
        foreach (string step in steps)
        {
            overlayText.text = step;
            yield return new WaitForSeconds(1f);
        }

        overlayPanel.SetActive(false);
        music.PlayNormal();
        GameActive = true;
    }

    // mm:ss:cc where cc is hundredths of a second, e.g. 01:32:25
    public static string FormatTime(float t)
    {
        int minutes = (int)(t / 60f);
        int seconds = (int)(t % 60f);
        int hundredths = (int)((t * 100f) % 100f);
        return minutes.ToString("00") + ":" + seconds.ToString("00") + ":" + hundredths.ToString("00");
    }

    void Update()
    {
        if (!GameActive) return;

        elapsedTime += Time.deltaTime;
        timerText.text = FormatTime(elapsedTime);

        if (scaredTimeLeft <= 0f) return;

        scaredTimeLeft -= Time.deltaTime;

        if (scaredTimeLeft <= 0f)
        {
            scaredTimeLeft = 0f;
            ghostTimerText.gameObject.SetActive(false);
            SetGlobalState(GhostState.Normal);
            return;
        }

        ghostTimerText.text = Mathf.CeilToInt(scaredTimeLeft).ToString();

        if (scaredTimeLeft <= recoveringTime && GlobalGhostState == GhostState.Scared)
            SetGlobalState(GhostState.Recovering);
    }

    public void AddScore(int points)
    {
        score += points;
        UpdateScoreText();
    }

    void UpdateScoreText()
    {
        scoreText.text = score.ToString("D6");   // 1234 -> "001234"
    }

    // Power pellet eaten
    public void StartScaredTimer()
    {
        scaredTimeLeft = scaredDuration;
        ghostTimerText.text = Mathf.CeilToInt(scaredTimeLeft).ToString();
        ghostTimerText.gameObject.SetActive(true);
        SetGlobalState(GhostState.Scared);
    }

    // Scared or Recovering ghost eaten
    public void EatGhost(GhostController ghost)
    {
        AddScore(300);
        if (ghostEatAudio != null) ghostEatAudio.Play();
        ghost.Eat();
        UpdateMusic();
    }

    // Returns true when there are no lives left
    public bool LoseLife()
    {
        lives--;
        if (lives >= 0 && lives < lifeIndicators.Length)
            lifeIndicators[lives].SetActive(false);
        return lives <= 0;
    }

    // After PacStudent dies: every ghost back to its start, Normal, and the scared timer cleared
    public void ResetGhosts()
    {
        scaredTimeLeft = 0f;
        ghostTimerText.gameObject.SetActive(false);
        GlobalGhostState = GhostState.Normal;

        foreach (GhostController g in ghosts) g.ResetGhost();

        GhostsFrozen = false;
        UpdateMusic();
    }

    void SetGlobalState(GhostState state)
    {
        GlobalGhostState = state;

        // A dead ghost is never changed by the global state
        foreach (GhostController g in ghosts)
            if (g.State != GhostState.Dead) g.SetState(state);

        UpdateMusic();
    }

    // Dead music wins while any ghost is dead, otherwise match the global state
    public void UpdateMusic()
    {
        foreach (GhostController g in ghosts)
        {
            if (g.State == GhostState.Dead)
            {
                music.PlayDead();
                return;
            }
        }

        if (GlobalGhostState == GhostState.Normal) music.PlayNormal();
        else music.PlayScared();
    }
}