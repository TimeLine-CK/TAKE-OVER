/// <summary>
/// 스크립트 최종 수정일: 2026-10-27
/// 기능: 탄환의 이동 경로를 검사하고 시대가 비활성인 동안 이동과 수명을 보존한다.
/// </summary>

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class TurretProjectile : MonoBehaviour
{
    public float RemainingLifetime => remainingLifetime;
    private Vector2 direction;
    private float speed;
    private int damage;
    private float remainingLifetime;
    private float remainingDistance;
    private LayerMask obstacleMask;
    private bool initialized;
    private bool consumed;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];

    public void Initialize(Vector2 moveDirection, float moveSpeed, int hitDamage, float lifetime,
        float maximumDistance = 12f, LayerMask terrainMask = default)
    {
        direction = moveDirection.normalized;
        speed = Mathf.Max(0f, moveSpeed);
        damage = hitDamage;
        remainingLifetime = Mathf.Max(0f, lifetime);
        remainingDistance = Mathf.Max(0f, maximumDistance);
        obstacleMask = terrainMask;
        if (obstacleMask.value == 0)
            obstacleMask = (1 << 9) | (1 << 10);
        initialized = true;
        consumed = false;
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 이번 프레임의 이동 경로에서 가장 먼저 만난 플레이어나 지형만 처리한다.
    /// </summary>
    private void Update()
    {
        if (!initialized || consumed || Time.deltaTime <= 0f || HitStopController.IsActive || Time.timeScale <= 0f)
            return;
        float deltaTime = Mathf.Min(Time.deltaTime, remainingLifetime);
        float distance = Mathf.Min(speed * deltaTime, remainingDistance);
        ContactFilter2D filter = new();
        filter.SetLayerMask(obstacleMask.value | (1 << LayerMask.NameToLayer("Player")));
        filter.useTriggers = true;
        int count = Physics2D.Raycast(transform.position, direction, filter, hits, distance);
        Collider2D nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int index = 0; index < count; index++)
        {
            Collider2D candidate = hits[index].collider;
            Health target = candidate.GetComponentInParent<Health>();
            bool isPlayer = target != null && target.CompareTag("Player");
            if (candidate.transform.IsChildOf(transform) || (candidate.isTrigger && !isPlayer))
                continue;
            if (!isPlayer && (obstacleMask.value & (1 << candidate.gameObject.layer)) == 0)
                continue;
            if (hits[index].distance >= nearestDistance)
                continue;
            nearest = candidate;
            nearestDistance = hits[index].distance;
        }
        if (nearest != null)
        {
            transform.position += (Vector3)(direction * nearestDistance);
            Hit(nearest);
            return;
        }
        transform.position += (Vector3)(direction * distance);
        remainingLifetime = Mathf.Max(0f, remainingLifetime - deltaTime);
        remainingDistance = Mathf.Max(0f, remainingDistance - distance);
        if (remainingLifetime <= 0f || remainingDistance <= 0f)
            Despawn();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (initialized && !consumed && !HitStopController.IsActive && Time.timeScale > 0f)
            Hit(other);
    }

    private void Hit(Collider2D other)
    {
        Health target = other.GetComponentInParent<Health>();
        if (target != null && target.CompareTag("Player"))
        {
            consumed = true;
            target.TakeDamage(damage, DamageSource.Enemy);
            Despawn();
        }
        else if (!other.isTrigger && (obstacleMask.value & (1 << other.gameObject.layer)) != 0)
            Despawn();
    }

    private void Despawn()
    {
        consumed = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
