/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 영역에 들어온 카트를 지정한 시작 위치로 복원한다.
/// </summary>

using UnityEngine;

public class CartRespawner : MonoBehaviour
{
    [SerializeField] private Transform CartStartTransform;

    /// <summary>
    /// 카트의 이동 상태를 초기화하고 시작 위치로 옮긴다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        RollingCarrier cart = collision.GetComponentInParent<RollingCarrier>();
        if (cart == null || CartStartTransform == null
            || cart.gameObject.layer != LayerMask.NameToLayer("Transport"))
            return;

        cart.RestoreState();
        Rigidbody2D cartBody = cart.GetComponent<Rigidbody2D>();
        cartBody.position = CartStartTransform.position;
        cartBody.linearVelocity = Vector2.zero;
        cartBody.angularVelocity = 0f;
        Physics2D.SyncTransforms();
    }
}
