using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TitleMenu : MonoBehaviour
{
    private bool settingsOpen;

    public void OpenSettings()
    {
        settingsOpen = true;
    }

    private void OnGUI()
    {
        if (!settingsOpen)
            return;

        var area = new Rect((Screen.width - 320f) / 2f, (Screen.height - 140f) / 2f, 320f, 140f);
        GUI.ModalWindow(GetInstanceID(), area, DrawSettings, "Settings");
    }

    private void DrawSettings(int windowId)
    {
        GUILayout.Space(12f);
        GUILayout.Label("Master volume");
        AudioListener.volume = GUILayout.HorizontalSlider(AudioListener.volume, 0f, 1f);
        GUILayout.Space(12f);
        if (GUILayout.Button("Close"))
            settingsOpen = false;
    }

    public void StartGame()
    {
        if (!settingsOpen)
            SceneManager.LoadScene("S_Pr_1");
    }

    public void QuitGame()
    {
        if (settingsOpen)
            return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
