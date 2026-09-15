/// <summary>
/// 스크립트 최종 수정일: 2026-09-15
/// 기능: 추적 대상의 이동 방향을 반영하며 데드존 안에서 카메라를 이동 시킨다.
/// </summary>

using UnityEngine;


public sealed class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target; // 추적할 플레이어
    [SerializeField] private Vector2 deadZoneSize = new(3f, 1.5f); // 카메라가 멈추는 영역
    [SerializeField, Min(0f)] private float followSharpness = 6f; // 추적 반응 속도
    [SerializeField, Min(0f)] private float lookAheadDistance = 2f; // 이동 방향을 미리 보여주는 거리
    [SerializeField, Min(0f)] private float lookAheadSharpness = 4f; // 시점 전환 반응 속도

    private Rigidbody2D targetBody; // 추적 대상의 이동 속도를 읽는 리지드바디
    private float lookDirection; // 현재 바라볼 수평 방향
    private float currentLookAhead; // 현재 적용된 시선 선행 거리
    private Transform returnTarget; // 복귀 시 다시 추적할 대상
    private Vector3 returnStartPosition; // 카메라 복귀 시작 위치
    private float returnDuration; // 카메라 복귀에 사용할 시간
    private float returnElapsed; // 카메라 복귀가 진행된 시간
    private bool isReturning; // 카메라가 복귀 중인지 여부

    /// <summary>
    /// 추적 대상을 찾고 이동 정보에 필요한 참조를 초기화한다.
    /// </summary>
    private void Awake()
    {
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
    }

    /// <summary>
    /// 카메라가 즉시 추적할 대상을 변경.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        if (newTarget == null)
            return;

        target = newTarget;
        targetBody = target.GetComponent<Rigidbody2D>();
        lookDirection = 0f;
        currentLookAhead = 0f;
        isReturning = false;
    }

    /// <summary>
    /// 지정한 시간 동안 새 대상으로 카메라를 복귀시킨다.
    /// </summary>
    public void ReturnToTarget(Transform newTarget, float duration)
    {
        if (newTarget == null)
            return;

        target = newTarget;
        targetBody = target.GetComponent<Rigidbody2D>();
        lookDirection = 0f;
        currentLookAhead = 0f;
        returnTarget = newTarget;
        returnStartPosition = transform.position;
        returnDuration = Mathf.Max(0f, duration);
        returnElapsed = 0f;
        isReturning = returnDuration > 0f;

        if (!isReturning)
            ApplyReturnPosition(1f);
    }

    /// <summary>
    /// 데드존과 선행 시점을 반영해 카메라 위치를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (target == null)
            return;

        if (isReturning)
        {
            returnElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(returnElapsed / returnDuration);
            ApplyReturnPosition(progress);

            if (progress >= 1f)
                isReturning = false;

            return;
        }

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
        transform.position = Vector3.Lerp(current, desired, blend);
    }

    /// <summary>
    /// 복귀 진행도에 맞춰 카메라 위치를 보간한다.
    /// </summary>
    private void ApplyReturnPosition(float progress)
    {
        Vector3 destination = returnTarget.position;
        destination.z = returnStartPosition.z;
        transform.position = Vector3.Lerp(returnStartPosition, destination, progress);
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
