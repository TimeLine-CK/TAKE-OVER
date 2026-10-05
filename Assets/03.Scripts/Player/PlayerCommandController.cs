/// <summary>
/// 스크립트 최종 수정일: 2026-09-29
/// 기능: 구르기, 돌진, 올려치기 입력과 TP 소비를 처리한다.
/// </summary>

using UnityEngine;

[RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor), typeof(PlayerCombat))]
public sealed class PlayerCommandController : MonoBehaviour
{
    [Header("Roll")]
    [Min(0f)] public float rollSpeed = 14f;                     // 구르기 이동 속도
    [Min(0.01f)] public float rollDuration = 0.25f;             // 구르기 유지 시간

    [Header("Dash Command")]
    [Min(0f)] public float dashPointCost = 15f;                 // 돌진 TP 소비량
    [Min(0f)] public float dashSpeed = 20f;                     // 돌진 이동 속도
    [Min(0.01f)] public float dashDuration = 0.18f;             // 돌진 유지 시간

    [Header("Uppercut Command")]
    [Min(0f)] public float uppercutPointCost = 20f;             // 올려치기 TP 소비량
    [Min(0.01f)] public float uppercutDuration = 0.3f;          // 올려치기 커맨드 판정 시간

    public bool IsCommandActive => motor != null
        && (motor.IsCommandActive || uppercutRemaining > 0f);   // 커맨드 동작 진행 여부

    private PlayerInputReader inputReader;                      // 행동 입력 제공자
    private PlayerMotor motor;                                  // 이동 동작 실행 컴포넌트
    private PlayerCombat combat;                                // 올려치기 공격 실행 컴포넌트
    private TPPointController pointController;                  // TP Point 제공자
    private float uppercutRemaining;                            // 올려치기 커맨드 남은 시간

    /// <summary>
    /// 커맨드 실행에 필요한 플레이어 컴포넌트를 찾는다.
    /// </summary>
    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        motor = GetComponent<PlayerMotor>();
        combat = GetComponent<PlayerCombat>();
        pointController = GetComponent<TPPointController>();
    }

    /// <summary>
    /// 구르기와 커맨드 입력을 소비해 플레이어 동작으로 변환한다.
    /// </summary>
    private void Update()
    {
        uppercutRemaining = Mathf.Max(0f, uppercutRemaining - Time.deltaTime);

        if (inputReader.ConsumeRollPressed())
        {
            TryRoll(motor);
        }


        if (inputReader.ConsumeDashPressed())
        {
            TryDash(motor);
        }


        if (inputReader.ConsumeUppercutPressed())
        {
            TryUppercut(combat);
        }

    }

    /// <summary>
    /// 지정한 이동 컴포넌트로 구르기를 실행한다.
    /// </summary>
    public bool TryRoll(PlayerMotor targetMotor)
    {
        int direction = GetInputDirection(targetMotor);

        if (!targetMotor.TryRoll(direction, rollSpeed, rollDuration))
            return false;

        targetMotor.GetComponentInChildren<PlayerAnimator>()?.PlayRoll();
        return true;
    }

    /// <summary>
    /// TP를 소비하고 지정한 이동 컴포넌트로 돌진을 실행한다.
    /// </summary>
    public bool TryDash(PlayerMotor targetMotor)
    {
        int direction = GetInputDirection(targetMotor);

        if (pointController == null
            || pointController.CurrentPoint < dashPointCost
            || !targetMotor.TryDash(direction, dashSpeed, dashDuration))
        {
            return false;
        }

        return pointController.TryUse(dashPointCost);
    }

    /// <summary>
    /// TP를 소비하고 지정한 대상에서 올려치기를 실행한다.
    /// </summary>
    public bool TryUppercut(PlayerCombat targetCombat)
    {
        if (pointController == null
            || pointController.CurrentPoint < uppercutPointCost)
        {
            return false;
        }

        if (!pointController.TryUse(uppercutPointCost))
        {
            return false;
        }


        if (targetCombat == combat)
        {
            uppercutRemaining = uppercutDuration;
        }

        targetCombat.PerformUppercut();
        return true;
    }

    /// <summary>
    /// 현재 수평 입력이 없다면 대상이 바라보는 방향을 반환한다.
    /// </summary>
    private int GetInputDirection(PlayerMotor targetMotor)
    {
        return Mathf.Abs(inputReader.Move.x) > 0.01f
            ? (int)Mathf.Sign(inputReader.Move.x)
            : targetMotor.FacingDirection;
    }
}
