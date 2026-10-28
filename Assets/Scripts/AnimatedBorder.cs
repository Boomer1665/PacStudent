using UnityEngine;

public class AnimatedBorder : MonoBehaviour
{
    [SerializeField] private RectTransform prefab;
    [SerializeField] private int count = 40;
    [SerializeField] private float margin = 30f;
    [SerializeField] private float speed = 200f;        // pixels per second

    RectTransform[] items;
    RectTransform rect;
    float distance;

    void Start()
    {
        rect = GetComponent<RectTransform>();
        items = new RectTransform[count];
        for (int i = 0; i < count; i++)
            items[i] = Instantiate(prefab, transform);
    }

    void Update()
    {
        float w = rect.rect.width - margin * 2;
        float h = rect.rect.height - margin * 2;
        float perimeter = 2 * (w + h);
        distance += speed * Time.deltaTime;

        for (int i = 0; i < count; i++)
        {
            float d = (distance + perimeter * i / count) % perimeter;
            items[i].anchoredPosition = PointOnRect(d, w, h);
        }
    }

    Vector2 PointOnRect(float d, float w, float h)
    {
        if (d < w) return new Vector2(-w / 2 + d, h / 2);        // top
        d -= w;
        if (d < h) return new Vector2(w / 2, h / 2 - d);         // right
        d -= h;
        if (d < w) return new Vector2(w / 2 - d, -h / 2);        // bottom
        d -= w;
        return new Vector2(-w / 2, -h / 2 + d);                  // left
    }
}