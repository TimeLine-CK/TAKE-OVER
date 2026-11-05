/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 수위를 따라 발판과 탑승자를 옮기고 위쪽 지형에 닿으면 상승을 멈춘다.
/// </summary>

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public sealed class FloatingPlatform : MonoBehaviour
{
    public SewerWaterArea WaterArea;
    [Min(0f)] public float MoveSpeed = 2f;
    [Min(0f)] public float SurfaceOffset;
    public LayerMask ObstacleMask = (1 << 9) | (1 << 10);

    private Rigidbody2D body;
    private BoxCollider2D surface;
    private float restHeight;
    private Vector2 previousVelocity;
    private readonly RaycastHit2D[] obstacles = new RaycastHit2D[16];
    private readonly Collider2D[] riders = new Collider2D[16];

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        surface = GetComponent<BoxCollider2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.useFullKinematicContacts = true;
        restHeight = body.position.y;
    }

    /// <summary>
    /// 수면까지 발판을 옮기되 위쪽 지형과 탑승자의 위치를 함께 처리한다.
    /// </summary>
    private void FixedUpdate()
    {
        if (WaterArea == null)
            return;

        float target = Mathf.Max(restHeight,
            WaterArea.SurfaceY + surface.bounds.extents.y + SurfaceOffset
            - (surface.bounds.center.y - body.position.y));
        float next = Mathf.MoveTowards(body.position.y, target, Mathf.Max(0f, MoveSpeed) * Time.fixedDeltaTime);
        Vector2 movement = new Vector2(0f, next - body.position.y);
        if (movement.y > 0f)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(ObstacleMask);
            filter.useTriggers = false;
            int count = surface.Cast(Vector2.up, filter, obstacles, movement.y + 0.02f);
            for (int i = 0; i < count; i++)
            {
                if (obstacles[i].normal.y < -0.01f)
                    movement.y = Mathf.Min(movement.y, Mathf.Max(0f, obstacles[i].distance - 0.02f));
            }
        }
        if (movement.sqrMagnitude > 0f)
        {
            MovingPlatform.MoveRiders(surface, body, movement, previousVelocity, riders);
            body.MovePosition(body.position + movement);
        }
        else
        {
            body.linearVelocity = Vector2.zero;
        }
        previousVelocity = movement / Time.fixedDeltaTime;
    }

    private void OnDisable()
    {
        previousVelocity = Vector2.zero;
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }
}
