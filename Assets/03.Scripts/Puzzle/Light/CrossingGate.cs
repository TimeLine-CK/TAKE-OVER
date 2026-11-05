/// <summary>
/// 스크립트 최종 수정일: 2026-11-03
/// 기능: F 입력으로 차단기를 전환하고 손잡이와 저장 상태를 갱신한다.
/// </summary>
using UnityEngine;
[DisallowMultipleComponent]
public sealed class CrossingGate : Interactable
{
    public string GateId;
    public string PuzzleId; // 같은 배터리에 연결할 퍼즐 ID
    public UltravioletCode Code;
    public GameObject Grip_F;
    public GameObject Grip_T;
    public bool IsOn
    {
        get; private set;
    }
    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(GateId))
            GateId = gameObject.scene.path + "/" + name;
        StageProgress.Switches.TryGetValue(GateId, out bool value);
        SetOn(value);
    }
    public override void Interact(PlayerInteractor player)
    {
        TimeFlag flag = TimeFlag.Present;
        if (player != null && player.TryGetComponent(out TimeShiftController shift))
            flag = shift.CurrentFlag;
        if (!CanInteract(flag))
            return;
        SetOn(!IsOn);
        base.Interact(player);
    }
    public void SetOn(bool value)
    {
        IsOn = value;
        StageProgress.Switches[GateId] = value;
        if (Grip_F != null)
            Grip_F.SetActive(!value);
        if (Grip_T != null)
            Grip_T.SetActive(value);
    }
}
