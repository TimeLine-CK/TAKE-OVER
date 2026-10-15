/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: Unity Input System에서 이동, 공격, 시간 전환과 상호작용 입력을 읽어 전달한다.
/// </summary>

using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInputReader : MonoBehaviour
{
    private const string MoveActionName = "Move";                   // 이동 액션 이름
    private const string JumpActionName = "Jump";                   // 점프 액션 이름
    private const string AttackActionName = "Attack";               // 공격 액션 이름
    private const string RollActionName = "Roll";                   // 구르기 액션 이름
    private const string TimeShiftActionName = "TimeShift";         // 시간 전환 액션 이름
    private const string InteractActionName = "Interact";           // 상호작용 액션 이름

    public Vector2 Move { get; private set; }              // 현재 이동 입력값 프로퍼티
    public Vector2 PointerPosition => Mouse.current != null
        ? Mouse.current.position.ReadValue()
        : Vector2.zero;                                    // 현재 마우스 화면 좌표 프로퍼티

    [SerializeField] private Texture2D pointerCursor;      // 게임에서 사용할 마우스 포인터 이미지

    private PlayerInput playerInput;                // Input Action Asset을 사용하는 PlayerInput
    private InputAction moveAction;                 // 이동 액션
    private InputAction jumpAction;                 // 점프 액션
    private InputAction attackAction;               // 공격 액션
    private InputAction rollAction;                 // 구르기 액션
    private InputAction timeShiftAction;            // 시간 전환 액션
    private InputAction interactAction;             // 상호작용 액션

    private bool isJumpPressed;                     // 점프 입력 여부
    private bool isAttackPressed;                   // 공격 입력 여부
    private bool isRollPressed;                     // 구르기 입력 여부
    private bool isTimeShiftPressed;                // 시간 전환 입력 여부
    private bool isInteractPressed;                 // 상호작용 입력 여부

    /// <summary>
    /// PlayerInput에서 사용할 액션들을 찾아 저장.
    /// </summary>
    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions.FindAction(MoveActionName, true);
        jumpAction = playerInput.actions.FindAction(JumpActionName, true);
        attackAction = playerInput.actions.FindAction(AttackActionName, true);
        rollAction = playerInput.actions.FindAction(RollActionName, true);
        timeShiftAction = playerInput.actions.FindAction(TimeShiftActionName, true);
        interactAction = playerInput.actions.FindAction(InteractActionName, true);

        if (pointerCursor != null)
        {
            Vector2 hotspot = new(pointerCursor.width * 0.5f, pointerCursor.height * 0.5f);
            Cursor.SetCursor(pointerCursor, hotspot, CursorMode.Auto);
        }
    }

    /// <summary>
    /// 입력 이벤트를 구독하고 대기 중인 입력을 초기화.
    /// </summary>
    private void OnEnable()
    {
        playerInput.onActionTriggered += OnActionTriggered;
        ClearPendingInputs();
    }

    private void Update()
    {
        Move = moveAction.ReadValue<Vector2>();
    }

    /// <summary>
    /// 입력 이벤트 구독을 해제하고 입력 상태를 초기화.
    /// </summary>
    private void OnDisable()
    {
        playerInput.onActionTriggered -= OnActionTriggered;
        Move = Vector2.zero;
        ClearPendingInputs();
    }

    public bool ConsumeJumpPressed()
    {
        if (!isJumpPressed)
            return false;
        isJumpPressed = false;
        return true;
    }

    public bool ConsumeAttackPressed()
    {
        if (!isAttackPressed)
            return false;
        isAttackPressed = false;
        return true;
    }

    public bool ConsumeRollPressed()
    {
        if (!isRollPressed)
            return false;
        isRollPressed = false;
        return true;
    }

    public bool ConsumeTimeShiftPressed()
    {
        if(!isTimeShiftPressed)
            return false;
        isTimeShiftPressed = false;
        return true;
    }

    public bool ConsumeInteractPressed()
    {
        if (!isInteractPressed)
            return false;
        isInteractPressed = false;
        return true;
    }

    public void ClearPendingInputs()
    {
        isJumpPressed = false;
        isAttackPressed = false;
        isRollPressed = false;
        isTimeShiftPressed = false;
        isInteractPressed = false;
    }

    /// <summary>
    /// 수행된 Input Action을 게임에서 소비할 상태값으로 변환한다.
    /// </summary>
    private void OnActionTriggered(InputAction.CallbackContext context)
    {
        if (!context.performed)
        {
            return;
        }

        if (context.action == jumpAction)
        {
            isJumpPressed = true;
        }
        else if (context.action == attackAction)
        {
            isAttackPressed = true;
        }
        else if (context.action == rollAction)
        {
            isRollPressed = true;
        }
        else if (context.action == timeShiftAction)
        {
            isTimeShiftPressed = true;
        }
        else if (context.action == interactAction)
        {
            isInteractPressed = true;
        }

    }

}
