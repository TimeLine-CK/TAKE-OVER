/// <summary>
/// 스크립트 최종 수정일: 2026-09-15
/// 기능: 잔상의 녹화, 본체 복귀, 슬롯 관리, 실행과 회수 흐름을 제어한다.
/// </summary>

using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerMotor), typeof(PlayerInputReader), typeof(PlayerCombat))]
public sealed class EchoController : MonoBehaviour
{
    private const float TickDuration = 1f / 60f; // 잔상 기록에 사용하는 고정 틱 간격
    private const float MaximumReturnDuration = 0.8f; // 잔상 복귀 연출 최대 시간

    private enum EchoState { Idle, Recording, Returning }

    public int RecordCount => records?.Count ?? 0; // 현재 저장된 잔상 기록 수
    public int RecordCapacity => records?.Capacity ?? 0; // 현재 사용할 수 있는 슬롯 수
    public int SelectedRecordIndex => records?.SelectedIndex ?? -1; // 현재 선택된 슬롯 위치
    public int RecordingSlotIndex => records == null
        ? -1
        : Mathf.Min(records.Count, records.Capacity - 1); // 현재 녹화가 저장될 슬롯 위치
    public bool IsRecording => state == EchoState.Recording; // 잔상을 녹화 중인지 여부
    public bool IsReturning => state == EchoState.Returning; // 잔상이 본체로 복귀 중인지 여부
    public bool IsPlaybackRunning => playbackManager != null && playbackManager.IsRunning; // 저장된 잔상이 실행 중인지 여부
    public bool HasUnrecoveredEchoes => records != null && records.HasUnrecoveredEchoes; // 실행 후 회수되지 않은 잔상 존재 여부
    public float RecordingProgress => currentRecording == null
        ? 0f
        : currentRecording.Frames.Count / (float)EchoRecording.MaximumTickCount; // 현재 녹화 진행 비율

    private PlayerMotor motor; // 본체 이동 컴포넌트
    private PlayerInputReader inputReader; // 잔상 조작 입력 제공자
    private PlayerCombat combat; // 본체 공격 컴포넌트
    private PlayerAnimator playerAnimator; // 본체 애니메이션 컴포넌트
    private CameraFollow2D cameraFollow; // 본체와 잔상 사이를 전환할 카메라
    private Rigidbody2D body; // 본체 리지드바디
    private EchoZone currentZone; // 현재 활성화된 잔상 기록 구역
    private EchoRecordCollection records; // 현재 구역의 잔상 기록 슬롯
    private EchoRecording currentRecording; // 현재 작성 중인 잔상 기록
    private EchoActor proxy; // 플레이어가 조작 중인 녹화 잔상
    private PlayerMotor proxyMotor; // 녹화 잔상 이동 컴포넌트
    private PlayerCombat proxyCombat; // 녹화 잔상 공격 컴포넌트
    private PlayerAnimator proxyAnimator; // 녹화 잔상 애니메이션 컴포넌트
    private EchoState state; // 현재 잔상 시스템 진행 상태
    private float tickAccumulator; // 다음 기록 틱까지 누적된 시간
    private float returnElapsed; // 잔상 복귀가 진행된 시간
    private bool pendingJump; // 다음 기록 틱에 저장할 점프 입력
    private bool pendingAttack; // 다음 기록 틱에 저장할 공격 입력
    private PlayerAnimator.AnimationSnapshot bodyAnimationStartState; // 녹화 시작 당시 본체 애니메이션 상태
    private EchoActorFactory actorFactory; // 녹화 및 재생 잔상 생성기
    private EchoPlaybackManager playbackManager; // 저장된 잔상 실행 관리자
    private EchoWorldPause worldPause; // 녹화 중 월드 정지 관리자

