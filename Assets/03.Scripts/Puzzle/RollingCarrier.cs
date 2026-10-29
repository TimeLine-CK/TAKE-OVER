/// <summary>
/// 스크립트 최종 수정일: 2026-10-29
/// 기능: 몸으로 밀거나 공격한 운반대를 이동하고 낙하와 탑승자 이동을 처리한다.
/// </summary>

using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
[DefaultExecutionOrder(100)]
public sealed class RollingCarrier : MonoBehaviour
{
    [SerializeField] private float hitSpeed = 4f;       // 검에 맞았을 때의 이동 속도
    [SerializeField] private float deceleration = 0.8f; // 초당 감속량
    [SerializeField, Min(0f)] private float pushSpeed = 1.5f; // 몸으로 밀 때의 이동 속도
    [SerializeField] private LayerMask obstacleMask;   // 운반대를 멈추는 지형과 전용 턱
    [Min(0f)] public float LaunchThreshold = 1.5f;
    [Min(0)] public int CollisionDamage = 18;
    public Vector2 LaunchSpeedRange = new(16f, 22f);
    public float LaunchVerticalSpeed = 9f;
    private Rigidbody2D body;
    private BoxCollider2D bodyCollider;
    private PlayerInputReader playerInput;
    private Collider2D playerCollider;
    private Health playerHealth;
    private float speed;
    private float verticalSpeed;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    private readonly Collider2D[] riders = new Collider2D[16];
    private readonly HashSet<RobotEnemy> struckRobots = new();

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<BoxCollider2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerInput = player.GetComponent<PlayerInputReader>();
            playerCollider = player.GetComponent<Collider2D>();
            playerHealth = player.GetComponent<Health>();
        }
    }

    /// <summary>
    /// 공격 방향의 수평 성분으로 이동 방향과 초기 속도를 정한다.
    /// </summary>
    public void Hit(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) < 0.01f)
            return;
        float nextDirection = Mathf.Sign(direction.x);
        if (Mathf.Abs(speed) < 0.001f || Mathf.Sign(speed) != nextDirection)
            struckRobots.Clear();
        speed = Mathf.Sign(direction.x) * Mathf.Max(0f, hitSpeed);
    }

    public void RestoreState()
    {
        speed = 0f;
        verticalSpeed = 0f;
        struckRobots.Clear();
        if (body != null)
            body.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 지형에 맞춰 이동과 낙하를 처리하고 접촉한 적과 탑승자를 이동한다.
    /// </summary>
    private void FixedUpdate()
    {
        float impactSpeed = speed;
        float movementSpeed = speed;
        if (Mathf.Abs(movementSpeed) < 0.001f)
            movementSpeed = GetPushSpeed();
        float distance = movementSpeed * Time.fixedDeltaTime;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleMask);
        filter.useTriggers = false;
        int count = bodyCollider.Cast(Vector2.right * Mathf.Sign(movementSpeed), filter, hits, Mathf.Abs(distance) + 0.02f);
        for (int index = 0; index < count; index++)
        {
            if (hits[index].normal.x * movementSpeed >= 0f)
                continue;
            float allowed = Mathf.Max(0f, hits[index].distance - 0.02f);
            if (allowed >= Mathf.Abs(distance))
                continue;
            distance = Mathf.Sign(distance) * allowed;
            speed = 0f;
        }

        verticalSpeed += Physics2D.gravity.y * body.gravityScale * Time.fixedDeltaTime;
        float fallDistance = verticalSpeed * Time.fixedDeltaTime;
        if (fallDistance < 0f)
        {
            count = bodyCollider.Cast(Vector2.down, filter, hits, -fallDistance + 0.02f);
            for (int index = 0; index < count; index++)
            {
                if (hits[index].normal.y <= 0f)
                    continue;
                float allowed = Mathf.Max(0f, hits[index].distance - 0.02f);
                if (allowed >= -fallDistance)
                    continue;
                fallDistance = -allowed;
                verticalSpeed = 0f;
            }
        }
        Vector2 movement = new Vector2(distance, fallDistance);

        Bounds bounds = bodyCollider.bounds;
        filter = ContactFilter2D.noFilter;
        filter.useTriggers = false;
        Vector2 riderCenter = new Vector2(bounds.center.x, bounds.max.y + 0.07f);
        count = Physics2D.OverlapBox(riderCenter, new Vector2(bounds.size.x - 0.04f, 0.18f), 0f, filter, riders);
        for (int index = 0; index < count; index++)
        {
            Rigidbody2D rider = riders[index].attachedRigidbody;
            if (rider == null || rider == body || rider.linearVelocity.y > 0.5f)
                continue;
            RobotEnemy ridingRobot = rider.GetComponent<RobotEnemy>();
            if (ridingRobot != null && ridingRobot.IsFlying)
                continue;
            bool duplicate = false;
            for (int prior = 0; prior < index; prior++)
                if (riders[prior].attachedRigidbody == rider)
                    duplicate = true;
            if (!duplicate)
                rider.position += movement;
        }

        Vector2 frontCenter = new Vector2(bounds.center.x + Mathf.Sign(impactSpeed)
            * (bounds.extents.x + Mathf.Abs(distance) * 0.5f + 0.1f), bounds.center.y);
        count = Physics2D.OverlapBox(frontCenter, new Vector2(0.35f + Mathf.Abs(distance), bounds.size.y), 0f, filter, riders);
        for (int index = 0; index < count; index++)
        {
            RobotEnemy robot = riders[index].GetComponentInParent<RobotEnemy>();
            if (robot == null || Mathf.Abs(impactSpeed) < LaunchThreshold || distance == 0f)
                continue;
            Health health = robot.GetComponent<Health>();
            if (health == null || health.IsDead || !struckRobots.Add(robot))
                continue;
            health.TakeDamage(Mathf.Min(Mathf.Max(0, CollisionDamage), health.CurrentHealth - 1), DamageSource.Environment);
            float horizontalSpeed = Mathf.Sign(impactSpeed) * Mathf.Clamp(Mathf.Abs(impactSpeed),
                Mathf.Min(LaunchSpeedRange.x, LaunchSpeedRange.y), Mathf.Max(LaunchSpeedRange.x, LaunchSpeedRange.y));
            float floor = riders[index].bounds.min.y;
            robot.Launch(new Vector2(horizontalSpeed, LaunchVerticalSpeed), floor);
            HitStopController.Request(HitStopType.Strong);
        }
        body.MovePosition(body.position + movement);
        speed = Mathf.MoveTowards(speed, 0f, Mathf.Max(0f, deceleration) * Time.fixedDeltaTime);
    }

    /// <summary>
    /// 플레이어가 카트 옆면에 붙어 카트 쪽으로 입력할 때만 미는 속도를 반환한다.
    /// </summary>
    private float GetPushSpeed()
    {
        if (playerInput == null || !playerInput.isActiveAndEnabled
            || playerCollider == null || !playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy
            || (playerHealth != null && playerHealth.IsDead))
            return 0f;

        float input = Mathf.Clamp(playerInput.Move.x, -1f, 1f);
        if (Mathf.Abs(input) <= 0.01f)
            return 0f;

        Bounds cartBounds = bodyCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;
        float verticalOverlap = Mathf.Min(cartBounds.max.y, playerBounds.max.y)
            - Mathf.Max(cartBounds.min.y, playerBounds.min.y);
        if (verticalOverlap <= 0.05f || (cartBounds.center.x - playerBounds.center.x) * input <= 0f)
            return 0f;

        float gap = cartBounds.min.x - playerBounds.max.x;
        if (input < 0f)
            gap = playerBounds.min.x - cartBounds.max.x;
        if (gap < -0.08f || gap > 0.08f)
            return 0f;

        return input * Mathf.Max(0f, pushSpeed);
    }
}
