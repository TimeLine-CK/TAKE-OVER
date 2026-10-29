/// <summary>
/// 스크립트 최종 수정일: 2026-10-29
/// 기능: 과거에서 두 지점 사이를 왕복하고 현재에서는 마지막 위치에 멈추며 탑승자를 함께 이동시킨다.
/// </summary>

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class MovingPlatform : MonoBehaviour
{
    public Transform StartPoint; // 이동을 시작할 지점
    public Transform EndPoint;   // 반대쪽 끝 지점
    public Health PlayerHealth;  // 이동 여부를 확인할 플레이어 체력
    [Min(0f)] public float MoveSpeed = 3f; // 초당 이동 거리

    private Rigidbody2D body;
    public bool MovingToEnd => movingToEnd;

    private Collider2D bodyCollider;
    private TimeShiftController timeShift;
    private Vector2 startPosition;
    private Vector2 endPosition;
    private Vector2 previousVelocity;
    private bool movingToEnd = true;
    private readonly Collider2D[] riders = new Collider2D[16];

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.useFullKinematicContacts = true;
    }

    private void Start()
    {
        if (PlayerHealth == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                PlayerHealth = player.GetComponent<Health>();
        }
        if (StartPoint == null || EndPoint == null || PlayerHealth == null
            || bodyCollider == null || bodyCollider.isTrigger)
        {
            Debug.LogError("MovingPlatform의 시작점, 종료점, 플레이어 체력과 일반 콜라이더를 확인하세요.", this);
            enabled = false;
            return;
        }

        // 지점이 플랫폼의 자식이어도 이동 범위가 따라 움직이지 않게 좌표를 보관한다.
        startPosition = StartPoint.position;
        endPosition = EndPoint.position;
        timeShift = PlayerHealth.GetComponent<TimeShiftController>();
        body.position = startPosition;
    }

    public void RestoreState(bool wasMovingToEnd)
    {
        movingToEnd = wasMovingToEnd;
        previousVelocity = Vector2.zero;
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (PlayerHealth == null || PlayerHealth.IsDead || !PlayerHealth.gameObject.activeInHierarchy
            || Time.timeScale <= 0f
            || (timeShift != null && (!timeShift.IsReady || timeShift.CurrentFlag != TimeFlag.Past)))
        {
            previousVelocity = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 target = startPosition;
        if (movingToEnd)
            target = endPosition;
        Vector2 next = Vector2.MoveTowards(body.position, target, Mathf.Max(0f, MoveSpeed) * Time.fixedDeltaTime);
        Vector2 movement = next - body.position;
        if (movement.sqrMagnitude > 0f)
        {
            MoveRiders(movement);
            body.MovePosition(next);
        }
        else
            body.linearVelocity = Vector2.zero;

        previousVelocity = movement / Time.fixedDeltaTime;
        if ((next - target).sqrMagnitude < 0.000001f)
            movingToEnd = !movingToEnd;
    }

    /// <summary>
    /// 플랫폼 위에 서 있는 물리 객체를 옮기며 점프해 올라가는 대상은 제외한다.
    /// </summary>
    private void MoveRiders(Vector2 movement)
    {
        Bounds bounds = bodyCollider.bounds;
        ContactFilter2D filter = ContactFilter2D.noFilter;
        filter.useTriggers = false;
        Vector2 center = new(bounds.center.x, bounds.max.y + 0.07f);
        Vector2 size = new(Mathf.Max(0.01f, bounds.size.x - 0.04f), 0.18f);
        int count = Physics2D.OverlapBox(center, size, 0f, filter, riders);
        for (int index = 0; index < count; index++)
        {
            Rigidbody2D rider = riders[index].attachedRigidbody;
            if (rider == null || rider == body || rider.transform.IsChildOf(transform))
                continue;
            PlayerMotor motor = rider.GetComponent<PlayerMotor>();
            if (motor != null)
            {
                if (!motor.IsGrounded && rider.linearVelocity.y > 0.5f)
                    continue;
            }
            else if (rider.linearVelocity.y - Mathf.Max(0f, previousVelocity.y) > 0.5f)
                continue;
            RobotEnemy robot = rider.GetComponent<RobotEnemy>();
            if (robot != null && robot.IsFlying)
                continue;
            bool duplicate = false;
            for (int prior = 0; prior < index; prior++)
                if (riders[prior].attachedRigidbody == rider)
                    duplicate = true;
            if (!duplicate)
                rider.position += movement;
        }
    }

    private void OnDisable()
    {
        previousVelocity = Vector2.zero;
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }
}
