/// <summary>
/// 스크립트 최종 수정일: 2026-10-27
/// 기능: 근접 경비의 발견, 추적, 공격, 피격과 운반대 충돌 후 비행을 처리한다.
/// </summary>

using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Health), typeof(Rigidbody2D), typeof(EnemyAwareness))]
public class RobotEnemy : MonoBehaviour
{
    public enum RobotState
    {
        Idle,
        Alert,
        Move,
        Attack,
        Recover,
        Hit,
        Die,
        Flying
    }

    private static readonly int idleStateHash = Animator.StringToHash("ProtoRobotIdle");
    private static readonly int runStateHash = Animator.StringToHash("ProtoRobotRun");
    private static readonly int attackStateHash = Animator.StringToHash("ProtoRobotAttack");
    private static readonly int hitStateHash = Animator.StringToHash("ProtoRobotHit");
    private static readonly int dieStateHash = Animator.StringToHash("ProtoRobotDie");

    [Header("Detection")]
    [FormerlySerializedAs("detectionRange"), Min(0f)] public float DetectionRange = 12f;
    [FormerlySerializedAs("attackRange"), Min(0f)] public float AttackRange = 2f;
    public LayerMask SightObstacleMask = (1 << 9) | (1 << 10);

    [Header("Movement")]
    [FormerlySerializedAs("moveSpeed"), Min(0f)] public float MoveSpeed = 3f;
    [Min(0f)] public float StopDistance = 1f;
    public Collider2D MovementArea; // 추적할 수 있는 구역
    [Min(0f)] public float BoundaryMargin = 1f;

    [Header("Combat")]
    [FormerlySerializedAs("attackDamage")] public int AttackDamage = 50;
    [FormerlySerializedAs("AttackWindup"), FormerlySerializedAs("attackWindup"), Min(0f)]
    public float AttackDuration = 1f;
    [FormerlySerializedAs("AttackInterval"), FormerlySerializedAs("attackInterval"), Min(0f)]
    public float RecoverDuration = 1f;
    [Min(0f)] public float HitDuration = 1f;
    [Min(0f)] public float ReturnAttackDelay = 1f;
    [FormerlySerializedAs("deathDuration"), Min(0f)] public float DeathDuration = 0.8f;

    [Header("Carrier")]
    public float FlightGravity = 39.24f;
    public float FlightDuration = 3f;
    public float FallKillDistance = 4f;
    public float FlightAttackDelay = 1f;

