/// <summary>
/// 스크립트 최종 수정일: 2026-10-12
/// 기능: 추적 대상의 이동 방향을 반영하며 데드존과 맵 경계 안에서 카메라를 이동 시킨다.
/// </summary>

using UnityEngine;


public sealed class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;                          // 추적할 플레이어
    [SerializeField] private Collider2D cameraBounds;                   // 카메라가 벗어나지 않을 맵 경계
    [SerializeField] private Vector2 deadZoneSize = new(3f, 1.5f);      // 카메라가 멈추는 영역
    [SerializeField] private float followSharpness = 6f;                // 추적 반응 속도
    [SerializeField] private float lookAheadDistance = 2f;              // 이동 방향을 미리 보여주는 거리
    [SerializeField] private float lookAheadSharpness = 4f;             // 시점 전환 반응 속도

    private Rigidbody2D targetBody;         // 추적 대상의 이동 속도를 읽는 리지드바디
    private Camera currentCamera;           // 화면 크기를 계산할 카메라
    private float lookDirection;            // 현재 바라볼 수평 방향
    private float currentLookAhead;         // 현재 적용된 시선 선행 거리

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
            targetBody = target.GetComponent<Rigidbody2D>();

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

        Vector3 current = transform.position;
        Vector3 desired = current;
        float horizontalSpeed = targetBody != null ? targetBody.linearVelocity.x : 0f;

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
        transform.position = ClampToBounds(Vector3.Lerp(current, desired, blend));
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

        position.x = bounds.size.x <= halfWidth * 2f
            ? bounds.center.x
            : Mathf.Clamp(position.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        position.y = bounds.size.y <= halfHeight * 2f
            ? bounds.center.y
            : Mathf.Clamp(position.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight);

        return position;
    }

    /// <summary>
    /// 선택된 카메라의 데드존 범위를 Scene 창에 표시.
    /// </summary>

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, deadZoneSize);
    }
#endif
}
