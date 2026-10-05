/// <summary>
/// 스크립트 최종 수정일: 2026-09-30
/// 기능: 단일 잔상의 녹화, 복귀, 실행, 체류와 위치 텔레포트를 제어한다.
/// </summary>

using UnityEngine;

[DefaultExecutionOrder(-100)] // 실행 시간 연기
public sealed class EchoController : MonoBehaviour
{
    private const float TickDuration = 1f / 60f;            // 잔상 기록에 사용하는 고정 틱 간격
    private const float MaximumReturnDuration = 0.8f;       // 잔상 복귀 연출 최대 시간

    private enum EchoState { Idle, Recording, Returning }

    [Header("Recording")]
    [Range(2f, 4f)] public float maximumRecordingDuration = 4f;     // 최대 녹화 시간
    [Min(0f)] public float recordingPointCost = 30f;                // 녹화 시작 TP 소비량

    [Header("Playback")]
    [Min(0f)] public float echoLingerDuration = 2f;     // 행동 종료 후 분신 체류 시간

    [Header("Teleport")]
    [Min(0f)] public float teleportPointCost = 25f;             // 분신 위치 텔레포트 TP 소비량
    [Min(0.01f)] public float teleportSearchStep = 0.25f;       // 안전 위치 탐색 간격
    [Min(0f)] public float teleportSearchHeight = 2f;           // 안전 위치 최대 탐색 높이

    public int RecordCount => records.Count;                        // 저장된 잔상 기록 수
    public int RecordCapacity => 1;                                 // 사용할 수 있는 단일 슬롯 수
    public int SelectedRecordIndex => records.Count > 0 ? 0 : -1;   // 현재 선택된 슬롯 위치
    public int RecordingSlotIndex => 0;                             // 현재 녹화가 저장될 슬롯 위치
    public bool IsRecording => state == EchoState.Recording;        // 잔상을 녹화 중인지 여부
    public bool IsReturning => state == EchoState.Returning;        // 잔상이 본체로 복귀 중인지 여부
    public bool IsPlaybackRunning => playbackManager != null && playbackManager.IsRunning;          // 저장된 잔상 실행 여부
    public bool IsEchoLingering => playbackManager != null && playbackManager.IsLingering;          // 잔상 체류 여부
    public bool HasActiveEcho => playbackManager != null && playbackManager.HasActiveEcho;          // 실행 또는 체류 중인 잔상 존재 여부
    public float EchoLingerRemaining => playbackManager?.LingerRemaining ?? 0f;                     // 남은 잔상 체류 시간
    public float RecordingProgress => currentRecording == null
        ? 0f
        : currentRecording.Frames.Count / (float)currentRecording.MaximumTickCount;                 // 현재 녹화 진행 비율
    public float CurrentTP => pointController != null ? pointController.CurrentPoint : 0f;          // 현재 TP Point
    public float MaximumTP => pointController != null ? pointController.maximumPoint : 0f;          // 최대 TP Point
    public float MaximumRecordingDuration => maximumRecordingDuration;                              // 설정된 최대 녹화 시간

