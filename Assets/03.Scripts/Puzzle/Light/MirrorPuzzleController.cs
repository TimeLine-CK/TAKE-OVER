/// <summary>
/// 스크립트 최종 수정일: 2026-10-29
/// 기능: 시대별 거울의 각도를 공유하고 사망 후 처음 정해진 각도로 되돌린다.
/// </summary>

using System.Collections.Generic;
using UnityEngine;

public sealed class MirrorPuzzleController : MonoBehaviour
{
    [SerializeField] private Vector2 initialAngleRange = new(0f, 360f); // 처음 설정할 각도의 범위

    public bool IsReady
    {
        get;
        private set;
    }

    private sealed class MirrorState
    {
        public float InitialAngle;
        public float Angle;
        public readonly List<MirrorReflector2D> Mirrors = new();
    }

    private readonly Dictionary<string, MirrorState> states = new();
    private TimeShiftController timeShift;

    private void Awake()
    {
        timeShift = FindFirstObjectByType<TimeShiftController>();
    }

    private void OnEnable()
    {
        if (timeShift != null)
        {
            timeShift.WorldsReady += Initialize;
            if (timeShift.IsReady)
                Initialize();
        }
    }

    private void Start()
    {
        if (timeShift == null || timeShift.IsReady)
            Initialize();
    }

    private void OnDisable()
    {
        if (timeShift != null)
            timeShift.WorldsReady -= Initialize;
    }

    /// <summary>
    /// 두 시대의 같은 이름을 가진 거울에 초기 각도 하나를 정해 적용한다.
    /// </summary>
    public void Initialize()
    {
        if (IsReady || (timeShift != null && !timeShift.IsReady))
            return;

        TimeFlagWolrdRoot pastWorld = null;
        if (timeShift != null)
            pastWorld = timeShift.GetWorld(TimeFlag.Past);

        foreach (MirrorReflector2D mirror in FindObjectsByType<MirrorReflector2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mirror.gameObject.scene != gameObject.scene
                && (pastWorld == null || !mirror.transform.IsChildOf(pastWorld.transform)))
                continue;
            if (mirror.IsFixed)
            {
                mirror.Bind(this, 0f);
                continue;
            }
            if (string.IsNullOrWhiteSpace(mirror.MirrorId))
            {
                Debug.LogWarning("거울의 Mirror Id를 설정해 주세요.", mirror);
                continue;
            }

            if (!states.TryGetValue(mirror.MirrorId, out MirrorState state))
            {
                float angle = Random.Range(Mathf.Min(initialAngleRange.x, initialAngleRange.y),
                    Mathf.Max(initialAngleRange.x, initialAngleRange.y));
                state = new MirrorState
                {
                    InitialAngle = angle,
                    Angle = angle
                };
                states.Add(mirror.MirrorId, state);
            }

            TimeFlagWolrdRoot world = mirror.GetComponentInParent<TimeFlagWolrdRoot>(true);
            bool duplicate = false;
            foreach (MirrorReflector2D linked in state.Mirrors)
                if (linked.GetComponentInParent<TimeFlagWolrdRoot>(true) == world)
                    duplicate = true;
            if (duplicate)
            {
                Debug.LogWarning("같은 시대의 거울은 서로 다른 Mirror Id를 사용해 주세요: " + mirror.MirrorId, mirror);
                continue;
            }

            state.Mirrors.Add(mirror);
            mirror.Bind(this, state.Angle);
        }

        IsReady = true;
        Physics2D.SyncTransforms();
    }

    public void RotateMirror(MirrorReflector2D mirror, float deltaAngle)
    {
        if (!IsReady || !isActiveAndEnabled || mirror == null || mirror.IsFixed
            || !states.TryGetValue(mirror.MirrorId, out MirrorState state) || !state.Mirrors.Contains(mirror))
            return;

        state.Angle = Mathf.Repeat(state.Angle + deltaAngle, 360f);
        ApplyState(state);
    }

    /// <summary>
    /// 체크포인트에서 조절한 각도와 관계없이 처음 각도로 복원한다.
    /// </summary>
    public void ResetMirrors()
    {
        foreach (MirrorState state in states.Values)
        {
            state.Angle = state.InitialAngle;
            ApplyState(state);
        }
        Physics2D.SyncTransforms();
    }

    private void ApplyState(MirrorState state)
    {
        foreach (MirrorReflector2D mirror in state.Mirrors)
            if (mirror != null)
                mirror.ApplyAngle(state.Angle);
    }
}
