/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 터렛 투사체를 이동시키고 플레이어에게 피해를 적용한다.
/// </summary>

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class TurretProjectile : MonoBehaviour
{
    private Vector2 direction; // 투사체 이동 방향
    private float speed;       // 투사체 이동 속도
    private int damage;      // 플레이어에게 적용할 피해량

    /// <summary>
    /// 투사체 이동과 피해 수치를 설정한다.
    /// </summary>
    public void Initialize(Vector2 moveDirection, float moveSpeed, int hitDamage, float lifetime)
    {
        direction = moveDirection.normalized;
        speed = moveSpeed;
        damage = hitDamage;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
    }

    /// <summary>
    /// 플레이어 또는 지형과 충돌하면 투사체를 제거한다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        Health targetHealth = other.GetComponentInParent<Health>();
        if (targetHealth != null && targetHealth.CompareTag("Player"))
        {
            targetHealth.TakeDamage(damage, DamageSource.Enemy);
            Destroy(gameObject);
            return;
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
            Destroy(gameObject);
    }
}
