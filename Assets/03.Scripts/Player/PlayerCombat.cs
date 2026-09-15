/// <summary>
/// 스크립트 최종 수정일: 2026-09-15
/// 기능: 플레이어와 잔상의 공격 입력, 타격 판정, 공격 애니메이션과 이펙트를 처리한다.
/// </summary>

using System;
using UnityEngine;

public sealed class PlayerCombat : MonoBehaviour
{
    public event Action Attacked; // 공격이 실행됐음을 전달하는 이벤트

    public bool DamageEnabled { get; set; } = true; // 공격 판정 활성화 여부
    public bool EffectsEnabled { get; set; } = true; // 공격 이펙트 활성화 여부

    [SerializeField] private Vector2 collideBoxSize = new(1.5f, 1f); // 공격 판정 크기
    [SerializeField] private LayerMask targetMask; // 공격할 대상 레이어
    [SerializeField] private PlayerAnimator animator; // 공격 애니메이션을 실행할 컴포넌트
    [SerializeField] private GameObject slashEffectPrefab; // 생성할 베기 이펙트 프리팹
    [SerializeField] private Vector2 slashEffectOffset = new(1.24f, 0.1f); // 공격 주체 기준 이펙트 위치
    [SerializeField, Range(0.1f, 1f)] private float slashEffectScale = 0.36f; // 베기 이펙트 크기

    private PlayerInputReader inputReader; // 공격 입력을 제공하는 컴포넌트
    private PlayerMotor motor; // 현재 바라보는 방향을 제공하는 컴포넌트
    private SpriteRenderer playerRenderer; // 이펙트 정렬 기준이 되는 렌더러
    private float attackDebugRemaining; // 공격 범위를 표시할 남은 시간
    private int attackDirection = 1; // 현재 공격 방향

    /// <summary>
    /// 공격 처리에 필요한 플레이어 컴포넌트를 초기화한다.
    /// </summary>
    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        motor = GetComponent<PlayerMotor>();
        playerRenderer = GetComponentInChildren<SpriteRenderer>();

        if (animator == null)
            animator = GetComponentInChildren<PlayerAnimator>();

        Debug.Assert(animator != null, "PlayerCombat의 PlayerAnimator를 찾지 못했습니다.", this);
    }

    /// <summary>
    /// 공격 입력을 소비하고 공격 범위 표시 시간을 갱신한다.
    /// </summary>
    private void Update()
    {
        if (inputReader.ConsumeAttackPressed())
        {
            PerformAttack();
            Debug.Log("공격 마우스 키 눌림");
        }

        if (attackDebugRemaining > 0f)
        {
            attackDebugRemaining -= Time.deltaTime;
            DrawAttackBox(GetAttackCenter());
        }
    }

    /// <summary>
    /// 지정한 방향으로 애니메이션, 이펙트, 타격 판정을 실행한다.
    /// </summary>
    private void Attack(int direction)
    {
        attackDirection = direction < 0 ? -1 : 1;
        animator.PlayAttack();

        SpawnSlashEffect();

        if (!DamageEnabled)
            return;

        Vector2 attackCenter = GetAttackCenter();
        attackDebugRemaining = 0.15f;

        RaycastHit2D hit = Physics2D.BoxCast(attackCenter,collideBoxSize, 0f, Vector2.zero, 0f, targetMask);

        if (hit.collider != null)
            Debug.Log($"{hit.collider.name} 공격 적중", this);
    }

    /// <summary>
    /// 이동 입력 또는 현재 시선 방향을 기준으로 공격한다.
    /// </summary>
    public void PerformAttack()
    {
        int direction = Mathf.Abs(inputReader.Move.x) > 0.01f
            ? (int)Mathf.Sign(inputReader.Move.x)
            : motor.FacingDirection;
        Attack(direction);
        Attacked?.Invoke();
    }

    /// <summary>
    /// 기록된 방향을 사용해 공격을 실행한다.
    /// </summary>
    public void PerformAttack(int direction)
    {
        Attack(direction);
        Attacked?.Invoke();
    }

    /// <summary>
    /// 공격 방향을 유지하며 플레이어를 따라가는 베기 이펙트를 생성한다.
    /// </summary>
    private void SpawnSlashEffect()
    {
        if (!EffectsEnabled || slashEffectPrefab == null)
            return;

        GameObject effect = Instantiate(slashEffectPrefab);
        Vector2 scale = new(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y)
        );
        Vector2 offset = new(
            slashEffectOffset.x * attackDirection * scale.x,
            slashEffectOffset.y * scale.y
        );

        effect.transform.position = (Vector2)transform.position + offset;
        effect.transform.rotation = Quaternion.identity;
        effect.transform.localScale = new Vector3(scale.x, scale.y, 1f)
            * slashEffectScale;
        effect.GetComponent<SlashEffect>()?.Follow(transform, offset);

        SpriteRenderer effectRenderer = effect.GetComponent<SpriteRenderer>();

        if (effectRenderer != null)
        {
            effectRenderer.flipX = attackDirection < 0;

            if (playerRenderer != null)
            {
                effectRenderer.sortingLayerID = playerRenderer.sortingLayerID;
                effectRenderer.sortingOrder = playerRenderer.sortingOrder - 1;
            }
        }
    }

    /// <summary>
    /// 현재 공격 방향에 맞는 타격 판정 중심을 계산한다.
    /// </summary>
    private Vector2 GetAttackCenter()
    {
        return (Vector2)transform.position
            + Vector2.right * attackDirection * collideBoxSize.x * 0.5f;
    }

    /// <summary>
    /// 지정한 공격 판정 범위를 디버그 선으로 표시한다.
    /// </summary>
    private void DrawAttackBox(Vector2 center)
    {
        Vector2 halfSize = collideBoxSize * 0.5f;
        Vector2 bottomLeft = center + new Vector2(-halfSize.x, -halfSize.y);
        Vector2 bottomRight = center + new Vector2(halfSize.x, -halfSize.y);
        Vector2 topLeft = center + new Vector2(-halfSize.x, halfSize.y);
        Vector2 topRight = center + new Vector2(halfSize.x, halfSize.y);

        Debug.DrawLine(bottomLeft, bottomRight, Color.red);
        Debug.DrawLine(bottomRight, topRight, Color.red);
        Debug.DrawLine(topRight, topLeft, Color.red);
        Debug.DrawLine(topLeft, bottomLeft, Color.red);
    }

    /// <summary>
    /// 아래 있는 함수는 히트 박스를 시각화 하는 역할을 수행한다.
    /// </summary>

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        PlayerMotor currentMotor = GetComponent<PlayerMotor>();
        int direction = currentMotor != null ? currentMotor.FacingDirection : 1;
        Vector2 attackCenter = (Vector2)transform.position
            + Vector2.right * direction * collideBoxSize.x * 0.5f;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(attackCenter, collideBoxSize);
    }
#endif
}
