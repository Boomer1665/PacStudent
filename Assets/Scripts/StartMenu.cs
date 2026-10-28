using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenu : MonoBehaviour
{
    public void LoadLevel1()
    {
        SceneManager.LoadScene("Scenes/Level01");
    }

    public void LoadLevel2()
    {
        SceneManager.LoadScene("Scenes/InnovationScene");
    }
}
