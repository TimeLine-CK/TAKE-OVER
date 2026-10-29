/// <summary>
/// 스크립트 최종 수정일: 2026-10-27
/// 기능: 고정 포탑의 발견 대기, 3점사, 피격과 시대 복귀 후 후딜을 처리한다.
/// </summary>

using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Health), typeof(EnemyAwareness))]
public sealed class TurretController : MonoBehaviour
{
    public enum TurretState
    {
        Idle,
        Alert,
        Attack,
        Recover,
        Hit,
        Die
    }

    private static readonly int idleStateHash = Animator.StringToHash("ProtoTurretIdle");
    private static readonly int attackStateHash = Animator.StringToHash("ProtoTurretAttack");
    private static readonly int hitStateHash = Animator.StringToHash("ProtoTurretHit");
    private static readonly int dieStateHash = Animator.StringToHash("ProtoTurretDie");

    [Header("References")]
    [FormerlySerializedAs("aimPivot"), FormerlySerializedAs("muzzlePoint")]
    public Transform MuzzlePoint;
    [FormerlySerializedAs("projectilePrefab")] public GameObject ProjectilePrefab;

    [Header("Detection")]
    [FormerlySerializedAs("detectionRange"), Min(0f)] public float DetectionRange = 12f;
    [Min(0f)] public float DetectionHeight = 2f;
    public LayerMask SightObstacleMask = (1 << 9) | (1 << 10);

    [Header("Attack")]
    [Min(1)] public int BurstCount = 3;
    [Min(0f)] public float ShotInterval = 1f;
    [Min(0f)] public float LastShotWait = 1f;
    [Min(0f)] public float RecoverDuration = 1f;
    [Min(0f)] public float HitDuration = 1f;

    [Header("Projectile")]
    [FormerlySerializedAs("projectileSpeed"), Min(0f)] public float ProjectileSpeed = 12f;
    [FormerlySerializedAs("projectileDamage")] public int ProjectileDamage = 8;
    [FormerlySerializedAs("projectileLifetime"), Min(0f)] public float ProjectileLifetime = 1f;
    [Min(0f)] public float ProjectileMaximumDistance = 12f;
    [FormerlySerializedAs("deathDuration"), Min(0f)] public float DeathDuration = 0.7f;

    public TurretState State => state;

