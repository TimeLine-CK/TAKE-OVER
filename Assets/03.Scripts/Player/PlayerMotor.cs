/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 플레이어의 수평 이동, 점프, 구르기, 중력, 접지와 모서리 보정을 처리한다.
/// </summary>

using System;
using UnityEngine;

public sealed class PlayerMotor : MonoBehaviour
{
    public event Action Jumped;                                         // 점프가 실행됐음을 전달하는 이벤트

    [Header("References")]
    [SerializeField] private Transform visualRoot;                      // 좌우 반전할 플레이어
    [SerializeField] private LayerMask groundMask;                      // 바닥으로 판정할 레이어
    [SerializeField] private bool startFacingLeft;                      // 씬 시작 시 왼쪽을 바라볼지 여부
    [SerializeField] private bool allowFallingBelowBounds;              // 낙사 구역으로 떨어질 수 있게 하단 제한을 해제

    [Header("Ground Movement")]
    [SerializeField] private float maximumSpeed = 8f;                   // 최대 수평 이동 속도
    [SerializeField] private float groundAcceleration = 100f;           // 지상 가속도
    [SerializeField] private float groundDeceleration = 133.33f;        // 지상 감속도
    [SerializeField] private float groundReverseAcceleration = 133.33f; // 지상 반대 방향 가속도

    [Header("Air Movement")]
    [SerializeField] private float airAcceleration = 64f;       // 공중 가속도
    [SerializeField] private float airDeceleration = 32f;       // 공중 감속도

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 16f;          // 점프 시작 속도
    [SerializeField] private float risingGravity = 45.71f;      // 상승 중 중력 가속도
    [SerializeField] private float fallingGravity = 89.6f;      // 하강 중 중력 가속도
    [SerializeField] private float maximumFallSpeed = 24f;      // 최대 낙하 속도
    [SerializeField] private float jumpBufferDuration = 0.08f;  // 선입력 허용 시간
    [SerializeField] private float coyoteTimeDuration = 0.06f;  // 낭떠러지 점프 허용 시간

    [Header("Roll")]
    [SerializeField] private float rollSpeed = 14f;             // 구르기 이동 속도
    [SerializeField] private float rollDuration = 0.25f;        // 구르기 유지 시간

    [Header("Ceiling Check")]
    [SerializeField] private float ceilingCheckDistance = 0.04f; // 상승 중 천장 확인 거리

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.05f;         // 바닥 탐지 거리
    [SerializeField] private float groundCheckWidthReduction = 0.08f;   // 바닥 표시 폭 축소값

    [Header("Corner Correction")]
    [SerializeField] private float maximumCornerCorrection = 0.2f;      // 모서리 보정 최대 거리
    [SerializeField] private float cornerCorrectionStep = 0.02f;        // 모서리 보정 탐색 간격
    [SerializeField] private float collisionSkin = 0.02f;               // 충돌 검사 여유값

    public bool IsGrounded => isGrounded;                                       // 현재 접지 여부
    public int FacingDirection => facingDirection;                              // 현재 바라보는 수평 방향
    public Vector2 Velocity => new(body.linearVelocity.x, verticalVelocity);    // 현재 이동 속도

