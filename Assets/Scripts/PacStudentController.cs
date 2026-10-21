using UnityEngine;

public class PacStudentController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource moveAudioSource;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private Vector3[] corners;

    private int targetIndex;
    private Vector3 startPos;
    private Vector3 endPos;
    private float startTime;
    private float duration;
    
   
    void Start()
    {
        transform.position = corners[0];
        targetIndex = 1;
        BeginMove();
    }


    void Update() {
        float t = (Time.time - startTime) / duration;
        transform.position = Vector3.Lerp(startPos, endPos, t);

        if (t >= 1f) {
            transform.position = endPos;
            targetIndex = (targetIndex + 1) % corners.Length;
            BeginMove();
        }
    }

    void BeginMove() {
        startPos = transform.position;
        endPos = corners[targetIndex];
        float distance = Vector3.Distance(startPos, endPos);
        duration = distance / moveSpeed;

        startTime = Time.time;

        if (!moveAudioSource.isPlaying) {
            moveAudioSource.Play();
        }

        Vector3 direction = (endPos - startPos);

        if (direction.x > 0) {
            animator.Play("Vacuum_Right");
        } else if (direction.x < 0) {
            animator.Play("Vacuum_Left");
        } else if (direction.y > 0) {
            animator.Play("Vacuum_Up");
        } else if (direction.y < 0) {
            animator.Play("Vacuum_Down");
        }
    }
}