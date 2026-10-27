using System.Collections.Generic;
using UnityEngine;

// training world: a fake pacstudent and one ghost
public class TrainingArea : MonoBehaviour
{
    [SerializeField] private PacBot pac;
    [SerializeField] private GhostAgent ghost;
    [SerializeField] private float maxSeconds = 30f;
    [SerializeField] private int minStartDistance = 12;

    private List<Vector2Int> cells;
    private float timer;

    public Vector2Int PacCell { get { return Vector2Int.RoundToInt(pac.transform.position); } }
    public Vector2Int GhostCell { get { return Vector2Int.RoundToInt(ghost.transform.position); } }
    public Vector2Int PacHeading { get { return pac.Heading; } }
    public Vector3 PacPosition { get { return pac.transform.position; } }

    void Awake()
    {
        LevelGrid.Load();
        cells = MazeUtil.ReachableCells();
    }

    public Vector2Int RandomCell()
    {
        return cells[Random.Range(0, cells.Count)];
    }

    // new episode: put both somewhere random, not too close together
    public void ResetEpisode()
    {
        timer = 0f;

        Vector2Int p = RandomCell();
        int[,] fromPac = MazeUtil.Distances(p);

        Vector2Int g = RandomCell();
        for (int tries = 0; tries < 50; tries++)
        {
            if (MazeUtil.Dist(fromPac, g) >= minStartDistance) break;
            g = RandomCell();
        }

        pac.PlaceAt(p);
        ghost.PlaceAt(g);
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer > maxSeconds)
        {
            timer = 0f;
            ghost.TimeOut();
        }
    }
}
