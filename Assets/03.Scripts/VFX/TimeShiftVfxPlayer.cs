/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 시간 전환 영상을 화면 위에 재생하고 월드 교체 시점을 전달한다.
/// </summary>

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class TimeShiftVfxPlayer : MonoBehaviour
{
    [SerializeField] private VideoClip shiftVfx;                                // 시간 전환에 사용할 영상
    [SerializeField] private Texture2D revealMask;                              // 영상 프레임별 균열 경계 반지름
    [SerializeField, ColorUsage(true, true)] private Color effectColor =
        new(0.1f, 1.4f, 2.5f, 1f);                                             // 균열 이펙트 색상

    private static readonly int frameId = Shader.PropertyToID("_Frame");
    private static readonly int videoTextureId = Shader.PropertyToID("_VideoTex");

    private VideoPlayer videoPlayer;                                            // 전환 영상을 재생할 플레이어
    private RawImage videoImage;                                                // 영상을 화면 전체에 표시할 이미지
    private RenderTexture renderTexture;                                        // 영상 프레임을 받을 렌더 텍스처
    private RenderTexture sourceTexture;                                        // 전환 직전 화면을 보관할 렌더 텍스처
    private Material effectMaterial;                                            // 검은 배경을 제외하고 표시할 머티리얼
    private long currentVideoFrame;                                             // 실제 출력 준비가 끝난 영상 프레임
    private int playbackVersion;                                                // 사망 등으로 취소된 재생을 구분하는 번호

    /// <summary>
    /// 영상 재생에 필요한 화면 오버레이를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (shiftVfx == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("TimeShiftVfxPlayer에 전환 영상이 설정되지 않았습니다.", this);
            }

            return;
        }

        CreateOverlay();

        if (effectMaterial == null)
        {
            return;
        }

        CreateVideoPlayer();
    }

    /// <summary>
    /// 현재 화면을 보관하고 목적 시점을 중앙부터 펼치며 전환 영상을 재생한다.
    /// </summary>
    public IEnumerator Play(Action shiftWorld)
    {
        int version = playbackVersion;
        if (videoPlayer == null || videoImage == null || effectMaterial == null || revealMask == null)
        {
            shiftWorld?.Invoke();
            yield break;
        }

        if (!videoPlayer.isPrepared)
        {
            videoPlayer.Prepare();
            // 영상 준비가 늦어져도 즉발 전환 입력을 지연시키지 않는다.
            shiftWorld?.Invoke();
            yield break;
        }

        yield return new WaitForEndOfFrame();

        PrepareSourceTexture();
        ScreenCapture.CaptureScreenshotIntoRenderTexture(sourceTexture);
        effectMaterial.SetTexture("_SourceTex", sourceTexture);
        effectMaterial.SetFloat("_SourceFlipY", SystemInfo.graphicsUVStartsAtTop ? 1f : 0f);
        effectMaterial.SetTexture(videoTextureId, renderTexture);
        effectMaterial.SetFloat(frameId, 0f);
        currentVideoFrame = 0;

        shiftWorld?.Invoke();
        if (version != playbackVersion || !isActiveAndEnabled)
            yield break;
        videoImage.enabled = true;
        videoPlayer.time = 0d;
        videoPlayer.Play();

        float duration = Mathf.Max(0.01f, (float)shiftVfx.length);
        float deadline = Time.realtimeSinceStartup + duration + 3f;

        while (Time.realtimeSinceStartup < deadline)
        {
            if (shiftVfx.frameCount > 0 && currentVideoFrame >= (long)shiftVfx.frameCount - 1)
                break;

            yield return null;
        }

        StopPlayback();
    }

    /// <summary>
    /// 재생 중인 영상을 정지하고 화면에 남아 있는 전환 이펙트를 숨긴다.
    /// </summary>
    public void StopPlayback()
    {
        playbackVersion++;
        if (videoImage != null)
            videoImage.enabled = false;

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            if (isActiveAndEnabled)
                videoPlayer.Prepare();
        }

        currentVideoFrame = 0;

        if (effectMaterial != null)
            effectMaterial.SetFloat(frameId, 0f);
    }

    private void OnDisable()
    {
        StopPlayback();
    }

    /// <summary>
    /// 현재 Game View 크기에 맞춰 전환 직전 화면을 저장할 렌더 텍스처를 준비한다.
    /// </summary>
    private void PrepareSourceTexture()
    {
        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);

        if (sourceTexture != null && sourceTexture.width == width && sourceTexture.height == height)
        {
            return;
        }

        ReleaseRenderTexture(sourceTexture);
        sourceTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        sourceTexture.name = "Time Shift Source";
        sourceTexture.Create();
    }

    /// <summary>
    /// 최상단에 표시할 전체 화면 Canvas와 영상을 출력할 RawImage를 생성한다.
    /// </summary>
    private void CreateOverlay()
    {
        GameObject canvasObject = new("Time Shift VFX Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        GameObject imageObject = new("Time Shift VFX");
        imageObject.transform.SetParent(canvasObject.transform, false);

        videoImage = imageObject.AddComponent<RawImage>();
        RectTransform rectTransform = videoImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        videoImage.raycastTarget = false;
        videoImage.enabled = false;

        Shader shader = Shader.Find("Hidden/TimeShiftVfx");

        if (shader == null)
        {
            Debug.LogError("TimeShiftVfx 셰이더를 찾을 수 없습니다.", this);
            return;
        }

        effectMaterial = new Material(shader);
        effectMaterial.SetColor("_EffectColor", effectColor);
        effectMaterial.SetTexture("_RevealMask", revealMask);
        videoImage.material = effectMaterial;
    }

    /// <summary>
    /// 전환 영상을 UI에 전달할 VideoPlayer와 RenderTexture를 생성한다.
    /// </summary>
    private void CreateVideoPlayer()
    {
        int width = Mathf.Max(1, (int)shiftVfx.width);
        int height = Mathf.Max(1, (int)shiftVfx.height);
        renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        renderTexture.name = "Time Shift VFX";
        renderTexture.Create();

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.clip = shiftVfx;
        videoPlayer.skipOnDrop = true;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.sendFrameReadyEvents = true;
        videoPlayer.frameReady += OnFrameReady;
        videoPlayer.Prepare();
    }

    private void OnFrameReady(VideoPlayer source, long frame)
    {
        currentVideoFrame = frame;
        effectMaterial.SetFloat(frameId, frame);
    }

    /// <summary>
    /// 런타임에 생성한 렌더 텍스처와 머티리얼을 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.frameReady -= OnFrameReady;

        ReleaseRenderTexture(renderTexture);
        ReleaseRenderTexture(sourceTexture);

        if (effectMaterial != null)
        {
            Destroy(effectMaterial);
        }
    }

    private static void ReleaseRenderTexture(RenderTexture texture)
    {
        if (texture == null)
        {
            return;
        }

        texture.Release();
        Destroy(texture);
    }
}
