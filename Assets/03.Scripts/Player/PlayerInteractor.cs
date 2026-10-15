/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 가까운 장치를 선택하고 시대와 전환 준비 상태에 따라 F 상호작용을 처리한다.
/// </summary>

using UnityEngine;

public sealed class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float interactionRange = 2f;       // 장치를 조작할 수 있는 거리
    [SerializeField] private LayerMask interactionMask = ~0;    // 상호작용 대상을 탐색할 레이어
    [SerializeField] private TMPro.TMP_Text promptLabel;        // 씬에 배치한 상호작용 안내 텍스트

    public Interactable CurrentTarget { get; private set; }

    private PlayerInputReader inputReader;
    private TimeShiftController timeShift;
    private Health health;
    private readonly Collider2D[] nearbyColliders = new Collider2D[64];

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        timeShift = GetComponent<TimeShiftController>();
        health = GetComponent<Health>();
    }

    /// <summary>
    /// 가까운 장치 안내를 갱신하고 F 입력을 한 번 소비한다.
    /// </summary>
    private void Update()
    {
        CurrentTarget = FindNearestTarget();
        UpdatePrompt();

        if (inputReader != null && inputReader.ConsumeInteractPressed())
            TryInteract();
    }

    /// <summary>
    /// 준비 중 입력을 버리고 현재 시대에서 조작 가능한 장치만 실행한다.
    /// </summary>
    public bool TryInteract()
    {
        if (!isActiveAndEnabled || (health != null && health.IsDead))
            return false;
        if (timeShift != null && (!timeShift.IsReady || timeShift.IsPreparing))
            return false;

        CurrentTarget = FindNearestTarget();
        if (CurrentTarget == null || !CurrentTarget.CanInteract(GetCurrentFlag()))
            return false;

        CurrentTarget.Interact(this);
        return true;
    }

    /// <summary>
    /// 탐색 범위 안에서 콜라이더까지의 거리가 가장 가까운 장치를 선택한다.
    /// </summary>
    private Interactable FindNearestTarget()
    {
        if (health != null && health.IsDead)
            return null;
        if (timeShift != null && !timeShift.IsReady)
            return null;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(interactionMask);
        filter.useTriggers = true;
        Vector2 position = transform.position;
        int count = Physics2D.OverlapCircle(position, Mathf.Max(0f, interactionRange), filter, nearbyColliders);
        float nearestDistance = float.PositiveInfinity;
        Interactable nearest = null;

        for (int index = 0; index < count; index++)
        {
            Collider2D collider = nearbyColliders[index];
            Interactable candidate = collider.GetComponentInParent<Interactable>();
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;
            float distance = (collider.ClosestPoint(position) - position).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;
            nearestDistance = distance;
            nearest = candidate;
        }
        return nearest;
    }

    private TimeFlag GetCurrentFlag()
    {
        if (timeShift != null)
            return timeShift.CurrentFlag;
        return TimeFlag.Present;
    }

    /// <summary>
    /// 장치 조작 안내 또는 시대와 전환 준비 제한을 표시한다.
    /// </summary>
    private void UpdatePrompt()
    {
        if (promptLabel == null)
            return;
        promptLabel.gameObject.SetActive(CurrentTarget != null);
        if (CurrentTarget == null)
            return;

        if (timeShift != null && timeShift.IsPreparing)
            promptLabel.text = "Preparing time shift";
        else
            promptLabel.text = CurrentTarget.GetPrompt(GetCurrentFlag());
    }

    private void OnDisable()
    {
        CurrentTarget = null;
        if (promptLabel != null)
            promptLabel.gameObject.SetActive(false);
    }
}
