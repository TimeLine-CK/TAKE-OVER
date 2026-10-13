/// <summary>
/// 스크립트 최종 수정일: 2026-10-13
/// 기능: 플레이어가 영역에 들어오면 현재 체크포인트로 등록한다.
/// </summary>

using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] private SystemDirector systemDirector; // 체크포인트를 관리하는 객체

    public Vector2 Position => transform.position; // 체크포인트 위치

    /// <summary>
    /// 지정된 관리자가 없으면 씬에서 체크포인트 관리자를 찾는다.
    /// </summary>
    private void Awake()
    {
        if (systemDirector == null)
            systemDirector = FindFirstObjectByType<SystemDirector>();
    }

    /// <summary>
    /// 플레이어가 영역에 들어오면 마지막으로 밟은 체크포인트를 갱신한다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        GameObject enteringObject = collision.gameObject;

        if (collision.attachedRigidbody != null)
            enteringObject = collision.attachedRigidbody.gameObject;

        if (!enteringObject.CompareTag("Player"))
            return;

        if (systemDirector == null)
        {
            Debug.LogWarning("Checkpoint에 SystemDirector가 연결되지 않았습니다.", this);
            return;
        }

        systemDirector.SetCheckpoint(this);
    }
}
