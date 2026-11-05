/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
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
    private Collider2D playerCollider;
    private Collider2D activeCollider;
    private MirrorReflector2D activeMirror;
    private RollingCarrier controlledCarrier;
    private Interactable heldTarget;

    public bool IsControllingCarrier => controlledCarrier != null && CanUseInteraction()
        && controlledCarrier.CanControl(this);
    private readonly Collider2D[] nearbyColliders = new Collider2D[64];

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        timeShift = GetComponent<TimeShiftController>();
        health = GetComponent<Health>();
        playerCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// 가까운 장치 안내를 갱신하고 F 입력을 한 번 소비한다.
    /// </summary>
    private void Update()
    {
        UpdateActiveInteraction();
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
        if (!CanUseInteraction())
            return false;
        if (controlledCarrier != null || activeMirror != null)
        {
            StopInteraction();
            return true;
        }
        RollingCarrier carrier = FindStandingCarrier();
        if (carrier != null && carrier.BeginControl(this))
        {
            controlledCarrier = carrier;
            return true;
        }
        CurrentTarget = FindNearestTarget();
        if (CurrentTarget == null || !CurrentTarget.CanInteract(GetCurrentFlag()))
            return false;
        CurrentTarget.Interact(this);
        if (CurrentTarget is MirrorReflector2D mirror)
        {
            activeMirror = mirror;
            activeCollider = targetCollider;
        }
        return true;
    }

    public void StopInteraction()
    {
        if (heldTarget != null)
            heldTarget.EndHoldInteract(this);
        heldTarget = null;
        if (controlledCarrier != null)
            controlledCarrier.EndControl(this);
        controlledCarrier = null;
        activeMirror = null;
        activeCollider = null;
    }

    /// <summary>
    /// F를 유지하는 동안 같은 장치에 시간을 전달하고 입력이나 범위를 벗어나면 중단한다.
    /// </summary>
    private void UpdateHeldInteraction()
    {
        Interactable target = null;
        if (inputReader != null && inputReader.IsInteractHeld && CanUseInteraction()
            && controlledCarrier == null && activeMirror == null
            && CurrentTarget != null && CurrentTarget.CanInteract(GetCurrentFlag()))
        {
            target = CurrentTarget;
        }
        if (heldTarget != target)
        {
            if (heldTarget != null)
                heldTarget.EndHoldInteract(this);
            heldTarget = target;
        }
        if (heldTarget != null)
            heldTarget.HoldInteract(this, Time.deltaTime);
    }

    private bool CanUseInteraction()
    {
        return isActiveAndEnabled && (health == null || !health.IsDead)
            && (timeShift == null || (timeShift.IsReady && !timeShift.IsPreparing));
    }

    /// <summary>
    /// 카트에서 내려오거나 장치를 사용할 수 없게 되면 조작 상태를 해제한다.
    /// </summary>
    private void UpdateActiveInteraction()
    {
        if (!CanUseInteraction())
        {
            StopInteraction();
            return;
        }
        if (controlledCarrier != null && !controlledCarrier.CanControl(this))
            StopInteraction();
        if (activeMirror == null)
            return;
        if (activeCollider == null || !activeCollider.enabled
            || !activeMirror.CanInteract(GetCurrentFlag())
            || ((Vector2)transform.position - activeCollider.ClosestPoint(transform.position)).sqrMagnitude
                > interactionRange * interactionRange)
        {
            StopInteraction();
            return;
        }
        if (inputReader != null)
            activeMirror.RotateFromScroll(inputReader.Scroll);
    }

    private RollingCarrier FindStandingCarrier()
    {
        if (playerCollider == null || !playerCollider.enabled)
            return null;
        Bounds bounds = playerCollider.bounds;
        ContactFilter2D filter = ContactFilter2D.noFilter;
        filter.useTriggers = false;
        int count = Physics2D.OverlapBox(new Vector2(bounds.center.x, bounds.min.y),
            new Vector2(bounds.size.x, 0.3f), 0f, filter, nearbyColliders);
        for (int i = 0; i < count; i++)
        {
            RollingCarrier carrier = nearbyColliders[i].GetComponentInParent<RollingCarrier>();
            if (carrier != null && carrier.CanControl(this))
                return carrier;
        }
        return null;
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
            if (candidate == null)
            {
                RollingCarrier carrier = collider.GetComponentInParent<RollingCarrier>();
                if (carrier != null)
                    candidate = carrier.GetComponentInChildren<MirrorReflector2D>();
            }
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
        StopInteraction();
        if (promptLabel != null)
            promptLabel.gameObject.SetActive(false);
    }
}
