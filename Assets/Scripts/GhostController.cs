using System.Collections;
using UnityEngine;

public enum GhostState { Normal, Scared, Recovering, Dead }

public class GhostController : MonoBehaviour
{
    private const string Prefix = "OrangeCat_";
    private const float DeadTime = 3f;

    private Animator animator;
    private Coroutine deadRoutine;
    private Vector3 startPosition;

    public GhostState State { get; private set; } = GhostState.Normal;

    void Awake()
    {
        animator = GetComponent<Animator>();
        startPosition = transform.position;
    }

    void Start()
    {
        animator.enabled = true;
        SetState(GhostState.Normal);
    }

    public void SetState(GhostState state)
    {
        State = state;

        switch (state)
        {
            case GhostState.Normal: animator.Play(Prefix + "Normal_Down"); break;
            case GhostState.Scared: animator.Play(Prefix + "Scared_Down"); break;
            case GhostState.Recovering: animator.Play(Prefix + "Recovering"); break;
            case GhostState.Dead: animator.Play(Prefix + "Dead"); break;
        }
    }

    // PacStudent died: back to the starting spot in the Normal state
    public void ResetGhost()
    {
        if (deadRoutine != null) StopCoroutine(deadRoutine);
        transform.position = startPosition;
        SetState(GhostState.Normal);
    }

    // PacStudent ate this ghost while it was Scared or Recovering
    public void Eat()
    {
        SetState(GhostState.Dead);

        if (deadRoutine != null) StopCoroutine(deadRoutine);
        deadRoutine = StartCoroutine(ReviveAfterDelay());
    }

    IEnumerator ReviveAfterDelay()
    {
        yield return new WaitForSeconds(DeadTime);

        // Back to Normal if the scared timer has finished, otherwise Scared or Recovering
        SetState(GameManager.Instance.GlobalGhostState);
        GameManager.Instance.UpdateMusic();
    }
}