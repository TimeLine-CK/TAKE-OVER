/// <summary>
/// 스크립트 최종 수정일: 2026-10-27
/// 기능: 적이 플레이어를 발견하면 표시를 띄우고 잠시 기다린 뒤 공격하게 한다.
/// </summary>

using UnityEngine;

public sealed class EnemyAwareness : MonoBehaviour
{
    [SerializeField] private float reactionDuration = 1f; // 발견 후 공격을 허용할 때까지의 시간
    [SerializeField] private GameObject noticeObject;    // 머리 위 발견 표시
    private bool hasDetected;
    private float remaining;
    private readonly RaycastHit2D[] sightHits = new RaycastHit2D[8];
    public bool HasDetected => hasDetected;
    public bool IsReady => hasDetected && remaining <= 0f;
    public float Remaining => remaining;

    public void ResetDetection()
    {
        hasDetected = false;
        remaining = 0f;
        if (noticeObject != null)
            noticeObject.SetActive(false);
    }

    /// <summary>
    /// 감지 여부를 반영하고 대응 시간이 지났을 때만 공격을 허용한다.
    /// </summary>
    public bool Observe(bool detected, bool advance = true)
    {
        if (!detected)
        {
            ResetDetection();
            return false;
        }
        if (!hasDetected)
        {
            hasDetected = true;
            remaining = Mathf.Max(0f, reactionDuration);
        }
        else if (advance)
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        bool ready = IsReady;
        if (noticeObject != null)
            noticeObject.SetActive(!ready);
        return ready;
    }

    public bool HasLineOfSight(Vector2 origin, Vector2 destination, LayerMask obstacleMask)
    {
        ContactFilter2D filter = new();
        filter.SetLayerMask(obstacleMask);
        filter.useTriggers = false;
        Vector2 offset = destination - origin;
        return Physics2D.Raycast(origin, offset.normalized, filter, sightHits, offset.magnitude) == 0;
    }
}
