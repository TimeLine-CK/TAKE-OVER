/// <summary>
/// 스크립트 최종 수정일: 2026-09-15
/// 기능: 저장된 잔상 프레임을 물리 틱마다 순서대로 실행한다.
/// </summary>

using System;
using UnityEngine;

public sealed class EchoPlayback : MonoBehaviour
{
    public bool IsRunning => enabled && recording != null && frameIndex < recording.Frames.Count; // 기록 재생 중인지 여부

    private EchoRecording recording; // 재생할 행동 기록
    private PlayerMotor motor; // 기록된 이동을 실행할 컴포넌트
    private PlayerCombat combat; // 기록된 공격을 실행할 컴포넌트
    private Rigidbody2D body; // 재생 종료 시 정지할 리지드바디
    private Action finished; // 재생 완료 시 호출할 콜백
    private int frameIndex; // 다음에 실행할 프레임 위치

    /// <summary>
    /// 재생할 기록과 완료 콜백을 설정하고 필요한 컴포넌트를 초기화한다.
    /// </summary>
    public void Initialize(EchoRecording source, Action onFinished)
    {
        recording = source;
        finished = onFinished;
        motor = GetComponent<PlayerMotor>();
        combat = GetComponent<PlayerCombat>();
        body = GetComponent<Rigidbody2D>();

        if (recording.Frames.Count == 0)
            Finish();
    }

    /// <summary>
    /// 잔상의 속도와 물리 시뮬레이션을 멈춘다.
    /// </summary>
    public void Pause()
    {
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
        enabled = false;
    }

    /// <summary>
    /// 다음 기록 프레임의 이동과 공격을 물리 틱에 맞춰 실행한다.
    /// </summary>
    private void FixedUpdate()
    {
        if (recording == null || frameIndex >= recording.Frames.Count)
            return;

        EchoFrame frame = recording.Frames[frameIndex++];
        motor.SimulateInput(frame.Move, frame.JumpPressed, Time.fixedDeltaTime);
        if (frame.AttackPressed)
            combat.PerformAttack(frame.AttackDirection);

        if (frameIndex < recording.Frames.Count)
            return;

        Finish();
    }

    /// <summary>
    /// 재생을 멈추고 완료 콜백을 호출한다.
    /// </summary>
    private void Finish()
    {
        Pause();
        finished?.Invoke();
    }
}
