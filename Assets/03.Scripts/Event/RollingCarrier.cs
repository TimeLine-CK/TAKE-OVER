/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 검에 맞은 방향으로 운반대를 굴리고 위의 대상과 전방의 적을 이동시킨다.
/// </summary>

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public sealed class RollingCarrier : MonoBehaviour
{
    [SerializeField] private float hitSpeed = 4f;       // 검에 맞았을 때의 이동 속도
    [SerializeField] private float deceleration = 0.8f; // 초당 감속량
    [SerializeField] private LayerMask obstacleMask;   // 운반대를 멈추는 지형과 전용 턱
    private Rigidbody2D body;
    private BoxCollider2D bodyCollider;
    private float speed;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    private readonly Collider2D[] riders = new Collider2D[16];

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<BoxCollider2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
    }

    /// <summary>
    /// 공격 방향의 수평 성분으로 이동 방향과 초기 속도를 정한다.
    /// </summary>
    public void Hit(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) < 0.01f)
            return;
        speed = Mathf.Sign(direction.x) * Mathf.Max(0f, hitSpeed);
    }

    public void RestoreState()
    {
        speed = 0f;
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 장애물 앞에서 멈추고 접촉한 적과 탑승자를 이동한 뒤 감속한다.
    /// </summary>
    private void FixedUpdate()
    {
        if (Mathf.Abs(speed) < 0.001f)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }
        float distance = speed * Time.fixedDeltaTime;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleMask);
        filter.useTriggers = false;
        int count = bodyCollider.Cast(Vector2.right * Mathf.Sign(speed), filter, hits, Mathf.Abs(distance) + 0.02f);
        for (int index = 0; index < count; index++)
        {
            if (hits[index].normal.x * speed >= 0f)
                continue;
            float allowed = Mathf.Max(0f, hits[index].distance - 0.02f);
            if (allowed >= Mathf.Abs(distance))
                continue;
            distance = Mathf.Sign(distance) * allowed;
            speed = 0f;
        }

        Bounds bounds = bodyCollider.bounds;
        filter.NoFilter();
        filter.useTriggers = false;
        Vector2 riderCenter = new Vector2(bounds.center.x, bounds.max.y + 0.07f);
        count = Physics2D.OverlapBox(riderCenter, new Vector2(bounds.size.x - 0.04f, 0.18f), 0f, filter, riders);
        for (int index = 0; index < count; index++)
        {
            Rigidbody2D rider = riders[index].attachedRigidbody;
            if (rider == null || rider == body || rider.linearVelocity.y > 0.5f)
                continue;
            bool duplicate = false;
            for (int prior = 0; prior < index; prior++)
                if (riders[prior].attachedRigidbody == rider)
                    duplicate = true;
            if (!duplicate)
                rider.position += Vector2.right * distance;
        }

        Vector2 frontCenter = new Vector2(bounds.center.x + Mathf.Sign(distance) * (bounds.extents.x + 0.1f), bounds.center.y);
        count = Physics2D.OverlapBox(frontCenter, new Vector2(0.35f, bounds.size.y), 0f, filter, riders);
        for (int index = 0; index < count; index++)
        {
            RobotEnemy robot = riders[index].GetComponentInParent<RobotEnemy>();
            if (robot != null)
                robot.Push(distance / Time.fixedDeltaTime);
        }
        body.MovePosition(body.position + Vector2.right * distance);
        speed = Mathf.MoveTowards(speed, 0f, Mathf.Max(0f, deceleration) * Time.fixedDeltaTime);
    }
}