    public RobotState State => state;
    public bool IsFlying => state == RobotState.Flying;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Health health;
    private Transform player;
    private Health playerHealth;
    private EnemyAwareness awareness;
    private RobotState state = (RobotState)(-1);
    private float remaining;
    private float attackDelay;
    private float attackDirection;
    private bool hasHit;
    private bool wasEnabled;
    private Vector2 savedVelocity;
    private Vector2 launchVelocity;
    private Vector2 startPosition;
    private float originalGravity;
    private float launchFloor;
    private float flightRemaining;
    private bool flightPending;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<Health>();
        awareness = GetComponent<EnemyAwareness>();
        awareness.ResetDetection();
        originalGravity = body.gravityScale;
        startPosition = body.position;
        health.Damaged += OnDamaged;
        health.Died += Die;
        health.Restored += RestoreState;
        if (animator != null)
            animator.keepAnimatorStateOnDisable = true;
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null)
        {
            player = target.transform;
            playerHealth = target.GetComponent<Health>();
        }
        EnterState(RobotState.Idle);
    }

    private void OnEnable()
    {
        if (wasEnabled)
        {
            attackDelay = Mathf.Max(attackDelay, ReturnAttackDelay);
            body.linearVelocity = savedVelocity;
        }
        wasEnabled = true;
    }

    private void OnDisable()
    {
        if (body != null)
            savedVelocity = body.linearVelocity;
    }

    private bool CanDetectPlayer()
    {
        return player != null && player.gameObject.activeInHierarchy && playerHealth != null
            && !playerHealth.IsDead && Vector2.Distance(body.position, player.position) <= DetectionRange;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f || HitStopController.IsActive || Time.timeScale <= 0f)
            return;
        if (state == RobotState.Die)
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
                gameObject.SetActive(false);
            return;
        }
        attackDelay = Mathf.Max(0f, attackDelay - Time.deltaTime);
        if (state == RobotState.Flying)
        {
            UpdateFlight();
            return;
        }

        bool detected = CanDetectPlayer();
        bool waiting = state == RobotState.Hit || state == RobotState.Recover;
        bool ready = awareness.Observe(detected, !waiting);
        if (waiting)
        {
            StopMoving();
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                if (state == RobotState.Hit)
                    EnterState(RobotState.Recover);
                else
                    ResumeTracking(detected, ready);
            }
            return;
        }
        if (state == RobotState.Attack)
        {
            StopMoving();
            TryHitPlayer();
            if (Time.timeScale <= 0f || state != RobotState.Attack)
                return;
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
                EnterState(RobotState.Recover);
            return;
        }
        if (!detected)
        {
            EnterState(RobotState.Idle);
            StopMoving();
            return;
        }
        if (!ready)
        {
            EnterState(RobotState.Alert);
            StopMoving();
            return;
        }

        float offset = player.position.x - body.position.x;
        float distance = Vector2.Distance(body.position, player.position);
        SetFacingDirection(offset);
        if (distance <= AttackRange && attackDelay <= 0f
            && awareness.HasLineOfSight(body.position, player.position, SightObstacleMask))
        {
            attackDirection = Mathf.Sign(offset);
            if (attackDirection == 0f)
                attackDirection = 1f;
            EnterState(RobotState.Attack);
            StopMoving();
            return;
        }
        EnterState(RobotState.Move);
        float velocity = 0f;
        if (Mathf.Abs(offset) > StopDistance)
            velocity = Mathf.Sign(offset) * Mathf.Max(0f, MoveSpeed);
        body.linearVelocity = new Vector2(velocity, body.linearVelocity.y);
    }

    private void FixedUpdate()
    {
        if (HitStopController.IsActive)
            return;
        if (state == RobotState.Die || state == RobotState.Flying || body.linearVelocity.x == 0f)
            return;
        float minimum = startPosition.x - DetectionRange;
        float maximum = startPosition.x + DetectionRange;
        if (MovementArea != null)
        {
            minimum = MovementArea.bounds.min.x;
            maximum = MovementArea.bounds.max.x;
        }
        float margin = Mathf.Min(Mathf.Max(0f, BoundaryMargin), (maximum - minimum) * 0.5f);
        float velocity = body.linearVelocity.x;
        float distance = maximum - margin - body.position.x;
        if (velocity < 0f)
            distance = body.position.x - minimum - margin;
        float allowedSpeed = Mathf.Max(0f, distance) / Time.fixedDeltaTime;
        velocity = Mathf.Sign(velocity) * Mathf.Min(Mathf.Abs(velocity), allowedSpeed);
        body.linearVelocity = new Vector2(velocity, body.linearVelocity.y);
    }

    private void ResumeTracking(bool detected, bool ready)
    {
        if (!detected)
            EnterState(RobotState.Idle);
        else if (!ready)
            EnterState(RobotState.Alert);
        else
            EnterState(RobotState.Move);
    }

    /// <summary>
    /// 휘두르기를 시작한 방향의 살아 있는 플레이어에게 한 번만 피해를 준다.
    /// </summary>
    private void TryHitPlayer()
    {
        if (hasHit || player == null || !player.gameObject.activeInHierarchy || playerHealth == null || playerHealth.IsDead)
            return;
        Vector2 offset = (Vector2)player.position - body.position;
        if (offset.magnitude > AttackRange || offset.x * attackDirection < 0f
            || !awareness.HasLineOfSight(body.position, player.position, SightObstacleMask))
            return;
        hasHit = true;
        playerHealth.TakeDamage(AttackDamage, DamageSource.Enemy);
    }

    private void OnDamaged(DamageSource source)
    {
        if (health.IsDead)
            return;
        if (state != RobotState.Flying)
        {
            StopMoving();
            EnterState(RobotState.Hit);
        }
    }

    /// <summary>
    /// 운반대 충돌 직후에는 위치를 유지하고 히트스탑이 끝나면 비행을 시작한다.
    /// </summary>
    public void Launch(Vector2 velocity, float floorHeight)
    {
        if (health.IsDead)
            return;
        launchVelocity = velocity;
        launchFloor = floorHeight;
        flightRemaining = Mathf.Max(0f, FlightDuration);
        flightPending = true;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        EnterState(RobotState.Flying);
    }

    private void UpdateFlight()
    {
        if (flightPending)
        {
            flightPending = false;
            body.gravityScale = Mathf.Max(0f, FlightGravity) / Mathf.Max(0.001f, Mathf.Abs(Physics2D.gravity.y));
            body.linearVelocity = launchVelocity;
        }
        flightRemaining -= Time.deltaTime;
        if (body.position.y < launchFloor - Mathf.Max(0f, FallKillDistance))
        {
            health.TakeDamage(health.CurrentHealth, DamageSource.Environment);
            return;
        }
        if (flightRemaining <= 0f)
        {
            body.gravityScale = originalGravity;
            StopMoving();
            attackDelay = Mathf.Max(attackDelay, FlightAttackDelay);
            ResumeTracking(CanDetectPlayer(), awareness.IsReady);
        }
    }

    private void Die()
    {
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>())
            collider.enabled = false;
        awareness.ResetDetection();
        EnterState(RobotState.Die);
    }

    private void RestoreState()
    {
        flightPending = false;
        flightRemaining = 0f;
        attackDelay = 0f;
        wasEnabled = gameObject.activeInHierarchy;
        savedVelocity = Vector2.zero;
        body.gravityScale = originalGravity;
        body.simulated = !health.IsDead;
        body.linearVelocity = Vector2.zero;
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
            collider.enabled = !health.IsDead;
        awareness.ResetDetection();
        if (health.IsDead)
            gameObject.SetActive(false);
        else
        {
            state = (RobotState)(-1);
            EnterState(RobotState.Idle);
        }
    }

    private void StopMoving()
    {
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void EnterState(RobotState next)
    {
        if (state == next && next != RobotState.Hit)
            return;
        state = next;
        int animation = idleStateHash;
        if (next == RobotState.Move)
            animation = runStateHash;
        else if (next == RobotState.Attack)
        {
            remaining = Mathf.Max(0f, AttackDuration);
            hasHit = false;
            animation = attackStateHash;
        }
        else if (next == RobotState.Hit)
        {
            remaining = Mathf.Max(0f, HitDuration);
            animation = hitStateHash;
        }
        else if (next == RobotState.Recover)
            remaining = Mathf.Max(0f, RecoverDuration);
        else if (next == RobotState.Die)
        {
            remaining = GetDeathDuration();
            animation = dieStateHash;
        }
        else if (next == RobotState.Flying)
            animation = hitStateHash;
        if (animator != null)
        {
            animator.speed = 1f;
            if (next == RobotState.Attack && AttackDuration > 0f && animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name == "ProtoRobotAttack")
                        animator.speed = clip.length / AttackDuration;
            if (!animator.HasState(0, animation))
                animation = idleStateHash;
            animator.Play(animation, 0, 0f);
        }
    }

    private float GetDeathDuration()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == "ProtoRobotDie")
                    return clip.length;
        return Mathf.Max(0.01f, DeathDuration);
    }

    private void SetFacingDirection(float direction)
    {
        if (spriteRenderer != null && Mathf.Abs(direction) > 0.01f)
            spriteRenderer.flipX = direction > 0f;
    }

    private void OnDestroy()
    {
        if (health == null)
            return;
        health.Damaged -= OnDamaged;
        health.Died -= Die;
        health.Restored -= RestoreState;
    }
}
