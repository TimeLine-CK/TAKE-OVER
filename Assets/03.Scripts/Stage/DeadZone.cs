/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 영역에 닿은 플레이어와 적을 사망 처리한다.
/// </summary>

using UnityEngine;

public class DeadZone : MonoBehaviour
{
    /// <summary>
    /// 플레이어나 적이 영역에 들어오면 체력을 0으로 만든다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        GameObject enteringObject = collision.gameObject;

        if (collision.attachedRigidbody != null)
            enteringObject = collision.attachedRigidbody.gameObject;

        if (!enteringObject.CompareTag("Player") && enteringObject.GetComponent<RobotEnemy>() == null
            && enteringObject.GetComponent<TurretController>() == null)
            return;

        Health playerHealth = enteringObject.GetComponent<Health>();

        if (playerHealth == null)
            return;

        playerHealth.TakeDamage(int.MaxValue, DamageSource.Environment);
    }
}
