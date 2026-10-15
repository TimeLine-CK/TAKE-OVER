/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 범위 안의 플레이어를 감지한 뒤 회전하지 않고 포구 방향으로 투사체를 발사한다.
/// </summary>

using UnityEngine;
using UnityEngine.Serialization;

public sealed class TurretController : MonoBehaviour
{
    private static readonly int idleStateHash = Animator.StringToHash("ProtoTurretIdle");
    private static readonly int attackStateHash = Animator.StringToHash("ProtoTurretAttack");
    private static readonly int dieStateHash = Animator.StringToHash("ProtoTurretDie");

    [Header("References")]
    [FormerlySerializedAs("aimPivot")]
    [FormerlySerializedAs("muzzlePoint")]
    public Transform MuzzlePoint;       // 탄환이 생성되는 총구 위치와 방향
    [FormerlySerializedAs("projectilePrefab")]
    public GameObject ProjectilePrefab; // 발사할 탄환 프리팹

    [Header("Aim")]
    [FormerlySerializedAs("detectionRange")]
    public float DetectionRange = 10f;      // 플레이어 감지 거리
    [FormerlySerializedAs("trackingDuration")]
    public float TrackingDuration = 1.2f;   // 감지 후 발사 준비 시간
    [FormerlySerializedAs("lockedAimDuration")]
    public float LockedAimDuration = 0.35f; // 발사 직전 대기 시간

    [Header("Projectile")]
    [FormerlySerializedAs("projectileSpeed")]
    public float ProjectileSpeed = 12f;         // 투사체 이동 속도
    [FormerlySerializedAs("projectileDamage")]
    public int ProjectileDamage = 20;           // 투사체 피해량
    [FormerlySerializedAs("projectileLifetime")]
    public float ProjectileLifetime = 4f;       // 투사체 유지 시간
    [FormerlySerializedAs("deathDuration")]
    public float DeathDuration = 0.7f;          // 사망 애니메이션 표시 시간

    private Transform player;       // 추적할 플레이어
    private Animator animator;      // 터렛 애니메이션 재생기
    private SpriteRenderer spriteRenderer; // 포탑의 좌우 반전 상태
    private Health health;          // 터렛 체력
    private Rigidbody2D body;       // 터렛 물리 본체
    private Collider2D bodyCollider; // 사망 시 비활성화할 충돌체
    private float trackingTime;     // 현재 추적한 시간
    private float lockedTime;       // 조준 고정 후 지난 시간
    private bool isAimLocked;       // 조준 고정 여부
    private bool isDead;            // 사망 여부
    private EnemyAwareness awareness;

    /// <summary>
    /// 플레이어와 기본 포신 참조를 찾는다.
    /// </summary>
    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        awareness = GetComponent<EnemyAwareness>();

        if (health != null)
        {
            health.Died += Die;
            health.Restored += RestoreState;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;

        if (MuzzlePoint == null)
            MuzzlePoint = transform;

        PlayAnimation(idleStateHash);
    }

    /// <summary>
    /// 감지 범위 안의 플레이어를 추적하고 조준을 고정한 뒤 발사한다.
    /// </summary>
    private void Update()
    {
        if (isDead
            || player == null || !player.gameObject.activeInHierarchy
            || Vector2.Distance(transform.position, player.position) > DetectionRange)
        {
            ResetAim();
            awareness?.Observe(false);
            return;
        }

        if (awareness != null && !awareness.Observe(true))
            return;

        if (!isAimLocked)
        {
            trackingTime += Time.deltaTime;
            isAimLocked = trackingTime >= TrackingDuration;
            return;
        }

        lockedTime += Time.deltaTime;
        if (lockedTime < LockedAimDuration)
            return;

        PlayAttack();
        ResetAim();
    }

    private void PlayAttack()
    {
        if (animator != null)
            PlayAnimation(attackStateHash);
        else
            SpawnProjectile();
    }

    /// <summary>
    /// 공격 애니메이션의 발사 시점에 고정된 포구 방향으로 탄환 하나를 생성한다.
    /// </summary>
    public void SpawnProjectile()
    {
        if (isDead || ProjectilePrefab == null)
            return;
        if (awareness != null && !awareness.IsReady)
            return;

        Vector3 spawnPosition = MuzzlePoint.position;
        Vector3 fireDirection = MuzzlePoint.TransformVector(Vector3.left);
        if (spriteRenderer != null && spriteRenderer.flipX)
        {
            Transform spriteTransform = spriteRenderer.transform;
            Vector3 localPosition = spriteTransform.InverseTransformPoint(spawnPosition);
            Vector3 localDirection = spriteTransform.InverseTransformVector(fireDirection);
            localPosition.x = -localPosition.x;
            localDirection.x = -localDirection.x;
            spawnPosition = spriteTransform.TransformPoint(localPosition);
            fireDirection = spriteTransform.TransformVector(localDirection);
        }
        Vector2 direction = fireDirection;
        GameObject projectileObject = Instantiate(
            ProjectilePrefab,
            spawnPosition,
            MuzzlePoint.rotation
        );
        projectileObject.transform.SetParent(transform.parent, true);

        TurretProjectile projectile = projectileObject.GetComponent<TurretProjectile>();
        if (projectile == null)
        {
            Destroy(projectileObject);
            return;
        }

        projectile.Initialize(direction, ProjectileSpeed, ProjectileDamage, ProjectileLifetime);
    }

    /// <summary>
    /// 터렛의 사망 애니메이션을 재생한 뒤 복원을 위해 비활성화한다.
    /// </summary>
    private void Die()
    {
        isDead = true;
        ResetAim();

        if (body != null)
            body.simulated = false;

        if (bodyCollider != null)
            bodyCollider.enabled = false;

        PlayAnimation(dieStateHash);
        awareness?.ResetDetection();
        Invoke(nameof(HideAfterDeath), Mathf.Max(0.01f, DeathDuration));
    }

    private void HideAfterDeath()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 저장된 생사 상태를 물리와 애니메이션에 반영한다.
    /// </summary>
    private void RestoreState()
    {
        CancelInvoke(nameof(HideAfterDeath));
        isDead = health.IsDead;
        ResetAim();
        awareness?.ResetDetection();
        if (body != null)
            body.simulated = !isDead;
        if (bodyCollider != null)
            bodyCollider.enabled = !isDead;
        if (isDead)
            gameObject.SetActive(false);
        else
            PlayAnimation(idleStateHash);
    }

    private void OnDisable()
    {
        ResetAim();
        awareness?.ResetDetection();
    }

    /// <summary>
    /// 다른 시대에서 돌아오면 이전 공격 모션 대신 대기 상태로 시작한다.
    /// </summary>
    private void OnEnable()
    {
        ResetAim();
        if (!isDead)
            PlayAnimation(idleStateHash);
    }

    private void PlayAnimation(int stateHash)
    {
        if (animator != null)
            animator.Play(stateHash, 0, 0f);
    }

    private void ResetAim()
    {
        trackingTime = 0f;
        lockedTime = 0f;
        isAimLocked = false;
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
