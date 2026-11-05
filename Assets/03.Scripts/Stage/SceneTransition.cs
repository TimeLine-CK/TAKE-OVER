using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class SceneTransition : MonoBehaviour
{
    [SerializeField] private VideoClip irisIn;
    [SerializeField] private VideoClip irisOut;
    [SerializeField] private Texture full;
    [SerializeField] private Material videoMaterial;

    private static SceneTransition instance;
    private readonly List<PlayerInputReader> blockedInputs = new();
    private RawImage image;
    private RawImage previousScene;
    private VideoPlayer player;
    private RenderTexture videoTexture;
    private RenderTexture screenTexture;
    private bool videoFailed;
    private bool finished;
    private long displayedFrame = -1;

    public static void Load(string scene)
    {
        if (instance != null)
            return;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("이동할 씬을 찾을 수 없습니다: " + scene);
            return;
        }

        SceneTransition prefab = Resources.Load<SceneTransition>("SceneTransition");
        if (prefab == null)
        {
            Debug.LogError("SceneTransition 프리팹이 없습니다.");
            return;
        }

        instance = Instantiate(prefab);
        DontDestroyOnLoad(instance.gameObject);
        instance.StartCoroutine(instance.Transition(scene));
    }

    private IEnumerator Transition(string scene)
    {
        HitStopController.Cancel();
        BlockInput();
        SceneManager.sceneLoaded += OnSceneLoaded;
        Time.timeScale = 0f;
        yield return new WaitForEndOfFrame();

        screenTexture = new RenderTexture(Screen.width, Screen.height, 0);
        screenTexture.Create();
        ScreenCapture.CaptureScreenshotIntoRenderTexture(screenTexture);
        CreateOverlay();
        previousScene.texture = screenTexture;
        if (SystemInfo.graphicsUVStartsAtTop)
            previousScene.uvRect = new Rect(0f, 1f, 1f, -1f);

        yield return PlayVideo(irisIn);
        ShowFull();
        yield return new WaitForEndOfFrame();

        AsyncOperation operation = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        if (operation == null)
        {
            Finish();
            yield break;
        }
        yield return operation;
        yield return null;
        BlockInput();
        TimeShiftController shift = FindFirstObjectByType<TimeShiftController>();
        float deadline = Time.realtimeSinceStartup + 30f;
        while (shift != null && !shift.IsReady && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (shift != null && !shift.IsReady)
            Debug.LogError("목적 씬의 과거·현재 초기화가 완료되지 않았습니다.", shift);

        previousScene.enabled = false;
        yield return new WaitForEndOfFrame();
        yield return PlayVideo(irisOut);
        Finish();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BlockInput();
    }

    private void BlockInput()
    {
        foreach (PlayerInputReader input in FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None))
        {
            if (input.enabled)
            {
                blockedInputs.Add(input);
                input.enabled = false;
            }
        }
    }

    private void LateUpdate()
    {
        if (!finished)
            Time.timeScale = 0f;
    }

    private void CreateOverlay()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        gameObject.AddComponent<GraphicRaycaster>();
        previousScene = CreateImage("Previous Scene");
        image = CreateImage("Hex Iris");
        image.enabled = false;
        videoTexture = new RenderTexture((int)irisIn.width, (int)irisIn.height, 0);
        videoTexture.Create();
        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = videoTexture;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.skipOnDrop = false;
        player.waitForFirstFrame = true;
        player.sendFrameReadyEvents = true;
        player.frameReady += OnFrameReady;
        player.errorReceived += OnVideoError;
    }

    private RawImage CreateImage(string objectName)
    {
        GameObject child = new(objectName, typeof(RectTransform));
        child.transform.SetParent(transform, false);
        RawImage result = child.AddComponent<RawImage>();
        result.rectTransform.anchorMin = Vector2.zero;
        result.rectTransform.anchorMax = Vector2.one;
        result.rectTransform.offsetMin = Vector2.zero;
        result.rectTransform.offsetMax = Vector2.zero;
        return result;
    }

    private void ShowFull()
    {
        image.texture = full;
        image.material = null;
        image.enabled = true;
    }

    private IEnumerator PlayVideo(VideoClip clip)
    {
        player.Stop();
        player.clip = clip;
        displayedFrame = -1;
        videoFailed = false;
        player.Prepare();
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!player.isPrepared && !videoFailed && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (!player.isPrepared || videoFailed)
        {
            Debug.LogWarning("씬 전환 영상 준비 실패: " + clip.name, this);
            yield break;
        }

        player.Play();
        deadline = Time.realtimeSinceStartup + (float)clip.length + 5f;
        while (displayedFrame < 0 && !videoFailed && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (displayedFrame < 0 || videoFailed)
            yield break;

        image.texture = videoTexture;
        image.material = videoMaterial;
        image.enabled = true;
        while (displayedFrame < (long)clip.frameCount - 1 && !videoFailed
            && Time.realtimeSinceStartup < deadline)
            yield return null;
        yield return new WaitForEndOfFrame();
    }

    private void OnFrameReady(VideoPlayer source, long frame)
    {
        displayedFrame = frame;
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        videoFailed = true;
        Debug.LogWarning(message, this);
    }

    private void Finish()
    {
        finished = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        foreach (PlayerInputReader input in blockedInputs)
        {
            if (input != null)
                input.enabled = true;
        }
        Time.timeScale = 1f;
        instance = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (player != null)
            player.Stop();
        if (videoTexture != null)
        {
            videoTexture.Release();
            Destroy(videoTexture);
        }
        if (screenTexture != null)
        {
            screenTexture.Release();
            Destroy(screenTexture);
        }
    }
}