    private Transform player;
    private Health playerHealth;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Health health;
    private Rigidbody2D body;
    private EnemyAwareness awareness;
    private TurretState state;
    private float remaining;
    private int shotsFired;
    private bool wasEnabled;
    private Vector3 burstPosition;
    private Vector2 burstDirection;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        awareness = GetComponent<EnemyAwareness>();
        awareness.ResetDetection();
        health.Damaged += OnDamaged;
        health.Died += Die;
        health.Restored += RestoreState;
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null)
        {
            player = target.transform;
            playerHealth = target.GetComponent<Health>();
        }
        if (MuzzlePoint == null)
            MuzzlePoint = transform;
        state = TurretState.Idle;
        PlayAnimation(idleStateHash);
    }

    private void OnEnable()
    {
        if (wasEnabled && !health.IsDead)
        {
            awareness.ResetDetection();
            EnterState(TurretState.Recover);
        }
        wasEnabled = true;
    }

    private bool CanDetectPlayer()
    {
        if (player == null || !player.gameObject.activeInHierarchy || playerHealth == null || playerHealth.IsDead)
            return false;
        GetMuzzle(out Vector3 origin, out Vector2 direction);
        Vector2 offset = player.position - transform.position;
        return offset.magnitude <= DetectionRange && Mathf.Abs(offset.y) <= DetectionHeight
            && Vector2.Dot(offset, direction) > 0f
            && awareness.HasLineOfSight(origin, player.position, SightObstacleMask);
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f || HitStopController.IsActive || Time.timeScale <= 0f)
            return;
        if (state == TurretState.Die)
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
                gameObject.SetActive(false);
            return;
        }
        if (state == TurretState.Hit || state == TurretState.Recover)
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                if (state == TurretState.Hit)
                    EnterState(TurretState.Recover);
                else
                    EnterState(TurretState.Idle);
            }
            return;
        }
        if (state == TurretState.Attack)
        {
            remaining -= Time.deltaTime;
            if (remaining > 0f)
                return;
            if (shotsFired < Mathf.Max(1, BurstCount))
                FireShot();
            else
                EnterState(TurretState.Recover);
            return;
        }
        // 발견 대기에 들어간 포탑은 대상을 놓쳐도 점사를 끝낸다.
        if (state == TurretState.Idle && !CanDetectPlayer())
        {
            awareness.Observe(false);
            return;
        }
        state = TurretState.Alert;
        if (awareness.Observe(true))
        {
            EnterState(TurretState.Attack);
            GetMuzzle(out burstPosition, out burstDirection);
            FireShot();
        }
    }

    private void FireShot()
    {
        PlayAnimation(attackStateHash);
        SpawnProjectile();
        shotsFired++;
        remaining = ShotInterval;
        if (shotsFired >= Mathf.Max(1, BurstCount))
            remaining = LastShotWait;
    }

    /// <summary>
    /// 점사 간격에 맞춰 고정한 총구에서 탄환 하나를 생성한다.
    /// </summary>
    private void SpawnProjectile()
    {
        if (health.IsDead || ProjectilePrefab == null)
            return;
        GameObject projectileObject = Instantiate(ProjectilePrefab, burstPosition, MuzzlePoint.rotation, transform.parent);
        TurretProjectile projectile = projectileObject.GetComponent<TurretProjectile>();
        if (projectile == null)
        {
            Destroy(projectileObject);
            return;
        }
        projectile.Initialize(burstDirection, ProjectileSpeed, ProjectileDamage, ProjectileLifetime,
            ProjectileMaximumDistance, SightObstacleMask);
    }

    /// <summary>
    /// 스프라이트의 좌우 반전까지 적용해 총구 위치와 발사 방향을 구한다.
    /// </summary>
    private void GetMuzzle(out Vector3 position, out Vector2 direction)
    {
        position = MuzzlePoint.position;
        Vector3 forward = MuzzlePoint.TransformVector(Vector3.left);
        if (spriteRenderer != null && spriteRenderer.flipX)
        {
            Transform spriteTransform = spriteRenderer.transform;
            Vector3 localPosition = spriteTransform.InverseTransformPoint(position);
            Vector3 localDirection = spriteTransform.InverseTransformVector(forward);
            localPosition.x = -localPosition.x;
            localDirection.x = -localDirection.x;
            position = spriteTransform.TransformPoint(localPosition);
            forward = spriteTransform.TransformVector(localDirection);
        }
        direction = ((Vector2)forward).normalized;
    }

    private void OnDamaged(DamageSource source)
    {
        if (!health.IsDead)
        {
            awareness.ResetDetection();
            EnterState(TurretState.Hit);
        }
    }

    private void EnterState(TurretState next)
    {
        state = next;
        shotsFired = 0;
        int animation = idleStateHash;
        if (next == TurretState.Hit)
        {
            remaining = Mathf.Max(0f, HitDuration);
            animation = hitStateHash;
        }
        else if (next == TurretState.Recover)
            remaining = Mathf.Max(0f, RecoverDuration);
        else if (next == TurretState.Die)
        {
            remaining = GetDeathDuration();
            animation = dieStateHash;
        }
        if (next == TurretState.Idle || next == TurretState.Recover)
            awareness.ResetDetection();
        PlayAnimation(animation);
    }

    private void Die()
    {
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>())
            collider.enabled = false;
        awareness.ResetDetection();
        EnterState(TurretState.Die);
    }

    private void RestoreState()
    {
        wasEnabled = gameObject.activeInHierarchy;
        awareness.ResetDetection();
        if (body != null)
            body.simulated = !health.IsDead;
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
            collider.enabled = !health.IsDead;
        if (health.IsDead)
            gameObject.SetActive(false);
        else
            EnterState(TurretState.Idle);
    }

    private float GetDeathDuration()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == "ProtoTurretDie")
                    return clip.length;
        return Mathf.Max(0.01f, DeathDuration);
    }

    private void PlayAnimation(int hash)
    {
        if (animator == null)
            return;
        if (!animator.HasState(0, hash))
            hash = idleStateHash;
        animator.Play(hash, 0, 0f);
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
