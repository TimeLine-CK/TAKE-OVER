/// <summary>
/// 스크립트 최종 수정일: 2026-10-27
/// 기능: 실제 경과 시간으로 타격 정지를 관리하고 정지 전의 게임 속도를 복원한다.
/// </summary>

using UnityEngine;
using UnityEngine.SceneManagement;

public enum HitStopType
{
    Normal,
    Strong,
    Critical,
    Kill,
    PlayerHit
}

[DefaultExecutionOrder(-10000)]
public sealed class HitStopController : MonoBehaviour
{
    [Min(0f)] public float NormalDuration = 0.04f;
    [Min(0f)] public float StrongDuration = 0.055f;
    [Min(0f)] public float CriticalDuration = 0.06f;
    [Min(0f)] public float KillDuration = 0.065f;
    [Min(0f)] public float PlayerHitDuration = 0.05f;
    [Min(0f)] public float MaximumDuration = 0.08f;

    public static bool IsActive => instance != null && instance.isActive;

    private static HitStopController instance;
    private bool isActive;
    private double startedAt;
    private double endsAt;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    /// <summary>
    /// 연속 타격은 기존 종료 시각과 비교하며 첫 정지부터 최대 시간까지만 늘린다.
    /// </summary>
    public static void Request(HitStopType type)
    {
        if (!Application.isPlaying)
            return;
        if (instance == null)
        {
            GameObject host = new("Hit Stop");
            host.AddComponent<HitStopController>();
        }
        if (!instance.isActiveAndEnabled)
            return;
        if (!instance.isActive && Time.timeScale <= 0f)
            return;

        float duration = instance.NormalDuration;
        if (type == HitStopType.Strong)
            duration = instance.StrongDuration;
        else if (type == HitStopType.Critical)
            duration = instance.CriticalDuration;
        else if (type == HitStopType.Kill)
            duration = instance.KillDuration;
        else if (type == HitStopType.PlayerHit)
            duration = instance.PlayerHitDuration;

        double now = Time.realtimeSinceStartupAsDouble;
        if (!instance.isActive)
        {
            if (duration <= 0f)
                return;
            instance.previousTimeScale = Time.timeScale;
            instance.startedAt = now;
            instance.endsAt = now;
            instance.isActive = true;
            Time.timeScale = 0f;
        }
        instance.endsAt = System.Math.Min(instance.startedAt + Mathf.Max(0f, instance.MaximumDuration),
            System.Math.Max(instance.endsAt, now + Mathf.Max(0f, duration)));
    }

    private void Update()
    {
        if (isActive && Time.realtimeSinceStartupAsDouble >= endsAt)
            Cancel();
    }

    public static void Cancel()
    {
        if (instance == null || !instance.isActive)
            return;
        instance.isActive = false;
        if (Time.timeScale == 0f)
            Time.timeScale = instance.previousTimeScale;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        Cancel();
    }

    private void OnDisable()
    {
        if (instance == this)
            Cancel();
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;
        Cancel();
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        instance = null;
    }
}
