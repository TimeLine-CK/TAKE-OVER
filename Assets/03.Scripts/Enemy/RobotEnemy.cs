/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 인간형 로봇의 대기, 추적, 공격과 사망 상태를 처리한다.
/// </summary>

using UnityEngine;
using UnityEngine.Serialization;

public class RobotEnemy : MonoBehaviour
{
    private static readonly int idleStateHash = Animator.StringToHash("ProtoRobotIdle");
    private static readonly int runStateHash = Animator.StringToHash("ProtoRobotRun");
    private static readonly int attackStateHash = Animator.StringToHash("ProtoRobotAttack");
    private static readonly int dieStateHash = Animator.StringToHash("ProtoRobotDie");

    private enum RobotState { Idle, Run, Attack, Dead }

    [Header("Detection")]
    [FormerlySerializedAs("detectionRange")]
    public float DetectionRange = 7f;          // 플레이어 감지 거리
    [FormerlySerializedAs("attackRange")]
    public float AttackRange = 1.4f;           // 공격 시작 거리

    [Header("Movement")]
    [FormerlySerializedAs("moveSpeed")]
    public float MoveSpeed = 3f;               // 추적 이동 속도

    [Header("Combat")]
    [FormerlySerializedAs("attackDamage")]
    public int AttackDamage = 15;                       // 공격 피해량
    [FormerlySerializedAs("attackInterval")]
    public float AttackInterval = 1.2f;                 // 공격 사이의 대기 시간
    [FormerlySerializedAs("attackWindup")]
    [Min(0f)] public float AttackWindup = 0.25f;        // 공격 시작부터 피해 판정까지의 시간
    [FormerlySerializedAs("attackHeight")]
    [Min(0f)] public float AttackHeight = 1.2f;         // 공격 가능한 대상과의 높이 차이
    [FormerlySerializedAs("deathDuration")]
    public float DeathDuration = 0.8f;                  // 사망 표시 유지 시간

    private Rigidbody2D body;                           // 로봇 이동 리지드바디
    private Collider2D bodyCollider;                    // 사망 시 비활성화할 콜라이더
    private SpriteRenderer spriteRenderer;              // 상태를 표시할 렌더러
    private Animator animator;                          // 로봇 애니메이션 재생기
    private Health health;                              // 로봇 체력
    private Transform player;                           // 추적할 플레이어
    private Health playerHealth;                        // 공격할 플레이어 체력
    private RobotState state = (RobotState)(-1);        // 현재 로봇 상태
    private float nextAttackTime;                       // 다음 공격 가능 시간
    private float attackHitTime;                        // 현재 공격의 피해 판정 시각
    private bool isAttackPending;                      // 피해 판정 대기 여부
    private float attackDirection;                     // 공격 시작 당시 바라본 방향
    private EnemyAwareness awareness;
    private float pushedUntil;

    /// <summary>
    /// 로봇 컴포넌트와 플레이어 참조를 초기화한다.
    /// </summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<Health>();
        awareness = GetComponent<EnemyAwareness>();

        if (health != null)
        {
            health.Died += Die;
            health.Restored += RestoreState;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerHealth = playerObject.GetComponent<Health>();
        }

        SetState(RobotState.Idle);
    }

    private void OnDisable()
    {
        isAttackPending = false;
        awareness?.ResetDetection();
    }

    /// <summary>
    /// 운반대에 밀리는 동안 추적 이동이 물리적인 밀림을 덮어쓰지 않게 한다.
    /// </summary>
    public void Push(float horizontalSpeed)
    {
        if (health.IsDead)
            return;
        pushedUntil = Time.time + 0.15f;
        isAttackPending = false;
        body.linearVelocity = new Vector2(horizontalSpeed, body.linearVelocity.y);
    }

    /// <summary>
    /// 저장된 체력에 맞춰 적의 물리와 애니메이션을 복원한다.
    /// </summary>
    private void RestoreState()
    {
        CancelInvoke(nameof(HideAfterDeath));
        isAttackPending = false;
        pushedUntil = 0f;
        nextAttackTime = Time.time;
        body.simulated = !health.IsDead;
        bodyCollider.enabled = !health.IsDead;
        body.linearVelocity = Vector2.zero;
        awareness?.ResetDetection();
        state = (RobotState)(-1);
        if (health.IsDead)
        {
            SetState(RobotState.Dead);
            gameObject.SetActive(false);
        }
        else
            SetState(RobotState.Idle);
    }

    /// <summary>
    /// 플레이어와의 거리에 따라 대기, 추적 또는 공격 상태를 실행한다.
    /// </summary>
    private void Update()
    {
        if (state == RobotState.Dead || player == null || !player.gameObject.activeInHierarchy)
        {
            return;
        }

        float distance = Mathf.Abs(player.position.x - transform.position.x);
        if (Time.time < pushedUntil)
            return;
        if (awareness != null && !awareness.Observe(distance <= DetectionRange))
        {
            isAttackPending = false;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            SetState(RobotState.Idle);
            return;
        }

        if (isAttackPending)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            if (Time.time >= attackHitTime)
            {
                isAttackPending = false;
                float horizontalOffset = player.position.x - transform.position.x;
                if (distance <= AttackRange
                    && Mathf.Abs(player.position.y - transform.position.y) <= AttackHeight
                    && horizontalOffset * attackDirection >= 0f)
                {
                    playerHealth?.TakeDamage(AttackDamage, DamageSource.Enemy);
                }
            }
            return;
        }

        if (distance > DetectionRange)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            SetState(RobotState.Idle);
            return;
        }

        if (distance > AttackRange)
        {
            float direction = Mathf.Sign(player.position.x - transform.position.x);
            body.linearVelocity = new Vector2(direction * MoveSpeed, body.linearVelocity.y);
            SetFacingDirection(direction);
            SetState(RobotState.Run);
            return;
        }

        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        if (Mathf.Abs(player.position.y - transform.position.y) > AttackHeight)
        {
            SetState(RobotState.Idle);
            return;
        }
        SetFacingDirection(player.position.x - transform.position.x);
        SetState(RobotState.Attack);

        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + Mathf.Max(AttackInterval, AttackWindup);
            attackHitTime = Time.time + AttackWindup;
            attackDirection = player.position.x >= transform.position.x ? 1f : -1f;
            isAttackPending = true;
            PlayAnimation(attackStateHash);
        }
    }

    /// <summary>
    /// 사망 상태로 전환하고 애니메이션이 끝나면 복원을 위해 비활성화한다.
    /// </summary>
    private void Die()
    {
        isAttackPending = false;
        SetState(RobotState.Dead);
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
        bodyCollider.enabled = false;
        awareness?.ResetDetection();
        Invoke(nameof(HideAfterDeath), Mathf.Max(0.01f, DeathDuration));
    }

    private void HideAfterDeath()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 상태를 변경하고 상태에 맞는 애니메이션을 재생한다.
    /// </summary>
    private void SetState(RobotState nextState)
    {
        if (state == nextState)
        {
            return;
        }

        state = nextState;

        int stateHash = idleStateHash;
        if (state == RobotState.Run)
            stateHash = runStateHash;
        else if (state == RobotState.Dead)
            stateHash = dieStateHash;

        PlayAnimation(stateHash);
    }

    private void PlayAnimation(int stateHash)
    {
        if (animator != null)
        {
            animator.Play(stateHash, 0, 0f);
        }
    }

    private void SetFacingDirection(float direction)
    {
        if (spriteRenderer != null && Mathf.Abs(direction) > 0.01f)
        {
            spriteRenderer.flipX = direction > 0f;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= Die;
            health.Restored -= RestoreState;
        }
    }
}
