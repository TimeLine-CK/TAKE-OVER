/// <summary>
/// 스크립트 최종 수정일: 2026-10-29
/// 기능: 태양광 패널에 닿은 빛으로 배터리를 충전하고 표시 슬롯과 연결된 문을 갱신한다.
/// </summary>

using UnityEngine;

[DefaultExecutionOrder(200)]
public sealed class SolarBattery : MonoBehaviour
{
    [SerializeField] private Collider2D solarPanel;                             // 빛을 받을 패널의 충돌체
    [SerializeField] private GameObject[] energySlots = new GameObject[10];     // 아래부터 켤 충전 표시
    [SerializeField] private SlidingDoor door;                                  // 완충되면 열릴 문
    [SerializeField, Range(0f, 100f)] private float charge;                     // 현재 충전량
    [SerializeField] private float chargePerSecond = 10f;                       // 초당 충전량

    public float Charge => charge;
    public bool IsFullyCharged => charge >= 100f;
    public bool IsReceivingLight => illuminatedFrame == Time.frameCount;

    private int illuminatedFrame = -1;
    private int visibleSlotCount = -1;

    private void Awake()
    {
        Debug.Assert(solarPanel != null, "SolarBattery의 Solar Panel을 설정해 주세요.", this);
        Debug.Assert(energySlots.Length == 10, "SolarBattery의 Energy Slots는 10개를 설정해 주세요.", this);
        Debug.Assert(door != null, "SolarBattery의 Door를 설정해 주세요.", this);
        RestoreCharge(charge);
    }

    /// <summary>
    /// 지정한 패널에 광선이 닿은 프레임만 충전 대상으로 기록한다.
    /// </summary>
    public void ReceiveLight(Collider2D surface)
    {
        if (isActiveAndEnabled && surface != null && surface == solarPanel)
            illuminatedFrame = Time.frameCount;
    }

    private void LateUpdate()
    {
        if (!IsReceivingLight || IsFullyCharged)
            return;

        charge = Mathf.MoveTowards(charge, 100f, Mathf.Max(0f, chargePerSecond) * Time.deltaTime);
        UpdateSlots();
        if (IsFullyCharged && door != null)
            door.SetOpen(true);
    }

    /// <summary>
    /// 저장된 충전량과 표시, 문의 열림 상태를 복원한다.
    /// </summary>
    public void RestoreCharge(float value)
    {
        charge = Mathf.Clamp(value, 0f, 100f);
        illuminatedFrame = -1;
        visibleSlotCount = -1;
        UpdateSlots();
        if (door != null)
            door.SetOpen(IsFullyCharged);
    }

    private void UpdateSlots()
    {
        int count = Mathf.Clamp(Mathf.FloorToInt(charge / 10f), 0, 10);
        if (visibleSlotCount == count)
            return;

        visibleSlotCount = count;
        for (int index = 0; index < energySlots.Length; index++)
            if (energySlots[index] != null)
                energySlots[index].SetActive(index < count);
    }
}
