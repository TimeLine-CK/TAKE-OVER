/// <summary>
/// 스크립트 최종 수정일: 2026-09-15
/// 기능: 공격 이펙트를 재생하고 공격 당시 방향을 유지하며 플레이어 위치를 추적한다.
/// </summary>

using UnityEngine;

[RequireComponent(typeof(Animation))]
public sealed class SlashEffect : MonoBehaviour
{
    [SerializeField] private AnimationClip clip; // 재생할 베기 이펙트 애니메이션

    private Transform target; // 위치를 따라갈 공격 주체
    private Vector2 offset; // 공격 시점에 결정된 월드 좌표 오프셋

    /// <summary>
    /// 이펙트 애니메이션을 재생하고 재생 길이에 맞춰 제거한다.
    /// </summary>
    private void OnEnable()
    {
        if (clip == null)
        {
            Destroy(gameObject);
            return;
        }

        GetComponent<Animation>().Play(clip.name);
        Destroy(gameObject, clip.length);
    }

    /// <summary>
    /// 공격 방향은 유지하면서 대상의 현재 위치를 따라간다.
    /// </summary>
    private void LateUpdate()
    {
        if (target != null)
            transform.position = (Vector2)target.position + offset;
    }

    /// <summary>
    /// 이펙트가 따라갈 대상과 공격 당시 위치 오프셋을 설정한다.
    /// </summary>
    public void Follow(Transform followTarget, Vector2 worldOffset)
    {
        target = followTarget;
        offset = worldOffset;
    }
}
