using UnityEngine;
using UnityEngine.SceneManagement;

public class HUDButtons : MonoBehaviour
{
    public void ExitToStart()
    {
        SceneManager.LoadScene("StartScene");
    }
}