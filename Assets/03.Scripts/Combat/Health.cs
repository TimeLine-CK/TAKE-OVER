/// <summary>
/// 스크립트 최종 수정일: 2026-09-28
/// 기능: 전투 대상의 체력, 피해와 사망 상태를 관리한다.
/// </summary>

using System;
using UnityEngine;

public sealed class Health : MonoBehaviour
{
    public float maximumHealth = 100f;  // 최대 체력
    public event Action Died;           // 체력이 모두 소진됐을 때 전달하는 이벤트

    public float CurrentHealth { get; private set; }    // 현재 체력
    public bool IsDead => CurrentHealth <= 0f;          // 사망 여부

    /// <summary>
    /// 현재 체력을 최대 체력으로 초기화한다.
    /// </summary>
    private void Awake()
    {
        CurrentHealth = maximumHealth;
    }

    /// <summary>
    /// 살아 있는 대상에게 피해를 적용하고 사망 이벤트를 전달한다.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);

        if (IsDead)
            Died?.Invoke();
    }
}
