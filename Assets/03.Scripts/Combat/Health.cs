/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 전투 대상의 체력, 피해와 사망 상태를 관리한다.
/// </summary>

using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum DamageSource
{
    Other,
    Enemy,
    Environment,
    Player
}

public sealed class Health : MonoBehaviour
{
    [FormerlySerializedAs("maximumHealth")]
    public int MaximumHealth = 100;  // 최대 체력
    public event Action Died;           // 체력이 모두 소진됐을 때 전달하는 이벤트
    public event Action<DamageSource> Damaged; // 실제 피해를 받은 원인을 전달하는 이벤트
    public event Action Restored; // 체크포인트 상태를 적용했을 때 전달하는 이벤트

    public int CurrentHealth => initialized ? currentHealth : MaximumHealth; // 현재 체력
    public bool IsDead => CurrentHealth <= 0f;          // 사망 여부
    private int currentHealth;
    private bool initialized;

    private void Awake()
    {
        if (!initialized)
        {
            currentHealth = MaximumHealth;
            initialized = true;
        }
    }

    public void RestoreHealth()
    {
        RestoreState(MaximumHealth);
    }

    /// <summary>
    /// 저장된 체력을 적용하고 전투 컴포넌트의 사망 상태를 동기화한다.
    /// </summary>
    public void RestoreState(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, MaximumHealth);
        initialized = true;
        Restored?.Invoke();
    }

    /// <summary>
    /// 살아 있는 대상에게 피해를 적용하고 사망 이벤트를 전달한다.
    /// </summary>
    public void TakeDamage(int damage, DamageSource source = DamageSource.Other)
    {
        if (IsDead || damage <= 0f)
            return;

        currentHealth = Mathf.Max(0, CurrentHealth - damage);
        initialized = true;
        Damaged?.Invoke(source);

        if (IsDead)
            Died?.Invoke();
    }
}
