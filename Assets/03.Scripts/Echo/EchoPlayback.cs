/// <summary>
/// 스크립트 최종 수정일: 2026-09-29
/// 기능: 저장된 잔상 상태를 60틱으로 실행하고 종료 후 일정 시간 체류시킨다.
/// </summary>

using System;
using UnityEngine;

public sealed class EchoPlayback : MonoBehaviour
{
    private const float TickDuration = 1f / 60f;        // 기록 재생에 사용하는 틱 간격

    public bool IsRunning => recording != null && frameIndex < recording.Frames.Count;      // 기록 재생 중인지 여부
    public bool IsLingering => recording != null
        && frameIndex >= recording.Frames.Count
        && lingerRemaining > 0f;        // 행동 종료 후 체류 중인지 여부
    public float LingerRemaining => lingerRemaining;        // 남은 체류 시간

    private EchoRecording recording;        // 재생할 행동 기록
    private PlayerMotor motor;              // 기록된 이동 상태를 복원할 컴포넌트
    private PlayerCombat combat;            // 기록된 공격을 실행할 컴포넌트
    private PlayerAnimator playerAnimator;  // 기록된 애니메이션을 복원할 컴포넌트
    private Rigidbody2D body;               // 체류 중 정지할 리지드바디
    private Action finished;                // 체류 종료 시 호출할 콜백
    private int frameIndex;                 // 다음에 실행할 프레임 위치
    private float tickAccumulator;          // 다음 재생 틱까지 누적된 시간
    private float lingerRemaining;          // 행동 종료 후 남은 체류 시간
    private float lingerDuration;           // 행동 종료 후 체류할 시간

    /// <summary>
    /// 재생할 기록, 체류 시간과 완료 콜백을 설정한다.
    /// </summary>
    public void Initialize(EchoRecording source, float duration, Action onFinished)
    {
        recording = source;
        lingerDuration = Mathf.Max(0f, duration);
        finished = onFinished;
        motor = GetComponent<PlayerMotor>();
        combat = GetComponent<PlayerCombat>();
        playerAnimator = GetComponentInChildren<PlayerAnimator>();
        body = GetComponent<Rigidbody2D>();

        if (recording.Frames.Count == 0)
        {
            BeginLinger();
        }

    }

    /// <summary>
    /// 실제 시간에 맞춰 기록 프레임을 재생하거나 체류 시간을 줄인다.
    /// </summary>
    private void Update()
    {
        if (IsRunning)
        {
            tickAccumulator += Time.deltaTime;

            while (tickAccumulator >= TickDuration && IsRunning)
            {
                tickAccumulator -= TickDuration;
                PlayNextFrame();
            }

            return;
        }

        if (!IsLingering)
        {
            return;
        }


        lingerRemaining = Mathf.Max(0f, lingerRemaining - Time.deltaTime);
        if (lingerRemaining <= 0f)
        {
            if (finished != null)
            {
                finished();
            }
        }

    }

    /// <summary>
    /// 다음 기록 프레임의 위치, 애니메이션과 공격을 복원한다.
    /// </summary>
    private void PlayNextFrame()
    {
        EchoFrame frame = recording.Frames[frameIndex++];
        motor.RestoreSnapshot(frame.DisplayState);
        playerAnimator.RestoreSnapshot(frame.AnimationState);

        if (frame.UppercutPressed)
        {
            combat.PerformUppercut();
        }
        else if (frame.AttackPressed)
        {
            combat.PerformAttack(frame.AttackAimDirection);
        }
        if (frameIndex >= recording.Frames.Count)
        {
            BeginLinger();
        }
    }

    /// <summary>
    /// 잔상의 물리와 애니메이션을 멈추고 체류 상태를 시작한다.
    /// </summary>
    private void BeginLinger()
    {
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
        playerAnimator.SetPlaybackSpeed(0f);
        lingerRemaining = lingerDuration;

        if (lingerRemaining <= 0f)
        {
            if (finished != null)
            {
                finished();
            }
        }

    }
}
