/// <summary>
/// 스크립트 최종 수정일: 2026-09-15
/// 기능: 플레이어의 이동과 점프 상태를 Animator 파라미터와 공격 트리거에 반영.
/// </summary>

using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private PlayerMotor motor; // 이동 및 접지 상태를 제공하는 컴포넌트
    [SerializeField] private PlayerInputReader inputReader; // 이동 입력을 제공하는 컴포넌트
    private Animator animator; // 캐릭터 애니메이션을 제어하는 Animator

    private readonly int isGroundedHash = Animator.StringToHash("IsGrounded"); // 접지 파라미터 해시
    private readonly int speedHash = Animator.StringToHash("Speed"); // 이동 속도 파라미터 해시
    private readonly int verticalVelocityHash = Animator.StringToHash("VerticalVelocity"); // 수직 속도 파라미터 해시
    private readonly int attackHash = Animator.StringToHash("Attack"); // 공격 트리거 해시
    private float idleTimer; // 대기 애니메이션 전환까지 남은 시간
    private const float IdleGraceTime = 0.05f; // 0.05초 동안은 달리기 상태 유지


    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (motor == null)
            motor = GetComponentInParent<PlayerMotor>();

        if (inputReader == null)
            inputReader = GetComponentInParent<PlayerInputReader>();
    }

    /// <summary>
    /// 이동, 접지, 수직 속도를 Animator 파라미터에 반영.
    /// </summary>
    private void Update()
    {
        float currentSpeed = Mathf.Abs(motor.Velocity.x);
        float inputX = Mathf.Abs(inputReader.Move.x);

        if (inputX > 0.1f || currentSpeed > 0.1f)
        {
            idleTimer = IdleGraceTime;
        }
        else
        {
            float deltaTime = animator.updateMode == AnimatorUpdateMode.UnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            idleTimer -= deltaTime;
        }

        animator.SetFloat(speedHash, idleTimer > 0f ? 1f : 0f);
        animator.SetBool(isGroundedHash, motor.IsGrounded);
        animator.SetFloat(verticalVelocityHash, motor.Velocity.y);
    }

    /// <summary>
    /// 공격 트리거를 초기화한 뒤 다시 활성화.
    /// </summary>
    public void PlayAttack()
    {
        animator.ResetTrigger(attackHash);
        animator.SetTrigger(attackHash);
    }

    /// <summary>
    /// 현재 애니메이션 상태와 재생 위치를 저장.
    /// </summary>
    public AnimationSnapshot CaptureSnapshot()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        return new AnimationSnapshot(state.fullPathHash, state.normalizedTime);
    }

    /// <summary>
    /// 저장된 애니메이션 상태와 재생 위치를 복구.
    /// </summary>
    public void RestoreSnapshot(AnimationSnapshot snapshot)
    {
        animator.ResetTrigger(attackHash);
        animator.Play(snapshot.StateHash, 0, snapshot.NormalizedTime);
        animator.Update(0f);
    }

    /// <summary>
    /// Animator의 전체 재생 속도를 설정.
    /// </summary>
    public void SetPlaybackSpeed(float speed)
    {
        animator.speed = speed;
    }

    public readonly struct AnimationSnapshot
    {
        public int StateHash { get; } // 저장된 Animator 상태 해시
        public float NormalizedTime { get; } // 저장된 애니메이션 재생 위치

        /// <summary>
        /// 복구에 필요한 애니메이션 상태와 재생 위치를 저장한다.
        /// </summary>
        public AnimationSnapshot(int stateHash, float normalizedTime)
        {
            StateHash = stateHash;
            NormalizedTime = normalizedTime;
        }
    }
}
