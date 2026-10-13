/// <summary>
/// 스크립트 최종 수정일: 2026-10-12
/// 기능: 공격 순간의 카메라 흔들림과 URP 후처리 펄스를 접근성 설정에 맞춰 재생한다.
/// </summary>

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class CombatVfxFeedback : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;                                // 화면 충격을 적용할 카메라
    [SerializeField, Range(0.05f, 0.3f)] private float duration = 0.18f;         // 화면 충격 유지 시간
    [SerializeField, Range(0f, 8f)] private float maximumShakePixels = 4f;       // 화면 흔들림 최대 픽셀 거리
    [SerializeField, Range(0f, 4f)] private float bloomIntensity = 1.4f;         // Bloom 최대 강도
    [SerializeField, Range(0f, 0.35f)] private float vignetteIntensity = 0.12f;  // Vignette 최대 강도
    [SerializeField, Range(-0.2f, 0.2f)] private float exposure = 0.04f;         // 노출 변화량
    [SerializeField, Range(0f, 0.1f)] private float chromaticIntensity = 0.025f;// 색수차 최대 강도
    [Header("Accessibility")]
    [FormerlySerializedAs("screenShake")]
    [SerializeField] private bool useScreenShake = true;                        // 카메라 흔들림 사용 여부
    [FormerlySerializedAs("postProcessing")]
    [SerializeField] private bool usePostProcessing = true;                     // 후처리 펄스 사용 여부
    [FormerlySerializedAs("chromaticAberration")]
    [SerializeField] private bool useChromaticAberration;                       // 색수차 사용 여부

    private Volume volume;                                                      // 공격 전용 런타임 Volume
    private VolumeProfile profile;                                              // 공격 전용 런타임 프로필
    private Bloom bloom;                                                        // Bloom 설정
    private Vignette vignette;                                                  // Vignette 설정
    private ColorAdjustments colorAdjustments;                                  // 노출 설정
    private ChromaticAberration chromatic;                                      // 색수차 설정
    private float remaining;                                                    // 화면 충격 남은 시간
    private float strength;                                                     // 현재 화면 충격 세기
    private Vector2 direction;                                                  // 공격 방향
    private Vector3 cameraPosition;                                             // 렌더링 전 카메라 위치
    private bool isShakeApplied;                                                // 현재 렌더링에 흔들림 적용 여부

    /// <summary>
    /// 대상 카메라와 공격 전용 후처리 Volume을 준비한다.
    /// </summary>
    private void OnEnable()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        CreateVolume();
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    /// <summary>
    /// 공격 방향과 세기로 화면 충격을 처음부터 재생한다.
    /// </summary>
    public void Pulse(Vector2 attackDirection, float amount = 1f)
    {
        if (targetCamera == null || amount <= 0f)
        {
            return;
        }

        direction = attackDirection.sqrMagnitude > 0.001f
            ? attackDirection.normalized
            : Vector2.right;
        strength = Mathf.Clamp01(amount);
        remaining = duration;
    }

    /// <summary>
    /// 실제 시간 기준으로 화면 충격의 감쇠값을 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (remaining <= 0f)
        {
            return;
        }

        remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
    }

    /// <summary>
    /// 대상 카메라 렌더링 직전에 흔들림과 후처리 가중치를 적용한다.
    /// </summary>
    private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != targetCamera || remaining <= 0f)
        {
            return;
        }

        float progress = 1f - remaining / duration;
        float envelope = strength * (1f - progress) * (1f - progress);
        volume.weight = usePostProcessing ? envelope : 0f;

        if (!useScreenShake || maximumShakePixels <= 0f)
        {
            return;
        }

        float worldDistance = maximumShakePixels;
        if (camera.orthographic && camera.pixelHeight > 0)
        {
            worldDistance *= camera.orthographicSize * 2f / camera.pixelHeight;
        }

        Vector2 side = new(-direction.y, direction.x);
        Vector2 offset = (
            direction * Mathf.Sin(progress * 37f)
            + side * Mathf.Sin(progress * 53f) * 0.45f
        ) * worldDistance * envelope;

        cameraPosition = camera.transform.position;
        camera.transform.position += (Vector3)offset;
        isShakeApplied = true;
    }

    /// <summary>
    /// 카메라 렌더링이 끝나면 추적 위치와 후처리 가중치를 복구한다.
    /// </summary>
    private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != targetCamera)
        {
            return;
        }

        if (isShakeApplied)
        {
            camera.transform.position = cameraPosition;
            isShakeApplied = false;
        }

        if (volume != null)
        {
            volume.weight = 0f;
        }
    }

    /// <summary>
    /// 공격에만 사용하는 런타임 Volume과 후처리 항목을 생성한다.
    /// </summary>
    private void CreateVolume()
    {
        if (volume != null)
        {
            return;
        }

        GameObject volumeObject = new("Combat Feedback Volume");
        volumeObject.transform.SetParent(transform, false);

        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 0f;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Combat Feedback";
        profile.hideFlags = HideFlags.DontSave;
        volume.sharedProfile = profile;

        bloom = profile.Add<Bloom>();
        bloom.intensity.Override(bloomIntensity);

        vignette = profile.Add<Vignette>();
        vignette.intensity.Override(vignetteIntensity);

        colorAdjustments = profile.Add<ColorAdjustments>();
        colorAdjustments.postExposure.Override(exposure);

        chromatic = profile.Add<ChromaticAberration>();
        chromatic.intensity.Override(chromaticIntensity);
        chromatic.active = useChromaticAberration;
    }

    /// <summary>
    /// 렌더링 이벤트를 해제하고 적용 중인 카메라 상태를 복구한다.
    /// </summary>
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;

        if (isShakeApplied && targetCamera != null)
        {
            targetCamera.transform.position = cameraPosition;
            isShakeApplied = false;
        }

        if (volume != null)
        {
            volume.weight = 0f;
        }
    }

    /// <summary>
    /// 컴포넌트 제거 시 런타임에 만든 Volume과 프로필을 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (volume != null)
        {
            Destroy(volume.gameObject);
        }

        if (profile != null)
        {
            Destroy(profile);
        }
    }
}
