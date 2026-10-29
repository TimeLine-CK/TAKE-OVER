/// <summary>
/// 스크립트 최종 수정일: 2026-10-29
/// 기능: 열림 상태에 따라 충돌체와 함께 문을 위아래로 이동한다.
/// </summary>

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class SlidingDoor : MonoBehaviour
{
    [SerializeField, Min(0f)] private float openDistance = 5f; // 문이 위로 올라갈 거리
    [SerializeField, Min(0f)] private float moveSpeed = 2f;    // 문이 이동하는 속도

    public bool IsOpen
    {
        get;
        private set;
    }

    private Rigidbody2D body;
    private Vector2 closedPosition;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        closedPosition = body.position;
    }

    public void SetOpen(bool value)
    {
        IsOpen = value;
    }

    private void FixedUpdate()
    {
        Vector2 target = closedPosition;
        if (IsOpen)
            target += Vector2.up * Mathf.Max(0f, openDistance);
        body.MovePosition(Vector2.MoveTowards(body.position, target, Mathf.Max(0f, moveSpeed) * Time.fixedDeltaTime));
    }
}
