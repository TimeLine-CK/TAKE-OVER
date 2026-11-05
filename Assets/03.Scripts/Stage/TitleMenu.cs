using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TitleMenu : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("S_Pr_1");
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