    private PlayerMotor motor;                                          // 본체 이동 컴포넌트
    private PlayerInputReader inputReader;                              // 잔상 조작 입력 제공자
    private PlayerCombat combat;                                        // 본체 공격 컴포넌트
    private PlayerAnimator playerAnimator;                              // 본체 애니메이션 컴포넌트
    private PlayerCommandController commandController;                  // 구르기와 커맨드 실행 컴포넌트
    private TPPointController pointController;                          // TP Point 관리 컴포넌트
    private CameraFollow2D cameraFollow;                                // 본체와 잔상 사이를 전환할 카메라
    private Rigidbody2D body;                                           // 본체 리지드바디
    private readonly EchoRecordCollection records = new(1);             // 단일 잔상 기록 슬롯
    private EchoRecording currentRecording;                             // 현재 작성 중인 잔상 기록
    private EchoActor proxy;                                            // 플레이어가 조작 중인 녹화 잔상
    private PlayerMotor proxyMotor;                                     // 녹화 잔상 이동 컴포넌트
    private PlayerCombat proxyCombat;                                   // 녹화 잔상 공격 컴포넌트
    private PlayerAnimator proxyAnimator;                               // 녹화 잔상 애니메이션 컴포넌트
    private EchoState state;                                            // 현재 잔상 시스템 진행 상태
    private float tickAccumulator;                                      // 다음 기록 틱까지 누적된 시간
    private float returnElapsed;                                        // 잔상 복귀가 진행된 시간
    private int currentZoneId;                                          // 현재 위치한 잔상 구역 번호
    private bool pendingJump;                                           // 다음 기록 틱에 저장할 점프 입력
    private bool pendingAttack;                                         // 다음 기록 틱에 저장할 공격 입력
    private bool pendingRoll;                                           // 다음 기록 틱에 저장할 구르기 입력
    private bool pendingDash;                                           // 다음 기록 틱에 저장할 돌진 입력
    private bool pendingUppercut;                                       // 다음 기록 틱에 저장할 올려치기 입력
    private PlayerAnimator.AnimationSnapshot bodyAnimationStartState;   // 녹화 시작 당시 본체 애니메이션 상태
    private EchoActorFactory actorFactory;                              // 녹화 및 재생 잔상 생성기
    private EchoPlaybackManager playbackManager;                        // 단일 잔상 실행 관리자
    private EchoWorldPause worldPause;                                  // 녹화 중 월드 감속 관리자

    private float RecordingTimeScale => maximumRecordingDuration * 0.1f; // 녹화 중 월드 시간 배율