    /// <summary>
    /// 잔상 시스템에 필요한 본체 컴포넌트와 관리 객체를 초기화한다.
    /// </summary>
    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        inputReader = GetComponent<PlayerInputReader>();
        combat = GetComponent<PlayerCombat>();
        playerAnimator = GetComponentInChildren<PlayerAnimator>();
        body = GetComponent<Rigidbody2D>();
        cameraFollow = FindFirstObjectByType<CameraFollow2D>();
        actorFactory = new EchoActorFactory(gameObject);
        playbackManager = new EchoPlaybackManager(actorFactory);
        worldPause = new EchoWorldPause();
    }

    /// <summary>
    /// 씬에 배치된 잔상 상태 UI를 연결하고 테스트 씬에 필요한 기록 구역을 준비한다.
    /// </summary>
    private void Start()
    {
        EchoStatusUI statusUI = FindFirstObjectByType<EchoStatusUI>();
        if (statusUI != null)
            statusUI.Initialize(this);

        if (SceneManager.GetActiveScene().name != "Stage-01"
            || FindFirstObjectByType<EchoZone>() != null)
            return;

        GameObject zoneObject = new("Stage-01 Test EchoZone");
        BoxCollider2D zoneCollider = zoneObject.AddComponent<BoxCollider2D>();
        zoneCollider.isTrigger = true;
        zoneCollider.size = Vector2.one * 10000f;
        EchoZone zone = zoneObject.AddComponent<EchoZone>();
        EnterZone(zone);
    }

    /// <summary>
    /// 현재 잔상 상태에 따라 슬롯, 녹화, 실행, 복귀 입력을 처리한다.
    /// </summary>
    private void Update()
    {
        if (state == EchoState.Idle)
        {
            records?.MoveSelection(inputReader.ConsumeSlotChange());

            if (inputReader.ConsumeDeleteRecordPressed())
                DeleteSelectedRecord();

            if (inputReader.ConsumeExecutePressed())
                TogglePlayback();

            if (inputReader.ConsumeCloneTogglePressed() && currentZone != null)
                BeginRecording();

            return;
        }

        if (state == EchoState.Recording)
        {
            pendingJump |= inputReader.ConsumeJumpPressed();
            pendingAttack |= inputReader.ConsumeAttackPressed();

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
    /// 새 잔상 기록 구역을 활성화하고 해당 구역의 슬롯을 초기화한다.
    /// </summary>
    public void EnterZone(EchoZone zone)
    {
        if (zone == null || state != EchoState.Idle)
            return;

        if (zone == currentZone && records != null)
            return;

        ClearRecordsAndEchoes();
        currentZone = zone;
        records = new EchoRecordCollection(zone.RecordCapacity);
    }

    /// <summary>
    /// 현재 잔상 기록 구역을 벗어나면 기록과 잔상을 제거한다.
    /// </summary>
    public void ExitZone(EchoZone zone)
    {
        if (zone != currentZone || state != EchoState.Idle)
            return;

        ClearRecordsAndEchoes();
        currentZone = null;
    }

    /// <summary>
    /// 본체 상태를 저장하고 조작 가능한 녹화 잔상을 생성한다.
    /// </summary>
    private void BeginRecording()
    {
        currentRecording = records.Create(currentZone.ZoneId, motor.CaptureSnapshot());
        bodyAnimationStartState = playerAnimator.CaptureSnapshot();
        proxy = actorFactory.CreateRecording(currentRecording.Id, body.position, transform.rotation);
        proxyMotor = proxy.Motor;
        proxyCombat = proxy.Combat;
        proxyAnimator = proxy.Animator;
        cameraFollow?.SetTarget(proxy.GameObject.transform);
        worldPause.Pause(proxy.Body);

        motor.enabled = false;
        combat.enabled = false;
        playerAnimator.enabled = false;
        tickAccumulator = 0f;
        pendingJump = false;
        pendingAttack = false;
        state = EchoState.Recording;
        inputReader.ClearPendingInputs();
    }

    /// <summary>
    /// 녹화 잔상에 입력을 적용하고 한 틱의 행동과 상태를 저장한다.
    /// </summary>
    private void RecordTick()
    {
        Vector2 move = inputReader.Move;
        proxyMotor.SimulateInput(move, pendingJump, TickDuration);

        if (pendingAttack)
            proxyCombat.PerformAttack();

        Physics2D.Simulate(TickDuration);

        EchoFrame frame = new(
            currentRecording.Frames.Count,
            move,
            pendingJump,
            pendingAttack,
            proxyMotor.FacingDirection,
            proxyMotor.CaptureSnapshot(),
            proxyAnimator.CaptureSnapshot()
        );

        pendingJump = false;
        pendingAttack = false;
        currentRecording.TryAddFrame(frame);

        if (currentRecording.Frames.Count >= EchoRecording.MaximumTickCount)
            BeginReturn();
    }

    /// <summary>
    /// 녹화를 종료하고 잔상이 본체로 돌아오는 상태를 시작한다.
    /// </summary>
    private void BeginReturn()
    {
        if (state != EchoState.Recording)
            return;

        state = EchoState.Returning;
        returnElapsed = 0f;
        proxy.Body.simulated = false;
        proxyAnimator.SetPlaybackSpeed(0f);
        cameraFollow?.ReturnToTarget(transform, GetReturnDuration());

        if (currentRecording.Frames.Count == 0)
            RestoreBodyAndWorld();
    }

    /// <summary>
    /// 기록을 역순으로 복원해 잔상의 본체 복귀 연출을 진행한다.
    /// </summary>
    private void UpdateReturn()
    {
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
            RestoreBodyAndWorld();
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
            Destroy(proxy.GameObject);

        worldPause.Resume();
        body.simulated = true;
        motor.enabled = true;
        combat.enabled = true;
        motor.RestoreSnapshot(currentRecording.StartState);
        playerAnimator.RestoreSnapshot(bodyAnimationStartState);
        playerAnimator.enabled = true;

        int replacedId = records.Count == records.Capacity
            ? records.Records[0].Id
            : -1;
        records.Add(currentRecording);

        if (replacedId >= 0)
            playbackManager.Remove(replacedId);

        if (playbackManager.IsRunning)
            playbackManager.Stop();

        currentRecording = null;
        proxy = default;
        proxyMotor = null;
        proxyCombat = null;
        proxyAnimator = null;
        state = EchoState.Idle;
        inputReader.ClearPendingInputs();
    }

    /// <summary>
    /// 저장된 잔상의 실행 또는 실행이 끝난 잔상의 회수를 처리한다.
    /// </summary>
    private void TogglePlayback()
    {
        if (records == null || playbackManager.IsRunning)
            return;

        if (records.HasUnrecoveredEchoes)
        {
            playbackManager.Clear();
            records.HasUnrecoveredEchoes = false;
            return;
        }

        if (records.Count == 0)
            return;

        records.HasUnrecoveredEchoes = true;
        playbackManager.Execute(records.Records);
    }

    /// <summary>
    /// 현재 선택된 기록과 연결된 재생 잔상을 제거한다.
    /// </summary>
    private void DeleteSelectedRecord()
    {
        int selectedId = records?.Selected?.Id ?? -1;
        if (selectedId < 0 || !records.DeleteSelected())
            return;

        playbackManager.Remove(selectedId);
    }

    /// <summary>
    /// 현재 구역에 저장된 기록과 생성된 모든 잔상을 제거한다.
    /// </summary>
    private void ClearRecordsAndEchoes()
    {
        records?.Clear();
        playbackManager.Clear();
    }

    /// <summary>
    /// 녹화 또는 복귀 중 오브젝트가 제거되면 본체와 월드 상태를 복구한다.
    /// </summary>
    private void OnDestroy()
    {
        if (state == EchoState.Recording || state == EchoState.Returning)
            RestoreBodyAndWorld();
    }
}
