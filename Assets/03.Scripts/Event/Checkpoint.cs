/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 플레이어가 영역에 들어오면 현재 체크포인트로 등록한다.
/// </summary>

using UnityEngine;
using System.Collections.Generic;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] private SystemDirector systemDirector; // 체크포인트를 관리하는 객체
    [SerializeField] private Transform safePosition; // 부활 위치
    private readonly HashSet<Collider2D> occupants = new();

    public Vector2 Position
    {
        get
        {
            if (safePosition != null)
                return safePosition.position;
            return transform.position;
        }
    }

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
        if (!occupants.Add(collision) || occupants.Count > 1)
            return;

        if (systemDirector == null)
        {
            Debug.LogWarning("Checkpoint에 SystemDirector가 연결되지 않았습니다.", this);
            return;
        }

        systemDirector.SetCheckpoint(this);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        occupants.Remove(collision);
    }

    private void OnDisable()
    {
        occupants.Clear();
    }
}
