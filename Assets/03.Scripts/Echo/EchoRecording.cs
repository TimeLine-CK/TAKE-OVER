/// <summary>
/// 스크립트 최종 수정일: 2026-09-15
/// 기능: 한 잔상의 시작 상태와 60틱 행동 프레임을 최대 8초까지 저장한다.
/// </summary>

using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EchoRecording
{
    public const int MaximumTickCount = 480; // 8초 동안 저장할 수 있는 최대 프레임 수

    public int Id { get; } // 잔상 기록 식별 번호
    public int ZoneId { get; } // 기록이 생성된 구역 번호
    public PlayerMotor.MotorSnapshot StartState { get; } // 녹화 시작 당시 이동 상태
    public IReadOnlyList<EchoFrame> Frames => frames; // 저장된 행동 프레임 목록 
    public float Duration => frames.Count / 60f; // 기록된 행동의 재생 시간

    private readonly List<EchoFrame> frames = new(MaximumTickCount); // 순서대로 기록된 행동 프레임

    /// <summary>
    /// 기록 번호, 구역 번호와 녹화 시작 상태를 저장한다.
    /// </summary>
    public EchoRecording(
        int id,
        int zoneId,
        PlayerMotor.MotorSnapshot startState
    )
    {
        Id = id;
        ZoneId = zoneId;
        StartState = startState;
    }

    /// <summary>
    /// 최대 길이를 넘지 않았다면 행동 프레임을 기록에 추가.
    /// </summary>
    public bool TryAddFrame(EchoFrame frame)
    {
        if (frames.Count >= MaximumTickCount)
            return false;

        frames.Add(frame);
        return true;
    }
}

public readonly struct EchoFrame
{
    public int Tick { get; } // 기록된 물리 틱 번호
    public Vector2 Move { get; } // 기록된 이동 입력
    public bool JumpPressed { get; } // 기록된 점프 입력 여부
    public bool AttackPressed { get; } // 기록된 공격 입력 여부
    public int AttackDirection { get; } // 기록된 공격 방향
    public PlayerMotor.MotorSnapshot DisplayState { get; } // 기록된 이동 상태
    public PlayerAnimator.AnimationSnapshot AnimationState { get; } // 기록된 애니메이션 상태

    /// <summary>
    /// 한 물리 틱에 기록된 입력, 방향, 이동과 애니메이션 상태를 저장.
    /// </summary>
    public EchoFrame(
        int tick,
        Vector2 move,
        bool jumpPressed,
        bool attackPressed,
        int attackDirection,
        PlayerMotor.MotorSnapshot displayState,
        PlayerAnimator.AnimationSnapshot animationState
    )
    {
        Tick = tick;
        Move = move;
        JumpPressed = jumpPressed;
        AttackPressed = attackPressed;
        AttackDirection = attackDirection;
        DisplayState = displayState;
        AnimationState = animationState;
    }
}
