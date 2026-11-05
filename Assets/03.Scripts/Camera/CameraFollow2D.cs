/// <summary>
/// 스크립트 최종 수정일: 2026-11-02
/// 기능: 플레이어를 추적하고 Ctrl과 화면 가장자리의 마우스 위치로 주변을 둘러본다.
/// </summary>

using UnityEngine;

public sealed class CameraFollow2D : MonoBehaviour
{
    public Collider2D CameraBounds => cameraBounds;

    [SerializeField] private Transform target;                          // 추적할 플레이어
    [SerializeField] private Collider2D cameraBounds;                   // 카메라가 벗어나지 않을 맵 경계
    [SerializeField] private Vector2 deadZoneSize = new(3f, 1.5f);      // 카메라가 멈추는 영역
    [SerializeField] private float followSharpness = 6f;                // 추적 반응 속도
    [SerializeField] private float lookAheadDistance = 2f;              // 이동 방향을 미리 보여주는 거리
    [SerializeField] private float lookAheadSharpness = 4f;             // 시점 전환 반응 속도
    [SerializeField, Min(0f)] private float peekDistance = 7.5f;         // 둘러보기 이동 속도
    [SerializeField, Range(0.05f, 0.45f)] private float peekEdgeWidth = 0.18f; // 둘러보기가 시작되는 화면 가장자리 비율
    [SerializeField, Min(0f)] private float peekSharpness = 6f;          // 둘러보기와 복귀 반응 속도

    private Rigidbody2D targetBody;         // 추적 대상의 이동 속도를 읽는 리지드바디
    private Camera currentCamera;           // 화면 크기를 계산할 카메라
    private float lookDirection;            // 현재 바라볼 수평 방향
    private float currentLookAhead;         // 현재 적용된 시선 선행 거리
    private PlayerInputReader inputReader;
    private Vector3 followPosition;
    private Vector2 currentPeekOffset;
    private bool isPeeking;

    /// <summary>
    /// 추적 대상을 찾고 이동 정보에 필요한 참조를 초기화한다.
    /// </summary>
    private void Awake()
    {
        currentCamera = GetComponent<Camera>();

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
                target = player.transform;
        }

        if (target != null)
        {
            targetBody = target.GetComponent<Rigidbody2D>();
            inputReader = target.GetComponent<PlayerInputReader>();
        }

        followPosition = transform.position;

