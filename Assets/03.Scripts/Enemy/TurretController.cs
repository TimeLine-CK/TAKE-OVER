/// <summary>
/// 스크립트 최종 수정일: 2026-10-01
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
    public Transform muzzlePoint;       // 탄환이 생성되는 총구 위치와 방향
    public GameObject projectilePrefab; // 발사할 탄환 프리팹

    [Header("Aim")]
    public float detectionRange = 10f;      // 플레이어 감지 거리
    public float trackingDuration = 1.2f;   // 감지 후 발사 준비 시간
    public float lockedAimDuration = 0.35f; // 발사 직전 대기 시간

    [Header("Projectile")]
    public float projectileSpeed = 12f;         // 투사체 이동 속도
    public float projectileDamage = 20f;        // 투사체 피해량
    public float projectileLifetime = 4f;       // 투사체 유지 시간
    public float deathDuration = 0.7f;          // 사망 애니메이션 표시 시간

    private Transform player;       // 추적할 플레이어
    private Animator animator;      // 터렛 애니메이션 재생기
    private Health health;          // 터렛 체력
    private Rigidbody2D body;       // 터렛 물리 본체
    private Collider2D bodyCollider; // 사망 시 비활성화할 충돌체
    private float trackingTime;     // 현재 추적한 시간
    private float lockedTime;       // 조준 고정 후 지난 시간
    private bool aimLocked;         // 조준 고정 여부
    private bool dead;              // 사망 여부

    /// <summary>
    /// 플레이어와 기본 포신 참조를 찾는다.
    /// </summary>
    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

        if (health != null)
            health.Died += Die;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;

        if (muzzlePoint == null)
            muzzlePoint = transform;

        PlayAnimation(idleStateHash);
    }

    /// <summary>
    /// 감지 범위 안의 플레이어를 추적하고 조준을 고정한 뒤 발사한다.
    /// </summary>
    private void Update()
    {
        if (dead
            || player == null
            || Vector2.Distance(transform.position, player.position) > detectionRange)
        {
            ResetAim();
            return;
        }

        if (!aimLocked)
        {
            trackingTime += Time.deltaTime;
            aimLocked = trackingTime >= trackingDuration;
            return;
        }

        lockedTime += Time.deltaTime;
        if (lockedTime < lockedAimDuration)
            return;

        PlayAttack();
        ResetAim();
    }

    /// <summary>
    /// 공격 애니메이션을 처음부터 재생한다.
    /// </summary>
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
        if (dead || projectilePrefab == null)
            return;

        Vector2 direction = -muzzlePoint.right;
        GameObject projectileObject = Instantiate(
            projectilePrefab,
            muzzlePoint.position,
            muzzlePoint.rotation
        );

        TurretProjectile projectile = projectileObject.GetComponent<TurretProjectile>();
        if (projectile == null)
        {
            Destroy(projectileObject);
            return;
        }

        projectile.Initialize(direction, projectileSpeed, projectileDamage, projectileLifetime);
    }

    /// <summary>
    /// 터렛을 사망 상태로 전환하고 사망 애니메이션이 끝난 뒤 제거한다.
    /// </summary>
    private void Die()
    {
        dead = true;
        ResetAim();

        if (body != null)
            body.simulated = false;

        if (bodyCollider != null)
            bodyCollider.enabled = false;

        PlayAnimation(dieStateHash);
        Destroy(gameObject, deathDuration);
    }

    /// <summary>
    /// 지정한 Animator 상태를 처음부터 재생한다.
    /// </summary>
    private void PlayAnimation(int stateHash)
    {
        if (animator != null)
            animator.Play(stateHash, 0, 0f);
    }

    /// <summary>
    /// 추적과 조준 고정 시간을 초기화한다.
    /// </summary>
    private void ResetAim()
    {
        trackingTime = 0f;
        lockedTime = 0f;
        aimLocked = false;
    }

    /// <summary>
    /// 체력 사망 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        if (health != null)
            health.Died -= Die;
    }
}
