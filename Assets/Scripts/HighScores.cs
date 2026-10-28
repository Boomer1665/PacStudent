using UnityEngine;

public static class HighScores
{
    public static int GetScore(string level)
    {
        return PlayerPrefs.GetInt(level + "_HighScore", 0);
    }

    public static float GetTime(string level)
    {
        return PlayerPrefs.GetFloat(level + "_BestTime", 0f);
    }

    // Saves when the score beats the old one, or ties it with a faster time
    public static void TrySave(string level, int score, float time)
    {
        int oldScore = GetScore(level);
        float oldTime = GetTime(level);

        if (score > oldScore || (score == oldScore && time < oldTime))
        {
            PlayerPrefs.SetInt(level + "_HighScore", score);
            PlayerPrefs.SetFloat(level + "_BestTime", time);
            PlayerPrefs.Save();
        }
    }
}
