/// <summary>
/// 스크립트 최종 수정일: 2026-10-13
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
        if (videoPlayer == null || videoImage == null || effectMaterial == null || revealMask == null)
        {
            shiftWorld?.Invoke();
            yield break;
        }

        if (!videoPlayer.isPrepared)
        {
            videoPlayer.Prepare();
            float prepareDeadline = Time.realtimeSinceStartup + 3f;

            while (!videoPlayer.isPrepared && Time.realtimeSinceStartup < prepareDeadline)
            {
                yield return null;
            }

            if (!videoPlayer.isPrepared)
            {
                Debug.LogWarning("시간 전환 영상을 준비하지 못해 월드만 전환합니다.", this);
                shiftWorld?.Invoke();
                yield break;
            }
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
        if (videoImage != null)
            videoImage.enabled = false;

        if (videoPlayer != null)
            videoPlayer.Stop();

        currentVideoFrame = 0;

        if (effectMaterial != null)
            effectMaterial.SetFloat(frameId, 0f);
    }

    /// <summary>
    /// 사망 등으로 비활성화되면 코루틴 종료 여부와 관계없이 이펙트를 정리한다.
    /// </summary>
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

    /// <summary>
    /// 실제 디코딩된 영상 프레임과 균열 마스크의 경계를 맞춘다.
    /// </summary>
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

    /// <summary>
    /// 사용이 끝난 런타임 렌더 텍스처를 해제한다.
    /// </summary>
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
