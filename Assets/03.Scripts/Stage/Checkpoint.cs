/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 플레이어가 영역에 들어오면 체크포인트를 저장하고 활성 상태에 맞는 이미지를 표시한다.
/// </summary>

using UnityEngine;
using System.Collections.Generic;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] private SystemDirector systemDirector; // 체크포인트를 관리하는 객체
    [SerializeField] private Transform safePosition; // 부활 위치
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite inactiveSprite;
    [SerializeField] private Sprite activeSprite;
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
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void OnEnable()
    {
        SetActivated(systemDirector != null && systemDirector.CurrentCheckpoint == this);
    }

    public void SetActivated(bool activated)
    {
        Sprite sprite = inactiveSprite;
        if (activated)
            sprite = activeSprite;
        if (spriteRenderer != null && sprite != null)
            spriteRenderer.sprite = sprite;
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
