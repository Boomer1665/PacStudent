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
    [SerializeField] private float scaredDuration = 10f;
    [SerializeField] private float recoveringTime = 3f;   // Recovering starts with this many seconds left

    // What every non-dead ghost should currently be
    public GhostState GlobalGhostState { get; private set; } = GhostState.Normal;

    private int score;
    private float scaredTimeLeft;

    void Awake()
    {
        Instance = this;
        UpdateScoreText();
    }

    void Update()
    {
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
