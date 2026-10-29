/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 정상 콘솔에서 가스를 제어하고 파손 콘솔에는 고장 안내를 표시한다.
/// </summary>

using UnityEngine;

public sealed class GasConsole : Interactable
{
    [SerializeField] private string valveId = "RoomGas"; // 다른 씬의 밸브를 연결할 식별자
    [SerializeField] private GasValve valve; // 제어할 밸브
    [SerializeField] private bool isBroken; // 파손되어 조작할 수 없는 콘솔
    [SerializeField] private SpriteRenderer indicator; // 작동 상태 표시등

    private void Awake()
    {
        if (valve != null)
            return;
        foreach (GasValve candidate in FindObjectsByType<GasValve>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.ValveId != valveId)
                continue;
            valve = candidate;
            break;
        }
    }

    public override bool CanInteract(TimeFlag currentFlag)
    {
        return !isBroken && valve != null && base.CanInteract(currentFlag);
    }

    public override string GetPrompt(TimeFlag currentFlag)
    {
        if (isBroken)
            return "Console damaged - try the past";
        if (valve != null && !valve.IsOpen)
            return "F - Open valve";
        return "F - Shut off gas";
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!isBroken && valve != null)
            valve.Toggle();
    }

    /// <summary>
    /// 파손 여부와 밸브 상태를 표시등에 반영한다.
    /// </summary>
    private void Update()
    {
        if (indicator == null)
            return;
        if (isBroken)
            indicator.color = new Color(1f, 0.2f, 0.15f);
        else if (valve != null && !valve.IsOpen)
            indicator.color = new Color(0.2f, 1f, 0.65f);
        else
            indicator.color = new Color(1f, 0.7f, 0.2f);
    }
}
