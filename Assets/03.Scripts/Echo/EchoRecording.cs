/// <summary>
/// 스크립트 최종 수정일: 2026-09-29
/// 기능: 한 잔상의 시작 상태와 60틱 행동 프레임을 설정된 시간만큼 저장한다.
/// </summary>

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EchoRecording
{
    public int Id { get; }                                      // 잔상 기록 식별 번호 프로퍼티
    public int ZoneId { get; }                                  // 기록이 생성된 구역 번호 프로퍼티
    public PlayerMotor.MotorSnapshot StartState { get; }        // 녹화 시작 당시 이동 상태 프로퍼티
    public IReadOnlyList<EchoFrame> Frames => frames;           // 저장된 행동 프레임 목록
    public float Duration => frames.Count / 60f;                // 기록된 행동의 재생 시간
    public int MaximumTickCount { get; }                        // 저장할 수 있는 최대 프레임 수

    private readonly List<EchoFrame> frames;                    // 순서대로 기록된 행동 프레임

    /// <summary>
    /// 기록 번호, 구역 번호와 녹화 시작 상태를 저장한다.
    /// </summary>
    public EchoRecording(int id, int zoneId, PlayerMotor.MotorSnapshot startState, int maximumTickCount)
    {
        Id = id;
        ZoneId = zoneId;
        StartState = startState;
        MaximumTickCount = Mathf.Max(1, maximumTickCount);
        frames = new List<EchoFrame>(MaximumTickCount);
    }

    /// <summary>
    /// 최대 길이를 넘지 않았다면 행동 프레임을 기록에 추가.
    /// </summary>
    public bool TryAddFrame(EchoFrame frame)
    {
        if (frames.Count >= MaximumTickCount)
        {
            return false;
        }

        frames.Add(frame);
        return true;
    }
}

public readonly struct EchoFrame
{
    public int Tick { get; }                                            // 기록된 물리 틱 번호 프로퍼티
    public Vector2 Move { get; }                                        // 기록된 이동 입력 프로퍼티
    public bool JumpPressed { get; }                                    // 기록된 점프 입력 여부 프로퍼티
    public bool AttackPressed { get; }                                  // 기록된 공격 입력 여부 프로퍼티
    public bool RollPressed { get; }                                    // 기록된 구르기 입력 여부 프로퍼티
    public bool DashPressed { get; }                                    // 기록된 돌진 입력 여부 프로퍼티
    public bool UppercutPressed { get; }                                // 기록된 올려치기 입력 여부 프로퍼티
    public int AttackDirection { get; }                                 // 기록된 공격 방향 프로퍼티
    public Vector2 AttackAimDirection { get; }                          // 기록된 마우스 조준 방향 프로퍼티
    public PlayerMotor.MotorSnapshot DisplayState { get; }              // 기록된 이동 상태 프로퍼티
    public PlayerAnimator.AnimationSnapshot AnimationState { get; }     // 기록된 애니메이션 상태 프로퍼티

    /// <summary>
    /// 한 물리 틱에 기록된 입력, 방향, 이동과 애니메이션 상태를 저장.
    /// </summary>
    public EchoFrame(
        int tick,
        Vector2 move,
        bool jumpPressed,
        bool attackPressed,
        bool rollPressed,
        bool dashPressed,
        bool uppercutPressed,
        int attackDirection,
        Vector2 attackAimDirection,
        PlayerMotor.MotorSnapshot displayState,
        PlayerAnimator.AnimationSnapshot animationState
    )
    {
        Tick = tick;
        Move = move;
        JumpPressed = jumpPressed;
        AttackPressed = attackPressed;
        RollPressed = rollPressed;
        DashPressed = dashPressed;
        UppercutPressed = uppercutPressed;
        AttackDirection = attackDirection;
        AttackAimDirection = attackAimDirection;
        DisplayState = displayState;
        AnimationState = animationState;
    }
}
