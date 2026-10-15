/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 적이 플레이어를 발견하면 표시를 띄우고 잠시 기다린 뒤 공격하게 한다.
/// </summary>

using UnityEngine;

public sealed class EnemyAwareness : MonoBehaviour
{
    [SerializeField] private float reactionDuration = 1f; // 발견 후 공격을 허용할 때까지의 시간
    [SerializeField] private GameObject noticeObject;    // 머리 위 발견 표시
    private bool hasDetected;
    private float readyTime;
    public bool IsReady => hasDetected && Time.time >= readyTime;

    private void OnEnable()
    {
        ResetDetection();
    }

    public void ResetDetection()
    {
        hasDetected = false;
        if (noticeObject != null)
            noticeObject.SetActive(false);
    }

    /// <summary>
    /// 감지 여부를 반영하고 대응 시간이 지났을 때만 공격을 허용한다.
    /// </summary>
    public bool Observe(bool detected)
    {
        if (!detected)
        {
            ResetDetection();
            return false;
        }
        if (!hasDetected)
        {
            hasDetected = true;
            readyTime = Time.time + Mathf.Max(0f, reactionDuration);
        }
        bool ready = Time.time >= readyTime;
        if (noticeObject != null)
            noticeObject.SetActive(!ready);
        return ready;
    }
}
