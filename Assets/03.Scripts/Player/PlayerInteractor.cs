/// <summary>
/// 스크립트 최종 수정일: 2026-10-30
/// 기능: 가까운 장치를 선택하고 시대와 전환 준비 상태에 따라 F 상호작용을 처리한다.
/// </summary>

using UnityEngine;

public sealed class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float interactionRange = 2f;       // 장치를 조작할 수 있는 거리
    [SerializeField] private LayerMask interactionMask = ~0;    // 상호작용 대상을 탐색할 레이어
    [SerializeField] private TMPro.TMP_Text promptLabel;        // 씬에 배치한 상호작용 안내 텍스트

    public Interactable CurrentTarget
    {
        get;
        private set;
    }

    private PlayerInputReader inputReader;
    private TimeShiftController timeShift;
    private Health health;
    private Collider2D targetCollider;
    private Collider2D heldCollider;
    private Interactable heldTarget; // F키를 누르기 시작한 장치
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

        UpdateHeldInteraction();
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
        heldTarget = CurrentTarget;
        heldCollider = targetCollider;
        return true;
    }

    /// <summary>
    /// 키를 놓거나 장치에서 멀어지면 조작을 멈춘다.
    /// </summary>
    private void UpdateHeldInteraction()
    {
        if (inputReader == null || !inputReader.IsInteractHeld || heldTarget == null || heldCollider == null
            || !heldCollider.enabled || !heldTarget.gameObject.activeInHierarchy
            || (health != null && health.IsDead)
            || (timeShift != null && (!timeShift.IsReady || timeShift.IsPreparing))
            || !heldTarget.CanInteract(GetCurrentFlag())
            || ((Vector2)transform.position - heldCollider.ClosestPoint(transform.position)).sqrMagnitude
                > interactionRange * interactionRange)
        {
            heldTarget = null;
            heldCollider = null;
            return;
        }

        heldTarget.HoldInteract(this, Time.deltaTime);
    }

    /// <summary>
    /// 조작 가능한 장치를 우선 선택하고 없으면 가까운 장치의 제한 안내를 표시한다.
    /// </summary>
    private Interactable FindNearestTarget()
    {
        targetCollider = null;
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
        bool nearestCanInteract = false;
        TimeFlag currentFlag = GetCurrentFlag();

        for (int index = 0; index < count; index++)
        {
            Collider2D collider = nearbyColliders[index];
            Interactable candidate = collider.GetComponentInParent<Interactable>();
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;
            if (candidate is MirrorReflector2D mirror && mirror.IsFixed)
                continue;
            float distance = (collider.ClosestPoint(position) - position).sqrMagnitude;
            bool canInteract = candidate.CanInteract(currentFlag);
            if (nearestCanInteract && !canInteract)
                continue;
            if (canInteract == nearestCanInteract && distance >= nearestDistance)
                continue;
            nearestCanInteract = canInteract;
            nearestDistance = distance;
            nearest = candidate;
            targetCollider = collider;
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
        heldTarget = null;
        heldCollider = null;
        if (promptLabel != null)
            promptLabel.gameObject.SetActive(false);
    }
}
