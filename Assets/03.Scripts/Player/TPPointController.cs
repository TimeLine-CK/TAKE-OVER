/// <summary>
/// 스크립트 최종 수정일: 2026-09-28
/// 기능: 플레이어의 TP Point 소비와 회복을 관리한다.
/// </summary>

using UnityEngine;

public sealed class TPPointController : MonoBehaviour
{
    [Header("TP Point")]
    [Min(1f)] public float maximumPoint = 100f;                                                     // 최대 TP Point
    [Min(0f)] public float recoveryPerSecond = 30f;                                                 // 초당 TP 회복량

    public float CurrentPoint { get; private set; }                                                 // 현재 TP Point
    public float NormalizedPoint => maximumPoint <= 0f ? 0f: CurrentPoint / maximumPoint;           // UI에 사용할 TP 비율

    private EchoController echoController;                                                          // 녹화 상태를 제공하는 컴포넌트
    private PlayerCommandController commandController;                                              // 커맨드 상태를 제공하는 컴포넌트

    /// <summary>
    /// TP를 최대치로 채우고 상태 제공 컴포넌트를 찾는다.
    /// </summary>
    private void Awake()
    {
        echoController = GetComponent<EchoController>();
        commandController = GetComponent<PlayerCommandController>();
        CurrentPoint = maximumPoint;
    }

    /// <summary>
    /// 녹화와 커맨드 동작 중이 아닐 때 TP를 회복한다.
    /// </summary>
    private void Update()
    {
        if ((echoController != null && echoController.IsRecording)
            || (commandController != null && commandController.IsCommandActive))
        {
            return;
        }

        CurrentPoint = Mathf.MoveTowards(
            CurrentPoint,
            maximumPoint,
            recoveryPerSecond * Time.unscaledDeltaTime
        );
    }

    /// <summary>
    /// 필요한 TP가 남아 있다면 지정한 양을 소비한다.
    /// </summary>
    public bool TryUse(float amount)
    {
        amount = Mathf.Max(0f, amount);

        if (CurrentPoint < amount)
            return false;

        CurrentPoint -= amount;
        return true;
    }

    /// <summary>
    /// Inspector에서 변경한 수치를 유효 범위로 제한한다.
    /// </summary>
    private void OnValidate()
    {
        maximumPoint = Mathf.Max(1f, maximumPoint);
        recoveryPerSecond = Mathf.Max(0f, recoveryPerSecond);
        CurrentPoint = Mathf.Clamp(CurrentPoint, 0f, maximumPoint);
    }
}
