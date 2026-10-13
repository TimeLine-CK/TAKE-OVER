/// <summary>
/// 스크립트 최종 수정일: 2026-10-12
/// 기능: 플레이어의 공격 입력, 타격 판정, 공격 애니메이션과 이펙트를 처리한다.
/// </summary>

using UnityEngine;
using UnityEngine.Serialization;

public sealed class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Vector2 collideBoxSize = new(1.5f, 1f);            // 공격 판정 크기
    [SerializeField] private LayerMask targetMask;                              // 공격할 대상 레이어
    [FormerlySerializedAs("attackDamage")]
    public int AttackDamage = 10;                                               // 기본 공격 피해량
    [SerializeField] private PlayerAnimator animator;                           // 공격 애니메이션을 실행할 컴포넌트
    [SerializeField] private GameObject slashEffectPrefab;                      // 생성할 베기 이펙트 프리팹
    [SerializeField] private Vector2 slashEffectOffset = new(0.4f, 0.05f);      // 공격 주체 기준 이펙트 위치
    [SerializeField, Range(0.1f, 2f)] private float slashEffectScale = 0.62f;   // 베기 이펙트 크기
    [SerializeField, Range(1, 8)] private int slashEffectPoolSize = 4;          // 미리 생성할 베기 이펙트 수

    private PlayerInputReader inputReader;                                      // 공격 입력을 제공하는 컴포넌트
    private PlayerMotor motor;                                                  // 현재 바라보는 방향을 제공하는 컴포넌트
    private Rigidbody2D body;                                                   // 공격 판정 기준이 되는 물리 위치
    private Camera mainCamera;                                                   // 마우스 좌표를 월드 좌표로 변환할 카메라
    private SpriteRenderer playerRenderer;                                      // 이펙트 정렬 기준이 되는 렌더러
    private CombatVfxFeedback vfxFeedback;                                      // 공격 화면 충격을 처리하는 컴포넌트
    private float attackDebugRemaining;                                         // 공격 범위를 표시할 남은 시간
    private Vector2 attackAimDirection = Vector2.right;                         // 현재 일반 공격 조준 방향
    private SlashEffect[] slashEffects;                                         // 재사용할 베기 이펙트 목록

    /// <summary>
    /// 공격 처리에 필요한 플레이어 컴포넌트를 초기화한다.
    /// </summary>
    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        motor = GetComponent<PlayerMotor>();
        body = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;
        playerRenderer = GetComponentInChildren<SpriteRenderer>();
        vfxFeedback = FindFirstObjectByType<CombatVfxFeedback>();

        if (animator == null)
        {
            animator = GetComponentInChildren<PlayerAnimator>();
        }
        Debug.Assert(animator != null, "PlayerCombat의 PlayerAnimator를 찾지 못했습니다.", this);
        CreateSlashEffectPool();
    }

    /// <summary>
    /// 공격 입력을 소비하고 공격 범위 표시 시간을 갱신한다.
    /// </summary>
    private void Update()
    {
        if (inputReader.ConsumeAttackPressed())
        {
            PerformAttack();
        }


        if (attackDebugRemaining > 0f)
        {
            attackDebugRemaining -= Time.deltaTime;
            DrawAttackBox(GetAttackCenter(), GetAttackAngle());
        }
    }

    /// <summary>
    /// 지정한 방향으로 애니메이션, 이펙트, 타격 판정을 실행한다.
    /// </summary>
    private void Attack(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.right * motor.FacingDirection;
        }

        attackAimDirection = direction.normalized;

        animator.PlayAttack();

        SpawnSlashEffect();
        vfxFeedback?.Pulse(attackAimDirection, 0.55f);

        Vector2 attackCenter = GetAttackCenter();
        attackDebugRemaining = 0.15f;

        ApplyDamage(attackCenter, collideBoxSize, GetAttackAngle());
    }

    /// <summary>
    /// 플레이어에서 마우스 위치를 향하는 방향으로 공격한다.
    /// </summary>
    private void PerformAttack()
    {
        Attack(GetPointerAimDirection());
    }

    /// <summary>
    /// 현재 마우스 화면 좌표를 플레이어 기준 월드 조준 방향으로 변환한다.
    /// </summary>
    private Vector2 GetPointerAimDirection()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return Vector2.right * motor.FacingDirection;
        }

        Vector2 pointerWorld = mainCamera.ScreenToWorldPoint(inputReader.PointerPosition);
        Vector2 direction = pointerWorld - body.position;
        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.right * motor.FacingDirection;
    }

    /// <summary>
    /// 지정한 공격 범위에서 처음 발견한 대상에게 피해를 적용한다.
    /// </summary>
    private void ApplyDamage(Vector2 center, Vector2 size, float angle)
    {
        Collider2D hit = Physics2D.OverlapBox(
            center,
            size,
            angle,
            targetMask
        );

        hit?.GetComponentInParent<Health>()?.TakeDamage(AttackDamage);
    }

    /// <summary>
    /// 공격 방향을 유지하며 플레이어를 따라가는 베기 이펙트를 재생한다.
    /// </summary>
    private void SpawnSlashEffect()
    {
        if (slashEffects == null)
        {
            return;
        }

        SlashEffect effect = GetAvailableSlashEffect();
        if (effect == null)
        {
            return;
        }

        Vector2 scale = new(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y)
        );
        Vector2 perpendicular = new(-attackAimDirection.y, attackAimDirection.x);
        Vector2 offset = attackAimDirection * slashEffectOffset.x * scale.x
            + perpendicular * slashEffectOffset.y * scale.y;

        int sortingLayer = playerRenderer != null ? playerRenderer.sortingLayerID : 0;
        int sortingOrder = playerRenderer != null ? playerRenderer.sortingOrder - 1 : 0;

        effect.Play(
            transform,
            offset,
            scale * slashEffectScale,
            attackAimDirection,
            sortingLayer,
            sortingOrder
        );
    }

    /// <summary>
    /// 공격 시 재사용할 베기 이펙트를 미리 생성한다.
    /// </summary>
    private void CreateSlashEffectPool()
    {
        if (slashEffectPrefab == null)
        {
            return;
        }

        SlashEffect prefabEffect = slashEffectPrefab.GetComponent<SlashEffect>();
        if (prefabEffect == null)
        {
            Debug.LogError("SlashEffect 프리팹에 SlashEffect가 없습니다.", this);
            return;
        }

        slashEffects = new SlashEffect[slashEffectPoolSize];

        for (int index = 0; index < slashEffects.Length; index++)
        {
            SlashEffect effect = Instantiate(prefabEffect);
            effect.name = $"SlashEffect_{index}";
            effect.Clear();
            slashEffects[index] = effect;
        }
    }

    /// <summary>
    /// 현재 재생 중이지 않은 베기 이펙트를 반환한다.
    /// </summary>
    private SlashEffect GetAvailableSlashEffect()
    {
        for (int index = 0; index < slashEffects.Length; index++)
        {
            if (!slashEffects[index].IsPlaying)
            {
                return slashEffects[index];
            }
        }

        return null;
    }

    /// <summary>
    /// 공격 주체가 제거될 때 생성한 베기 이펙트 풀을 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (slashEffects == null)
        {
            return;
        }

        for (int index = 0; index < slashEffects.Length; index++)
        {
            if (slashEffects[index] != null)
            {
                Destroy(slashEffects[index].gameObject);
            }
        }
    }

    /// <summary>
    /// 현재 공격 방향에 맞는 타격 판정 중심을 계산한다.
    /// </summary>
    private Vector2 GetAttackCenter()
    {
        return (body != null ? body.position : (Vector2)transform.position)
            + attackAimDirection * collideBoxSize.x * 0.5f;
    }

    /// <summary>
    /// 현재 일반 공격 조준 방향을 각도로 변환한다.
    /// </summary>
    private float GetAttackAngle()
    {
        return Mathf.Atan2(attackAimDirection.y, attackAimDirection.x) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// 지정한 공격 판정 범위를 디버그 선으로 표시한다.
    /// </summary>
    private void DrawAttackBox(Vector2 center, float angle)
    {
        Vector2 halfSize = collideBoxSize * 0.5f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        Vector2 bottomLeft = center + (Vector2)(rotation * new Vector2(-halfSize.x, -halfSize.y));
        Vector2 bottomRight = center + (Vector2)(rotation * new Vector2(halfSize.x, -halfSize.y));
        Vector2 topLeft = center + (Vector2)(rotation * new Vector2(-halfSize.x, halfSize.y));
        Vector2 topRight = center + (Vector2)(rotation * new Vector2(halfSize.x, halfSize.y));

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