    private Rigidbody2D body;                                                   // 물리 이동을 적용할 리지드바디
    private Collider2D bodyCollider;                                            // 접지와 충돌 검사에 사용할 콜라이더
    private PlayerInputReader inputReader;                                      // 플레이어 입력을 제공하는 컴포넌트
    private PlayerAnimator playerAnimator;                                      // 구르기 애니메이션을 실행할 컴포넌트
    private CameraFollow2D cameraFollow;                                        // 이동 경계를 제공하는 카메라
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];           // 접지 검사 결과 버퍼
    private readonly RaycastHit2D[] ceilingHits = new RaycastHit2D[4];          // 천장 검사 결과 버퍼
    private readonly Collider2D[] overlapHits = new Collider2D[8];             // 모서리 보정의 겹침 검사 버퍼
    private ContactFilter2D groundFilter;                                       // 바닥 레이어만 검사하는 필터

    private Vector3 visualBaseScale;                                            // 캐릭터 시각 요소의 기본 크기

    private bool isGrounded;                                                    // 현재 바닥에 닿아 있는지 여부
    private int facingDirection = 1;                                            // 현재 바라보는 방향

    private float jumpBufferRemaining;                                          // 남아 있는 점프 선입력 시간
    private float coyoteTimeRemaining;                                          // 남아 있는 코요테 타임
    private float verticalVelocity;                                             // 직접 계산하는 수직 속도
    private bool hasJumpedThisPhysicsTick;                                      // 현재 물리 틱에서 점프했는지 여부
    private float rollRemaining;                                                // 구르기 남은 시간
    private float rollHorizontalVelocity;                                       // 구르기 중 유지할 수평 속도

    /// <summary>
    /// 컴포넌트와 접지 필터, 시각 요소의 기본 상태를 초기화.
    /// </summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        inputReader = GetComponent<PlayerInputReader>();
        playerAnimator = GetComponentInChildren<PlayerAnimator>();
        if (Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow2D>();

        body.freezeRotation = true;
        body.gravityScale = 0f;
        verticalVelocity = body.linearVelocity.y;

        // 플레이어 자신의 Collider가 바닥으로 검출되지 않게 설정
        groundMask &= ~(1 << gameObject.layer);
        groundFilter.SetLayerMask(groundMask);
        groundFilter.useTriggers = false;

        if (visualRoot != null)
        {
            visualBaseScale = visualRoot.localScale;
        }
        if (startFacingLeft)
            facingDirection = -1;
        ApplyVisualFacing();

        Debug.Assert(
            groundMask.value != 0,
            "PlayerMotor의 Ground Mask가 설정되지 않았습니다.",
            this
        );
    }

    /// <summary>
    /// 부활할 때 남아 있는 이동 속도와 점프 및 구르기 상태를 초기화한다.
    /// </summary>
    public void ResetMovement()
    {
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        verticalVelocity = 0f;
        jumpBufferRemaining = 0f;
        coyoteTimeRemaining = 0f;
        rollRemaining = 0f;
        rollHorizontalVelocity = 0f;
        hasJumpedThisPhysicsTick = false;
        isGrounded = false;
    }

    private void FixedUpdate()
    {
        SimulateInput(
            inputReader.Move,
            inputReader.ConsumeJumpPressed(),
            Time.fixedDeltaTime
        );
    }

    /// <summary>
    /// 전달받은 입력과 시간 간격을 사용해 한 번의 이동 물리를 계산.
    /// </summary>
    private void SimulateInput(Vector2 move, bool isJumpPressed, float deltaTime)
    {
        hasJumpedThisPhysicsTick = false;
        float horizontalInput = move.x;

        UpdateGroundState(deltaTime);
        UpdateJumpBuffer(isJumpPressed, deltaTime);

        if (inputReader.ConsumeRollPressed())
            TryRoll(horizontalInput);

        Vector2 velocity = body.linearVelocity;
        velocity.y = verticalVelocity;

        if (rollRemaining > 0f)
        {
            velocity.x = rollHorizontalVelocity;
        }
        else
        {
            ApplyFacing(horizontalInput);
            ApplyHorizontalMovement(ref velocity, horizontalInput, deltaTime);
            TryJump(ref velocity);
        }

        StopRisingAtCeiling(ref velocity);
        float verticalDistance = ApplyGravity(ref velocity, deltaTime);
        verticalVelocity = velocity.y;
        velocity.y = verticalDistance / deltaTime;

        ApplyCornerCorrection(velocity, deltaTime);
        ApplyCameraBounds(ref velocity, deltaTime);

        body.linearVelocity = velocity;
        UpdateRoll(deltaTime);
    }

    /// <summary>
    /// 카메라의 맵 경계 안에 콜라이더 전체가 들어오도록 위치와 이동 속도를 제한한다.
    /// </summary>
    private void ApplyCameraBounds(ref Vector2 velocity, float deltaTime)
    {
        if (cameraFollow == null || cameraFollow.CameraBounds == null)
            return;

        Bounds worldBounds = cameraFollow.CameraBounds.bounds;
        Bounds playerBounds = bodyCollider.bounds;
        Vector2 offset = (Vector2)playerBounds.center - body.position;
        Vector2 center = (Vector2)worldBounds.center - offset;
        Vector2 halfSize = Vector2.Max(Vector2.zero,
            (Vector2)worldBounds.extents - (Vector2)playerBounds.extents);
        Vector2 minimum = center - halfSize;
        Vector2 maximum = center + halfSize;
        if (allowFallingBelowBounds)
            minimum.y = float.NegativeInfinity;
        Vector2 position = body.position;
        Vector2 clampedPosition = new(
            Mathf.Clamp(position.x, minimum.x, maximum.x),
            Mathf.Clamp(position.y, minimum.y, maximum.y)
        );
        if (position != clampedPosition)
            body.position = clampedPosition;

        Vector2 limitedVelocity = new(
            Mathf.Clamp(velocity.x, (minimum.x - clampedPosition.x) / deltaTime,
                (maximum.x - clampedPosition.x) / deltaTime),
            Mathf.Clamp(velocity.y, (minimum.y - clampedPosition.y) / deltaTime,
                (maximum.y - clampedPosition.y) / deltaTime)
        );
        if (!Mathf.Approximately(velocity.y, limitedVelocity.y))
            verticalVelocity = 0f;
        velocity = limitedVelocity;
    }

    /// <summary>
    /// 지상에서 지정한 방향으로 구르기를 시작한다.
    /// </summary>
    private void TryRoll(float horizontalInput)
    {
        if (!isGrounded || rollRemaining > 0f)
            return;

        if (Mathf.Abs(horizontalInput) > 0.01f)
            facingDirection = (int)Mathf.Sign(horizontalInput);
        rollHorizontalVelocity = facingDirection * Mathf.Max(0f, rollSpeed);
        rollRemaining = Mathf.Max(0.01f, rollDuration);
        ApplyVisualFacing();
        playerAnimator?.PlayRoll();
    }

    /// <summary>
    /// 바닥 충돌을 검사하고 접지 및 코요테 타임을 갱신한다.
    /// </summary>
    private void UpdateGroundState(float deltaTime)
    {
        bool wasGrounded = isGrounded;
        int hitCount = bodyCollider.Cast(
            Vector2.down,
            groundFilter,
            groundHits,
            groundCheckDistance
        );
        bool isGroundDetected = false;
        for (int index = 0; index < hitCount; index++)
        {
            if (CanPassFromBelow(groundHits[index].collider)
                && bodyCollider.bounds.min.y < groundHits[index].collider.bounds.max.y - collisionSkin * 2f)
                continue;
            if (groundHits[index].normal.y > 0.5f)
            {
                isGroundDetected = true;
                break;
            }
        }

        if (verticalVelocity > 0f)
        {
            isGroundDetected = false;
        }

        if (verticalVelocity > 0f && body.linearVelocity.y <= 0f)
        {
            verticalVelocity = 0f;
        }

        isGrounded = isGroundDetected;

        if (isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = 0f;
            }

            coyoteTimeRemaining = coyoteTimeDuration;
        }
        else if (wasGrounded)
        {
            coyoteTimeRemaining = coyoteTimeDuration;
        }
        else
        {
            coyoteTimeRemaining = Mathf.Max(0f, coyoteTimeRemaining - deltaTime);
        }
    }

    /// <summary>
    /// 점프 입력을 버퍼에 저장하거나 남은 시간을 감소시킨다.
    /// </summary>
    private void UpdateJumpBuffer(bool isJumpPressed, float deltaTime)
    {
        if (isJumpPressed)
        {
            jumpBufferRemaining = jumpBufferDuration;
            return;
        }

        jumpBufferRemaining = Mathf.Max(
            0f,
            jumpBufferRemaining - deltaTime
        );
    }

    private void UpdateRoll(float deltaTime)
    {
        if (rollRemaining <= 0f)
            return;

        rollRemaining = Mathf.Max(0f, rollRemaining - deltaTime);
    }

    /// <summary>
    /// 올라가다가 천장에 닿으면 상승 속도를 0으로 만든다.
    /// </summary>
    private void StopRisingAtCeiling(ref Vector2 velocity)
    {
        if (velocity.y <= 0f)
        {
            return;
        }

        int hitCount = bodyCollider.Cast(
            Vector2.up,
            groundFilter,
            ceilingHits,
            ceilingCheckDistance
        );

        for (int index = 0; index < hitCount; index++)
        {
            if (CanPassFromBelow(ceilingHits[index].collider))
                continue;
            if (ceilingHits[index].normal.y < -0.5f)
            {
                verticalVelocity = 0f;
                velocity.y = 0f;
                return;
            }
        }
    }

    /// <summary>
    /// 지상과 공중 상태에 맞는 가속도로 수평 속도를 계산한다.
    /// </summary>
    private void ApplyHorizontalMovement(
        ref Vector2 velocity,
        float horizontalInput,
        float deltaTime
    )
    {
        horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);

        float targetSpeed = horizontalInput * maximumSpeed;

        float acceleration = GetHorizontalAcceleration(
            velocity.x,
            horizontalInput
        );

        velocity.x = Mathf.MoveTowards(
            velocity.x,
            targetSpeed,
            acceleration * deltaTime
        );
    }

    /// <summary>
    /// 현재 이동 상태에 적용할 수평 가속도를 반환한다.
    /// </summary>
    private float GetHorizontalAcceleration(
        float currentSpeed,
        float horizontalInput
    )
    {
        if (isGrounded)
        {
            if (Mathf.Approximately(horizontalInput, 0f))
            {
                return groundDeceleration;
            }

            bool isReversing =
                !Mathf.Approximately(currentSpeed, 0f) &&
                Mathf.Sign(currentSpeed) != Mathf.Sign(horizontalInput);

            if (isReversing)
                return groundReverseAcceleration;

            return groundAcceleration;
        }

        if (Mathf.Approximately(horizontalInput, 0f))
            return airDeceleration;

        return airAcceleration;
    }

    /// <summary>
    /// 점프 가능한 상태라면 수직 속도를 설정하고 점프를 실행한다.
    /// </summary>
    private bool TryJump(ref Vector2 velocity)
    {
        if (jumpBufferRemaining <= 0f)
        {
            return false;
        }

        if (coyoteTimeRemaining <= 0f)
        {
            return false;
        }

        velocity.y = jumpVelocity;

        jumpBufferRemaining = 0f;
        coyoteTimeRemaining = 0f;
        isGrounded = false;
        hasJumpedThisPhysicsTick = true;
        Jumped?.Invoke();

        return true;
    }

    /// <summary>
    /// 상승과 하강 상태에 맞는 중력을 적용하고 이동 거리를 반환한다.
    /// </summary>
    private float ApplyGravity(ref Vector2 velocity, float deltaTime)
    {
        if (isGrounded && velocity.y <= 0f)
        {
            velocity.y = 0f;
            return 0f;
        }

        float distance = 0f;
        float remainingTime = deltaTime;

        if (velocity.y > 0f)
        {
            float timeToApex = velocity.y / risingGravity;
            float risingTime = Mathf.Min(remainingTime, timeToApex);

            distance += velocity.y * risingTime
                - risingGravity * risingTime * risingTime * 0.5f;
            velocity.y = Mathf.Max(0f, velocity.y - risingGravity * risingTime);
            remainingTime -= risingTime;

            if (remainingTime <= 0f)
            {
                return distance;
            }

            velocity.y = 0f;
        }

        if (velocity.y <= -maximumFallSpeed)
        {
            velocity.y = -maximumFallSpeed;
            return distance + velocity.y * remainingTime;
        }

        float timeToMaximumFallSpeed =
            (maximumFallSpeed + velocity.y) / fallingGravity;
        float fallingTime = Mathf.Min(
            remainingTime,
            Mathf.Max(0f, timeToMaximumFallSpeed)
        );

        distance += velocity.y * fallingTime
            - fallingGravity * fallingTime * fallingTime * 0.5f;
        velocity.y -= fallingGravity * fallingTime;
        remainingTime -= fallingTime;

        if (remainingTime > 0f)
        {
            velocity.y = -maximumFallSpeed;
            distance += velocity.y * remainingTime;
        }

        return distance;
    }

    /// <summary>
    /// 착지할 때 남아 있는 점프 선입력을 실행한다.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if ((groundMask.value & (1 << collision.gameObject.layer)) == 0)
        {
            return;
        }

        StopRisingFromCollision(collision);

        if (rollRemaining > 0f || hasJumpedThisPhysicsTick || jumpBufferRemaining <= 0f)
        {
            return;
        }

        for (int index = 0; index < collision.contactCount; index++)
        {
            if (collision.GetContact(index).normal.y <= 0.5f)
            {
                continue;
            }

            Vector2 velocity = body.linearVelocity;
            velocity.y = jumpVelocity;
            body.linearVelocity = velocity;
            verticalVelocity = jumpVelocity;
            jumpBufferRemaining = 0f;
            coyoteTimeRemaining = 0f;
            isGrounded = false;
            hasJumpedThisPhysicsTick = true;
            Jumped?.Invoke();
            return;
        }
    }

    /// <summary>
    /// 천장에 계속 닿아 있는 동안 상승 속도가 다시 생기지 않게 막는다.
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if ((groundMask.value & (1 << collision.gameObject.layer)) != 0)
        {
            StopRisingFromCollision(collision);
        }

    }

    /// <summary>
    /// 충돌 접점이 천장을 향하면 현재 상승 속도를 제거한다.
    /// </summary>
    private void StopRisingFromCollision(Collision2D collision)
    {
        if (CanPassFromBelow(collision.collider))
            return;
        if (verticalVelocity <= 0f)
        {
            return;
        }

        for (int index = 0; index < collision.contactCount; index++)
        {
            if (collision.GetContact(index).normal.y >= -0.5f)
            {
                continue;
            }

            verticalVelocity = 0f;
            body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
            return;
        }
    }

    /// <summary>
    /// 수평 입력에 맞춰 바라보는 방향을 갱신한다.
    /// </summary>
    private void ApplyFacing(float horizontalInput)
    {
        if (horizontalInput > 0.01f)
        {
            facingDirection = 1;
        }
        else if (horizontalInput < -0.01f)
        {
            facingDirection = -1;
        }
        else
        {
            return;
        }

        ApplyVisualFacing();
    }

    private void ApplyVisualFacing()
    {
        if (visualRoot == null)
        {
            return;
        }

        Vector3 scale = visualBaseScale;
        scale.x = Mathf.Abs(scale.x) * facingDirection;
        visualRoot.localScale = scale;
    }

    /// <summary>
    /// 상승 중 천장 모서리에 걸리면 이동 가능한 수평 위치를 찾는다.
    /// </summary>
    private void ApplyCornerCorrection(
        Vector2 velocity,
        float deltaTime
    )
    {
        if (velocity.y <= 0f)
        {
            return;
        }

        Vector2 predictedPosition = body.position + velocity * deltaTime;

        if (!OverlapsGround(predictedPosition))
        {
            return;
        }

        float preferredDirection = 1f;
        if (!Mathf.Approximately(velocity.x, 0f))
            preferredDirection = Mathf.Sign(velocity.x);

        for (
            float distance = cornerCorrectionStep;
            distance <= maximumCornerCorrection + 0.001f;
            distance += cornerCorrectionStep
        )
        {
            if (TryCornerCorrection(
                preferredDirection * distance,
                predictedPosition
            ))
            {
                return;
            }

            if (TryCornerCorrection(
                -preferredDirection * distance,
                predictedPosition
            ))
            {
                return;
            }
        }
    }

    /// <summary>
    /// 지정한 수평 오프셋으로 모서리 보정이 가능한지 검사한다.
    /// </summary>
    private bool TryCornerCorrection(
        float horizontalOffset,
        Vector2 predictedPosition
    )
    {
        Vector2 offset = Vector2.right * horizontalOffset;

        if (OverlapsGround(body.position + offset))
        {
            return false;
        }

        if (OverlapsGround(predictedPosition + offset))
        {
            return false;
        }

        // 속도는 유지하고 위치만 옮긴다.
        body.position += offset;
        return true;
    }

    /// <summary>
    /// 지정한 위치에서 플레이어 콜라이더가 바닥과 겹치는지 검사한다.
    /// </summary>
    private bool OverlapsGround(Vector2 bodyPosition)
    {
        Bounds bounds = bodyCollider.bounds;

        Vector2 colliderOffset =
            (Vector2)bounds.center - body.position;

        Vector2 checkCenter =
            bodyPosition + colliderOffset;

        Vector2 checkSize = new(
            Mathf.Max(0.01f, bounds.size.x - collisionSkin * 2f),
            Mathf.Max(0.01f, bounds.size.y - collisionSkin * 2f)
        );

        int count = Physics2D.OverlapBox(checkCenter, checkSize, 0f, groundFilter, overlapHits);
        for (int index = 0; index < count; index++)
            if (!CanPassFromBelow(overlapHits[index]))
                return true;
        return false;
    }

    /// <summary>
    /// 일방향 발판은 천장과 모서리 보정에서 제외해 아래에서 통과시킨다.
    /// </summary>
    private static bool CanPassFromBelow(Collider2D collider)
    {
        if (collider == null || !collider.usedByEffector)
            return false;
        PlatformEffector2D effector = collider.GetComponent<PlatformEffector2D>();
        return effector != null && effector.enabled && effector.useOneWay;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 선택된 플레이어의 접지 검사 영역을 Scene 창에 표시한다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Collider2D currentCollider = GetComponent<Collider2D>();

        if (currentCollider == null)
            return;

        Bounds bounds = currentCollider.bounds;

        Vector2 checkSize = new(
            Mathf.Max(0.01f, bounds.size.x - groundCheckWidthReduction),
            Mathf.Max(0.01f, collisionSkin)
        );

        Vector2 checkOrigin = new(
            bounds.center.x,
            bounds.min.y + collisionSkin - groundCheckDistance
        );

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(checkOrigin, checkSize);
    }
#endif

}
