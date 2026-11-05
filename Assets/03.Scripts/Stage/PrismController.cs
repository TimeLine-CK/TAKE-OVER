/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 두 스테이지의 배터리 완충과 적 전멸을 확인하고 프리즘 조작으로 하수도를 해금한다.
/// </summary>
using UnityEngine;
using UnityEngine.Events;
public sealed class PrismController : Interactable
{
    public string[] RequiredBatteryIds;
    public string[] RequiredClearScenes;
    public UnityEvent OnUnlocked = new();
    public override bool CanInteract(TimeFlag flag)
    {
        if (!base.CanInteract(flag) || StageProgress.SewerUnlocked
            || !StageProgress.AllCharged(RequiredBatteryIds)
            || RequiredClearScenes == null || RequiredClearScenes.Length == 0)
        {
            return false;
        }
        foreach (string scene in RequiredClearScenes)
        {
            if (string.IsNullOrWhiteSpace(scene)
                || !StageProgress.Switches.TryGetValue("StageClear/" + scene, out bool cleared) || !cleared)
            {
                return false;
            }
        }
        return true;
    }
    public override void Interact(PlayerInteractor player)
    {
        TimeFlag flag = TimeFlag.Present;
        if (player != null && player.TryGetComponent(out TimeShiftController shift))
            flag = shift.CurrentFlag;
        if (!CanInteract(flag))
            return;
        StageProgress.UnlockSewer();
        OnUnlocked.Invoke();
        base.Interact(player);
    }
}