    /// <summary>
    /// 잔상 시스템에 필요한 본체 컴포넌트와 관리 객체를 초기화한다.
    /// </summary>
    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        inputReader = GetComponent<PlayerInputReader>();
        combat = GetComponent<PlayerCombat>();
        playerAnimator = GetComponentInChildren<PlayerAnimator>();
        commandController = GetComponent<PlayerCommandController>();
        pointController = GetComponent<TPPointController>();
        body = GetComponent<Rigidbody2D>();
        cameraFollow = FindFirstObjectByType<CameraFollow2D>();
        actorFactory = new EchoActorFactory(gameObject);
        playbackManager = new EchoPlaybackManager(actorFactory);
        worldPause = new EchoWorldPause();
    }

    /// <summary>
    /// 씬에 배치된 잔상 상태 UI를 연결한다.
    /// </summary>
    private void Start()
    {
        FindFirstObjectByType<EchoStatusUI>()?.Initialize(this);
    }

    /// <summary>
    /// 현재 잔상 상태에 따라 녹화, 실행, 텔레포트와 복귀 입력을 처리한다.
    /// </summary>
    private void Update()
    {
        if (state == EchoState.Idle)
        {
            if (inputReader.ConsumeTeleportPressed())
            {
                TryTeleportToEcho();
            }
            if (inputReader.ConsumeExecutePressed())
            {
                ExecutePlayback();
            }
            if (inputReader.ConsumeCloneTogglePressed())
            {
                BeginRecording();
            }
            return;
        }

        if (state == EchoState.Recording)
        {
            ReadRecordingInputs();

            if (inputReader.ConsumeCloneTogglePressed())
            {
                BeginReturn();
                return;
            }

            tickAccumulator += Time.unscaledDeltaTime;
            while (tickAccumulator >= TickDuration && state == EchoState.Recording)
            {
                tickAccumulator -= TickDuration;
                RecordTick();
            }

            return;
        }

        UpdateReturn();
    }

    /// <summary>
    /// 현재 위치한 잔상 구역 번호를 저장한다.
    /// </summary>
    public void EnterZone(EchoZone zone)
    {
        if (zone != null)
            currentZoneId = zone.ZoneId;
    }

    /// <summary>
    /// 현재 잔상 구역을 벗어나면 기본 구역 번호로 되돌린다.
    /// </summary>
    public void ExitZone(EchoZone zone)
    {
        if (zone != null && currentZoneId == zone.ZoneId)
            currentZoneId = 0;
    }

    /// <summary>
    /// 녹화 중 사용할 일회성 입력을 다음 기록 틱까지 보관한다.
    /// </summary>
    private void ReadRecordingInputs()
    {
        pendingJump |= inputReader.ConsumeJumpPressed();
        pendingAttack |= inputReader.ConsumeAttackPressed();
        pendingRoll |= inputReader.ConsumeRollPressed();
        pendingDash |= inputReader.ConsumeDashPressed();
        pendingUppercut |= inputReader.ConsumeUppercutPressed();
    }

    /// <summary>
    /// TP를 소비하고 본체 상태에서 조작 가능한 녹화 잔상을 생성한다.
    /// </summary>
    private void BeginRecording()
    {
        if (HasActiveEcho
            || pointController == null
            || !pointController.TryUse(recordingPointCost))
        {
            return;
        }

        int maximumTickCount = Mathf.RoundToInt(maximumRecordingDuration / TickDuration);

        currentRecording = records.Create(currentZoneId, motor.CaptureSnapshot(), maximumTickCount);

        bodyAnimationStartState = playerAnimator.CaptureSnapshot();
        proxy = actorFactory.CreateRecording(currentRecording.Id, body.position, transform.rotation);

        proxyMotor = proxy.Motor;
        proxyCombat = proxy.Combat;
        proxyAnimator = proxy.Animator;

        proxyMotor.enabled = false;

        cameraFollow?.SetTarget(proxy.GameObject.transform);
        worldPause.SlowDown(RecordingTimeScale);

        body.simulated = false;
        motor.enabled = false;
        combat.enabled = false;
        playerAnimator.enabled = false;

        if (commandController != null)
        {
            commandController.enabled = false;
        }

        tickAccumulator = 0f;
        ClearPendingRecordingInputs();
        state = EchoState.Recording;
        inputReader.ClearPendingInputs();
    }

    /// <summary>
    /// 녹화 입력을 잔상에 적용하고 한 틱의 상태를 저장한다.
    /// </summary>
    private void RecordTick()
    {
        if (commandController == null)
            return;

        bool rollExecuted = pendingRoll && commandController.TryRoll(proxyMotor);
        bool dashExecuted = pendingDash && commandController.TryDash(proxyMotor);

        Vector2 move = inputReader.Move;
        proxyMotor.SimulateInput(move, pendingJump, TickDuration);
        bool uppercutExecuted = pendingUppercut && commandController.TryUppercut(proxyCombat);

        if (pendingAttack && !uppercutExecuted)
        {
            proxyCombat.PerformAttack(proxyCombat.GetPointerAimDirection());
        }


        worldPause.Simulate(TickDuration, proxy.Body);

        EchoFrame frame = new(
            currentRecording.Frames.Count,
            move,
            pendingJump,
            pendingAttack,
            rollExecuted,
            dashExecuted,
            uppercutExecuted,
            proxyMotor.FacingDirection,
            proxyCombat.LastAttackAimDirection,
            proxyMotor.CaptureSnapshot(),
            proxyAnimator.CaptureSnapshot()
        );

        ClearPendingRecordingInputs();
        currentRecording.TryAddFrame(frame);

        if (currentRecording.Frames.Count >= currentRecording.MaximumTickCount)
        {
            BeginReturn();
        }

    }

    /// <summary>
    /// 녹화를 종료하고 잔상이 본체로 돌아오는 상태를 시작한다.
    /// </summary>
    private void BeginReturn()
    {
        if (state != EchoState.Recording)
        {
            return;
        }

        state = EchoState.Returning;
        returnElapsed = 0f;
        proxy.Body.simulated = false;
        proxyAnimator.SetPlaybackSpeed(0f);
        cameraFollow?.ReturnToTarget(transform, GetReturnDuration());

        if (currentRecording.Frames.Count == 0)
        {
            RestoreBodyAndWorld();
        }

    }

    /// <summary>
    /// 기록을 역순으로 복원해 잔상의 본체 복귀 연출을 진행한다.
    /// </summary>
    private void UpdateReturn()
    {
        worldPause.Simulate(Time.unscaledDeltaTime);

        float duration = GetReturnDuration();
        returnElapsed += Time.unscaledDeltaTime;
        float progress = duration <= 0f ? 1f : Mathf.Clamp01(returnElapsed / duration);

        int index = Mathf.Clamp(
            Mathf.RoundToInt((currentRecording.Frames.Count - 1) * (1f - progress)),
            0,
            currentRecording.Frames.Count - 1
        );

        proxyMotor.RestoreSnapshot(currentRecording.Frames[index].DisplayState);
        proxyAnimator.RestoreSnapshot(currentRecording.Frames[index].AnimationState);

        if (progress >= 1f)
        {
            RestoreBodyAndWorld();
        }

    }

    /// <summary>
    /// 기록 길이를 기준으로 실제 잔상 복귀 시간을 계산한다.
    /// </summary>
    private float GetReturnDuration()
    {
        return Mathf.Min(currentRecording.Duration, MaximumReturnDuration);
    }

    /// <summary>
    /// 녹화 잔상을 제거하고 본체와 월드 상태를 녹화 시작 시점으로 복구한다.
    /// </summary>
    private void RestoreBodyAndWorld()
    {
        if (proxy.GameObject != null)
        {
            Destroy(proxy.GameObject);
        }


        worldPause.Resume();
        body.simulated = true;
        motor.enabled = true;
        combat.enabled = true;
        motor.RestoreSnapshot(currentRecording.StartState);
        playerAnimator.RestoreSnapshot(bodyAnimationStartState);
        playerAnimator.enabled = true;
        if (commandController != null)
        {
            commandController.enabled = true;
        }

        records.Add(currentRecording);
        currentRecording = null;
        proxy = default;
        proxyMotor = null;
        proxyCombat = null;
        proxyAnimator = null;
        state = EchoState.Idle;
        inputReader.ClearPendingInputs();
    }

    /// <summary>
    /// 저장된 단일 잔상 기록을 실행한다.
    /// </summary>
    private void ExecutePlayback()
    {
        if (records.Count == 0 || HasActiveEcho)
        {
            return;
        }

        playbackManager.Execute(records.Records[0], echoLingerDuration);
    }

    /// <summary>
    /// TP를 소비하고 실행 또는 체류 중인 잔상 위치로 본체를 이동한다.
    /// </summary>
    private void TryTeleportToEcho()
    {
        Transform target = playbackManager.ActiveTransform;
        if (target == null || pointController == null)
            return;

        if (!TryFindTeleportPosition(target.position, out Vector2 destination))
            return;

        if (!pointController.TryUse(teleportPointCost))
            return;

        motor.Teleport(destination);
    }

    /// <summary>
    /// 잔상 위치부터 위쪽으로 플레이어가 들어갈 수 있는 위치를 찾는다.
    /// </summary>
    private bool TryFindTeleportPosition(Vector2 origin, out Vector2 destination)
    {
        for (float height = 0f; height <= teleportSearchHeight; height += teleportSearchStep)
        {
            Vector2 candidate = origin + Vector2.up * height;
            if (!motor.CanOccupy(candidate))
            {
                continue;
            }

            destination = candidate;
            return true;
        }

        destination = origin;
        return false;
    }

    /// <summary>
    /// 다음 기록 틱에 사용할 일회성 입력을 초기화한다.
    /// </summary>
    private void ClearPendingRecordingInputs()
    {
        pendingJump = false;
        pendingAttack = false;
        pendingRoll = false;
        pendingDash = false;
        pendingUppercut = false;
    }

    /// <summary>
    /// Inspector에서 변경한 잔상 수치를 기획 범위로 제한한다.
    /// </summary>
    private void OnValidate()
    {
        maximumRecordingDuration = Mathf.Clamp(maximumRecordingDuration, 2f, 4f);
        recordingPointCost = Mathf.Max(0f, recordingPointCost);
        echoLingerDuration = Mathf.Max(0f, echoLingerDuration);
        teleportPointCost = Mathf.Max(0f, teleportPointCost);
        teleportSearchStep = Mathf.Max(0.01f, teleportSearchStep);
        teleportSearchHeight = Mathf.Max(0f, teleportSearchHeight);
    }

    /// <summary>
    /// 녹화 또는 복귀 중 오브젝트가 제거되면 월드 시간 상태를 복구한다.
    /// </summary>
    private void OnDestroy()
    {
        if (state == EchoState.Recording || state == EchoState.Returning)
            worldPause.Resume();

        playbackManager?.Clear();
    }
}