        Debug.Assert(target != null, "CameraFollow2D의 Target을 찾지 못했습니다.", this);
        Debug.Assert(targetBody != null, "CameraFollow2D의 Target에 Rigidbody2D가 없습니다.", this);
        Debug.Assert(currentCamera != null, "CameraFollow2D와 같은 오브젝트에 Camera가 없습니다.", this);
    }

    /// <summary>
    /// 데드존과 선행 시점을 반영해 카메라 위치를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 current = followPosition;
        Vector3 desired = current;
        float horizontalSpeed = 0f;
        if (targetBody != null)
        {
            horizontalSpeed = targetBody.linearVelocity.x;
        }

        if (Mathf.Abs(horizontalSpeed) > 0.1f)
            lookDirection = Mathf.Sign(horizontalSpeed);

        float lookAheadBlend = 1f - Mathf.Exp(-lookAheadSharpness * Time.unscaledDeltaTime);
        currentLookAhead = Mathf.Lerp(currentLookAhead, lookDirection * lookAheadDistance, lookAheadBlend);

        Vector2 focusPosition = (Vector2)target.position + Vector2.right * currentLookAhead;
        Vector2 distance = focusPosition - (Vector2)current;
        Vector2 halfDeadZone = deadZoneSize * 0.5f;

        if (Mathf.Abs(distance.x) > halfDeadZone.x)
            desired.x = focusPosition.x - Mathf.Sign(distance.x) * halfDeadZone.x;

        if (Mathf.Abs(distance.y) > halfDeadZone.y)
            desired.y = focusPosition.y - Mathf.Sign(distance.y) * halfDeadZone.y;

        float blend = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
        followPosition = ClampToBounds(Vector3.Lerp(current, desired, blend));
        if (TryGetPeekDirection(out Vector2 direction))
        {
            currentPeekOffset += direction * Mathf.Max(0f, peekDistance) * Time.unscaledDeltaTime;
        }
        else
        {
            float peekBlend = 1f - Mathf.Exp(-peekSharpness * Time.unscaledDeltaTime);
            currentPeekOffset = Vector2.Lerp(currentPeekOffset, Vector2.zero, peekBlend);
            if (currentPeekOffset.sqrMagnitude < 0.000001f)
                currentPeekOffset = Vector2.zero;
        }
        Vector3 position = ClampToBounds(followPosition + (Vector3)currentPeekOffset);
        currentPeekOffset = position - followPosition;
        transform.position = position;
    }

    /// <summary>
    /// 가장자리에서 둘러보기를 시작하고 마우스 방향을 따라 시점을 이어서 이동한다.
    /// </summary>
    private bool TryGetPeekDirection(out Vector2 direction)
    {
        direction = Vector2.zero;
        if (inputReader == null || !inputReader.isActiveAndEnabled || !inputReader.IsCameraPeekHeld
            || currentCamera == null || !currentCamera.orthographic)
        {
            isPeeking = false;
            return false;
        }
        Rect viewport = currentCamera.pixelRect;
        Vector2 pointer = inputReader.PointerPosition;
        if (viewport.width <= 0f || viewport.height <= 0f
            || pointer.x < viewport.xMin || pointer.x > viewport.xMax
            || pointer.y < viewport.yMin || pointer.y > viewport.yMax)
        {
            isPeeking = false;
            return false;
        }

        Vector2 viewportPosition = new Vector2(
            (pointer.x - viewport.xMin) / viewport.width,
            (pointer.y - viewport.yMin) / viewport.height
        );
        direction = GetPeekDirection(viewportPosition);
        return isPeeking;
    }

    private Vector2 GetPeekDirection(Vector2 viewportPosition)
    {
        Vector2 direction = (viewportPosition - Vector2.one * 0.5f) * 2f;
        float distance = direction.magnitude;
        if (distance <= 0.12f)
        {
            isPeeking = false;
            return Vector2.zero;
        }
        if (!isPeeking)
        {
            if (Mathf.Approximately(GetEdgeStrength(viewportPosition.x), 0f)
                && Mathf.Approximately(GetEdgeStrength(viewportPosition.y), 0f))
                return Vector2.zero;
            isPeeking = true;
        }
        return direction.normalized * Mathf.InverseLerp(0.12f, 1f, distance);
    }

    private float GetEdgeStrength(float position)
    {
        float direction = position * 2f - 1f;
        float edgeStart = 1f - peekEdgeWidth * 2f;
        return Mathf.Sign(direction) * Mathf.InverseLerp(edgeStart, 1f, Mathf.Abs(direction));
    }

    /// <summary>
    /// 화면 전체가 맵 경계 안에 들어오도록 카메라 중심 위치를 제한한다.
    /// </summary>
    private Vector3 ClampToBounds(Vector3 position)
    {
        if (cameraBounds == null || currentCamera == null || !currentCamera.orthographic)
            return position;

        Bounds bounds = cameraBounds.bounds;
        float halfHeight = currentCamera.orthographicSize;
        float halfWidth = halfHeight * currentCamera.aspect;

        if (bounds.size.x <= halfWidth * 2f)
            position.x = bounds.center.x;
        else
            position.x = Mathf.Clamp(position.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);

        if (bounds.size.y <= halfHeight * 2f)
            position.y = bounds.center.y;
        else
            position.y = Mathf.Clamp(position.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight);

        return position;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, deadZoneSize);
    }
#endif
}
