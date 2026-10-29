/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 과거 밸브 상태와 현재 누출 차단 이력을 관리한다.
/// </summary>

using UnityEngine;

public sealed class GasValve : MonoBehaviour
{
    [SerializeField] private string valveId = "RoomGas"; // 연결할 가스와 콘솔 식별자
    [SerializeField] private GasArea presentGas; // 현재의 가스 영역
    [SerializeField] private GasArea pastGas;    // 과거의 가스 영역
    [SerializeField] private bool isOpen = true; // 과거 밸브의 열림 여부
    [SerializeField] private bool hasClosedInPast; // 과거에서 한 번이라도 차단했는지 여부

    public bool IsOpen => isOpen;
    public bool HasClosedInPast => hasClosedInPast;
    public string ValveId => valveId;
    private TimeShiftController timeShift;

    /// <summary>
    /// 과거 씬을 불러온 뒤 양쪽 가스 영역을 연결한다.
    /// </summary>
    private void OnEnable()
    {
        timeShift = FindFirstObjectByType<TimeShiftController>();
        if (timeShift == null)
        {
            ConnectAreas();
            return;
        }
        timeShift.WorldsReady += ConnectAreas;
        if (timeShift.IsReady)
            ConnectAreas();
    }

    private void OnDisable()
    {
        if (timeShift != null)
            timeShift.WorldsReady -= ConnectAreas;
    }

    private void ConnectAreas()
    {
        foreach (GasArea area in FindObjectsByType<GasArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (area.ValveId != valveId)
                continue;
            TimeFlagWolrdRoot world = area.GetComponentInParent<TimeFlagWolrdRoot>(true);
            if (world == null)
                continue;
            if (world.TimeFlag == TimeFlag.Present)
                presentGas = area;
            else
                pastGas = area;
        }
        ApplyState();
    }

    /// <summary>
    /// 밸브를 열거나 닫고, 과거에서 닫은 기록을 현재 가스에 반영한다.
    /// </summary>
    public void Toggle()
    {
        isOpen = !isOpen;
        if (!isOpen)
            hasClosedInPast = true;
        ApplyState();
    }

    public void RestoreState(bool open, bool closedInPast)
    {
        isOpen = open;
        hasClosedInPast = closedInPast;
        ApplyState();
    }

    private void ApplyState()
    {
        if (pastGas != null)
            pastGas.SetLeaking(isOpen);
        if (presentGas != null)
            presentGas.SetLeaking(!hasClosedInPast);
    }
}
