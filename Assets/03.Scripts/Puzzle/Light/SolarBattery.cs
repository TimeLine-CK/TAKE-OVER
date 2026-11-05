/// <summary>
/// 스크립트 최종 수정일: 2026-11-02
/// 기능: 지정한 패널에 닿은 빛으로 배터리를 충전한다.
/// </summary>
using UnityEngine;
[DefaultExecutionOrder(200)]
public sealed class SolarBattery : BatteryCharge
{
    [SerializeField] private Collider2D solarPanel;
    private int illuminatedFrame = -1;
    public bool IsReceivingLight => illuminatedFrame == Time.frameCount;
    protected override void Awake()
    {
        base.Awake();
        if (solarPanel != null)
            solarPanel.isTrigger = true;
    }

    public bool IsSolarPanel(Collider2D surface) => surface != null && surface == solarPanel;

    public void ReceiveLight(Collider2D surface)
    {
        if (isActiveAndEnabled && surface != null && surface == solarPanel)
            illuminatedFrame = Time.frameCount;
    }
    protected override bool CanCharge()
    {
        return IsReceivingLight;
    }
}
