/// <summary>
/// 스크립트 최종 수정일: 2026-09-30
/// 기능: 인간형 로봇의 대기, 추적, 공격과 사망 상태를 처리한다.
/// </summary>

using UnityEngine;

public class RobotEnemy : MonoBehaviour
{
    private static readonly int idleStateHash = Animator.StringToHash("ProtoRobotIdle");
    private static readonly int runStateHash = Animator.StringToHash("ProtoRobotRun");
    private static readonly int attackStateHash = Animator.StringToHash("ProtoRobotAttack");
    private static readonly int dieStateHash = Animator.StringToHash("ProtoRobotDie");

    private enum RobotState { Idle, Run, Attack, Dead }

    [Header("Detection")]
    public float detectionRange = 7f;          // 플레이어 감지 거리
    public float attackRange = 1.4f;           // 공격 시작 거리

    [Header("Movement")]
    public float moveSpeed = 3f;               // 추적 이동 속도

    [Header("Combat")]
    public float attackDamage = 15f;                    // 공격 피해량
    public float attackInterval = 1.2f;                 // 공격 사이의 대기 시간
    [Min(0f)] public float attackWindup = 0.25f;        // 공격 시작부터 피해 판정까지의 시간
    [Min(0f)] public float attackHeight = 1.2f;         // 공격 가능한 대상과의 높이 차이
    public float deathDuration = 0.8f;                  // 사망 표시 유지 시간

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
    private bool attackPending;                        // 피해 판정 대기 여부
    private float attackDirection;                     // 공격 시작 당시 바라본 방향

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

        if (health != null)
        {
            health.Died += Die;
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

    /// <summary>
    /// 플레이어와의 거리에 따라 대기, 추적 또는 공격 상태를 실행한다.
    /// </summary>
    private void Update()
    {
        if (state == RobotState.Dead || player == null)
        {
            return;
        }

        float distance = Mathf.Abs(player.position.x - transform.position.x);

        if (attackPending)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            if (Time.time >= attackHitTime)
            {
                attackPending = false;
                float horizontalOffset = player.position.x - transform.position.x;
                if (distance <= attackRange
                    && Mathf.Abs(player.position.y - transform.position.y) <= attackHeight
                    && horizontalOffset * attackDirection >= 0f)
                {
                    playerHealth?.TakeDamage(attackDamage);
                }
            }
            return;
        }

        if (distance > detectionRange)
        {
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
            SetState(RobotState.Idle);
            return;
        }

        if (distance > attackRange)
        {
            float direction = Mathf.Sign(player.position.x - transform.position.x);
            body.linearVelocity = new Vector2(direction * moveSpeed, body.linearVelocity.y);
            SetFacingDirection(direction);
            SetState(RobotState.Run);
            return;
        }

        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        if (Mathf.Abs(player.position.y - transform.position.y) > attackHeight)
        {
            SetState(RobotState.Idle);
            return;
        }
        SetFacingDirection(player.position.x - transform.position.x);
        SetState(RobotState.Attack);

        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + Mathf.Max(attackInterval, attackWindup);
            attackHitTime = Time.time + attackWindup;
            attackDirection = player.position.x >= transform.position.x ? 1f : -1f;
            attackPending = true;
            PlayAnimation(attackStateHash);
        }
    }

    /// <summary>
    /// 사망 상태로 전환하고 잠시 뒤 로봇을 제거한다.
    /// </summary>
    private void Die()
    {
        attackPending = false;
        SetState(RobotState.Dead);
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
        bodyCollider.enabled = false;
        Destroy(gameObject, deathDuration);
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

        int stateHash = state switch
        {
            RobotState.Run => runStateHash,
            RobotState.Dead => dieStateHash,
            _ => idleStateHash
        };

        PlayAnimation(stateHash);
    }

    /// <summary>
    /// 지정한 Animator 상태를 처음부터 재생한다.
    /// </summary>
    private void PlayAnimation(int stateHash)
    {
        if (animator != null)
        {
            animator.Play(stateHash, 0, 0f);
        }
    }

    /// <summary>
    /// 로봇 스프라이트가 이동 또는 공격 대상을 바라보게 한다.
    /// </summary>
    private void SetFacingDirection(float direction)
    {
        if (spriteRenderer != null && Mathf.Abs(direction) > 0.01f)
        {
            spriteRenderer.flipX = direction > 0f;
        }
    }

    /// <summary>
    /// 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= Die;
        }
    }
}
