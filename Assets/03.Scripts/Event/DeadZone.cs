/// <summary>
/// 스크립트 최종 수정일: 2026-10-13
/// 기능: 영역에 닿은 플레이어에게 치명적인 피해를 적용한다.
/// </summary>

using UnityEngine;

public class DeadZone : MonoBehaviour
{
    /// <summary>
    /// 플레이어가 영역에 들어오면 체력을 소진시켜 사망 이벤트를 발생시킨다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        GameObject enteringObject = collision.gameObject;

        if (collision.attachedRigidbody != null)
            enteringObject = collision.attachedRigidbody.gameObject;

        if (!enteringObject.CompareTag("Player"))
            return;

        Health playerHealth = enteringObject.GetComponent<Health>();

        if (playerHealth == null)
            return;

        playerHealth.TakeDamage(int.MaxValue);
    }
}
