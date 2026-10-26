using UnityEngine;
using TMPro;

public class HighScoreDisplay : MonoBehaviour
{
    [SerializeField] private string levelKey = "Level1";

    void Start()
    {
        TMP_Text text = GetComponent<TMP_Text>();
        text.text = "High Score: " + HighScores.GetScore(levelKey).ToString("D6")
                                   + "\n\nBest Time: " + GameManager.FormatTime(HighScores.GetTime(levelKey));
    }
}
