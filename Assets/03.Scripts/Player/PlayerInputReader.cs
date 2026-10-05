/// <summary>
/// 스크립트 최종 수정일: 2026-09-29
/// 기능: Unity Input System에서 이동과 행동 입력을 읽어 게임플레이 코드에 전달한다.
/// </summary>

using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInputReader : MonoBehaviour
{
    private const string MoveActionName = "Move";                   // 이동 액션 이름
    private const string JumpActionName = "Jump";                   // 점프 액션 이름
    private const string AttackActionName = "Attack";               // 공격 액션 이름
    private const string CloneToggleActionName = "CloneToggle";     // 잔상 녹화 액션 이름
    private const string ExecuteActionName = "Execute";             // 잔상 실행 액션 이름
    private const string TeleportActionName = "Teleport";           // 분신 위치 텔레포트 액션 이름
    private const string RollActionName = "Roll";                   // 구르기 액션 이름
    private const string DashActionName = "Dash";                   // 돌진 커맨드 액션 이름

    public Vector2 Move { get; private set; }              // 현재 이동 입력값 프로퍼티
    public Vector2 PointerPosition => Mouse.current != null
        ? Mouse.current.position.ReadValue()
        : Vector2.zero;                                    // 현재 마우스 화면 좌표 프로퍼티

    [SerializeField] private Texture2D pointerCursor;      // 게임에서 사용할 마우스 포인터 이미지

    private PlayerInput playerInput;                // Input Action Asset을 사용하는 PlayerInput
    private InputAction moveAction;                 // 이동 액션
    private InputAction jumpAction;                 // 점프 액션
    private InputAction attackAction;               // 공격 액션
    private InputAction cloneToggleAction;          // 잔상 녹화 전환 액션
    private InputAction executeAction;              // 잔상 실행 및 회수 액션
    private InputAction teleportAction;             // 분신 위치 텔레포트 액션
    private InputAction rollAction;                 // 구르기 액션
    private InputAction dashAction;                 // 돌진 커맨드 액션
    private bool jumpPressed;                       // 소비되지 않은 점프 입력 여부
    private bool attackPressed;                     // 소비되지 않은 공격 입력 여부
    private bool cloneTogglePressed;                // 소비되지 않은 잔상 녹화 입력 여부
    private bool executePressed;                    // 소비되지 않은 잔상 실행 입력 여부
    private bool teleportPressed;                   // 소비되지 않은 텔레포트 입력 여부
    private bool rollPressed;                       // 소비되지 않은 구르기 입력 여부
    private bool dashPressed;                       // 소비되지 않은 돌진 입력 여부
    private bool uppercutPressed;                   // 소비되지 않은 올려치기 입력 여부

    /// <summary>
    /// PlayerInput에서 사용할 액션들을 찾아 저장.
    /// </summary>
    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions.FindAction(MoveActionName, true);
        jumpAction = playerInput.actions.FindAction(JumpActionName, true);
        attackAction = playerInput.actions.FindAction(AttackActionName, true);
        cloneToggleAction = playerInput.actions.FindAction(CloneToggleActionName, true);
        executeAction = playerInput.actions.FindAction(ExecuteActionName, true);
        teleportAction = playerInput.actions.FindAction(TeleportActionName, true);
        rollAction = playerInput.actions.FindAction(RollActionName, true);
        dashAction = playerInput.actions.FindAction(DashActionName, true);

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

    /// <summary>
    /// 현재 이동 액션 값을 매 프레임 갱신.
    /// </summary>
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

    /// <summary>
    /// 점프 입력을 한 번만 반환하고 소비한다.
    /// </summary>

    public bool ConsumeJumpPressed()
    {
        if (!jumpPressed) return false;
        jumpPressed = false;
        return true;
    }

    /// <summary>
    /// 공격 입력을 한 번만 반환하고 소비한다.
    /// </summary>
    public bool ConsumeAttackPressed()
    {
        if (!attackPressed) return false;
        attackPressed = false;
        return true;
    }

    /// <summary>
    /// 잔상 녹화 입력을 한 번만 반환하고 소비한다.
    /// </summary>
    public bool ConsumeCloneTogglePressed()
    {
        if (!cloneTogglePressed) return false;
        cloneTogglePressed = false;
        return true;
    }

    /// <summary>
    /// 잔상 실행 입력을 한 번만 반환하고 소비한다.
    /// </summary>
    public bool ConsumeExecutePressed()
    {
        if (!executePressed) return false;
        executePressed = false;
        return true;
    }

    /// <summary>
    /// 잔상 위치 텔레포트 입력을 한 번만 반환하고 소비한다.
    /// </summary>
    public bool ConsumeTeleportPressed()
    {
        if (!teleportPressed) return false;
        teleportPressed = false;
        return true;
    }

    /// <summary>
    /// 구르기 입력을 한 번만 반환하고 소비한다.
    /// </summary>
    public bool ConsumeRollPressed()
    {
        if (!rollPressed) return false;
        rollPressed = false;
        return true;
    }

    /// <summary>
    /// 돌진 입력을 한 번만 반환하고 소비한다.
    /// </summary>
    public bool ConsumeDashPressed()
    {
        if (!dashPressed) return false;
        dashPressed = false;
        return true;
    }

    /// <summary>
    /// 올려치기 입력을 한 번만 반환하고 소비한다.
    /// </summary>
    public bool ConsumeUppercutPressed()
    {
        if (!uppercutPressed) return false;
        uppercutPressed = false;
        return true;
    }


    /// <summary>
    /// 아직 소비되지 않은 일회성 입력을 모두 제거한다.
    /// </summary>
    public void ClearPendingInputs()
    {
        jumpPressed = false;
        attackPressed = false;
        cloneTogglePressed = false;
        executePressed = false;
        teleportPressed = false;
        rollPressed = false;
        dashPressed = false;
        uppercutPressed = false;
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
            jumpPressed = true;
        }
        else if (context.action == attackAction)
        {
            if (Keyboard.current != null && Keyboard.current.wKey.isPressed)
            {
                uppercutPressed = true;
            }
            else
            {
                attackPressed = true;
            }
        }
        else if (context.action == cloneToggleAction)
        {
            cloneTogglePressed = true;
        }
        else if (context.action == executeAction)
        {
            executePressed = true;
        }

        else if (context.action == teleportAction)
        {
            teleportPressed = true;
        }
        else if (context.action == rollAction)
        {
            rollPressed = true;
        }
        else if (context.action == dashAction)
        {
            dashPressed = true;
        }

    }

}
