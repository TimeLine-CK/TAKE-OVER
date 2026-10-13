/// <summary>
/// 스크립트 최종 수정일: 2026-10-12
/// 기능: 시점별 월드 오브젝트를 하나의 루트로 묶어 활성 상태를 관리한다.
/// </summary>

using UnityEngine;

public sealed class TimeFlagWolrdRoot : MonoBehaviour
{
    [SerializeField] private TimeFlag timeFlag; // 이 루트가 나타내는 시점

    public TimeFlag TimeFlag => timeFlag;

    /// <summary>
    /// 해당 시점의 월드 전체를 활성화하거나 비활성화한다.
    /// </summary>
    public void SetWorldActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }
}
