using UnityEngine;

public class CherryController : MonoBehaviour
{
    [SerializeField] private GameObject cherryPrefab;
    [SerializeField] private float speed = 4f;       // tiles per second
    [SerializeField] private float spawnDelay = 5f;  // seconds between cherries
    [SerializeField] private float margin = 2f;      // how far outside the level it starts

    private GameObject cherry;
    private Vector3 startPos;
    private Vector3 endPos;
    private float distance;
    private float progress;   // 0 -> 1 along the whole path
    private float timer;      // counts up while there is no cherry

    void Update()
    {
        // No cherry (never spawned, reached the end, or was eaten): wait, then spawn
        if (cherry == null)
        {
            timer += Time.deltaTime;
            if (timer >= spawnDelay) Spawn();
            return;
        }

        // Linear lerp
        progress += speed * Time.deltaTime / distance;
        cherry.transform.position = Vector3.Lerp(startPos, endPos, progress);

        if (progress >= 1f)
        {
            Destroy(cherry);
            cherry = null;
            timer = 0f;
        }
    }

    void Spawn()
    {
        // Edges of the level
        float left = LevelGrid.MinX - 0.5f;
        float right = LevelGrid.MaxX + 0.5f;
        float bottom = LevelGrid.MinY - 0.5f;
        float top = LevelGrid.MaxY + 0.5f;
        Vector2 centre = new Vector2((left + right) / 2f, (bottom + top) / 2f);

        // Pick a random edge, then a random point along it, just outside the level
        switch (Random.Range(0, 4))
        {
            case 0: startPos = new Vector3(left - margin, Random.Range(bottom, top), 0f); break;
            case 1: startPos = new Vector3(right + margin, Random.Range(bottom, top), 0f); break;
            case 2: startPos = new Vector3(Random.Range(left, right), bottom - margin, 0f); break;
            default: startPos = new Vector3(Random.Range(left, right), top + margin, 0f); break;
        }

        // The mirror point through the centre, so the path always crosses the middle
        endPos = new Vector3(2f * centre.x - startPos.x, 2f * centre.y - startPos.y, 0f);

        distance = Vector3.Distance(startPos, endPos);
        progress = 0f;
        timer = 0f;
        cherry = Instantiate(cherryPrefab, startPos, Quaternion.identity);
    }
}