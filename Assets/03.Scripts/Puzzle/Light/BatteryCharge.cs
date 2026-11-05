/// <summary>
/// 스크립트 최종 수정일: 2026-11-02
/// 기능: 배터리 충전량과 표시, 완충 이벤트를 관리한다.
/// </summary>
using UnityEngine;
using UnityEngine.Events;
public abstract class BatteryCharge : MonoBehaviour
{
    public string BatteryId;
    public UnityEvent OnFullyCharged = new();
    public UnityEvent<bool> OnCompletionChanged = new();
    [SerializeField] private GameObject[] energySlots = new GameObject[10];
    [SerializeField, HideInInspector] private SlidingDoor door;
    [SerializeField, Range(0, 100)] private float charge;
    [SerializeField, Min(0)] private float chargePerSecond = 10;
    public float Charge => charge;
    public bool IsFullyCharged => charge >= 100;
    private bool completed;
    private int visibleSlotCount = -1;
    protected virtual void Awake()
    {
        if (door != null)
            OnCompletionChanged.AddListener(door.SetOpen);
        if (string.IsNullOrWhiteSpace(BatteryId))
            BatteryId = gameObject.scene.path + "/" + name;
        if (StageProgress.Charges.TryGetValue(BatteryId, out float saved))
            charge = saved;
        StageProgress.SetCharge(BatteryId, charge);
        UpdateSlots();
    }
    protected virtual void Start()
    {
        ApplyCompletion(true);
    }
    protected abstract bool CanCharge();
    protected virtual void LateUpdate()
    {
        if (IsFullyCharged || !CanCharge())
            return;
        float next = Mathf.MoveTowards(charge, 100, chargePerSecond * Time.deltaTime);
        if (Mathf.Approximately(next, charge))
            return;
        int previousSlots = visibleSlotCount;
        charge = next;
        StageProgress.SetCharge(BatteryId, charge);
        UpdateSlots();
        for (int i = previousSlots; i < visibleSlotCount; i++)
            GameSfx.Play(GameSfx.Cue.BatteryUp);
        ApplyCompletion();
    }
    public void RestoreCharge(float value)
    {
        charge = Mathf.Clamp(value, 0, 100);
        StageProgress.SetCharge(BatteryId, charge);
        UpdateSlots();
        ApplyCompletion();
    }
    private void ApplyCompletion(bool force = false)
    {
        bool changed = completed != IsFullyCharged;
        if (!changed && !force)
            return;
        completed = IsFullyCharged;
        OnCompletionChanged.Invoke(completed);
        if (completed && changed)
            OnFullyCharged.Invoke();
    }
    private void UpdateSlots()
    {
        int count = Mathf.Clamp(Mathf.FloorToInt(charge / 10), 0, 10);
        if (visibleSlotCount == count)
            return;
        visibleSlotCount = count;
        for (int i = 0; i < energySlots.Length; i++)
            if (energySlots[i] != null)
                energySlots[i].SetActive(i < count);
    }
}
