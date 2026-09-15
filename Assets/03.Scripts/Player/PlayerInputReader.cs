/// <summary>
/// 스크립트 최종 수정일: 2026-09-10
/// 기능: Unity Input System에서 이동과 행동 입력을 읽어 게임플레이 코드에 전달한다.
/// </summary>

using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInputReader : MonoBehaviour
{
    private const string MoveActionName = "Move"; // 이동 액션 이름
    private const string JumpActionName = "Jump"; // 점프 액션 이름
    private const string AttackActionName = "Attack"; // 공격 액션 이름
    private const string CloneToggleActionName = "CloneToggle"; // 잔상 녹화 액션 이름
    private const string SelectSlotActionName = "SelectSlot"; // 슬롯 선택 액션 이름
    private const string ExecuteActionName = "Execute"; // 잔상 실행 액션 이름

    public Vector2 Move { get; private set; } // 현재 이동 입력값

    private PlayerInput playerInput; // Input Action Asset을 사용하는 PlayerInput
    private InputAction moveAction; // 이동 액션
    private InputAction jumpAction; // 점프 액션
    private InputAction attackAction; // 공격 액션
    private InputAction cloneToggleAction; // 잔상 녹화 전환 액션
    private InputAction selectSlotAction; // 잔상 슬롯 선택 액션
    private InputAction executeAction; // 잔상 실행 및 회수 액션
    private bool jumpPressed; // 소비되지 않은 점프 입력 여부
    private bool attackPressed; // 소비되지 않은 공격 입력 여부
    private bool cloneTogglePressed; // 소비되지 않은 잔상 녹화 입력 여부
    private bool executePressed; // 소비되지 않은 잔상 실행 입력 여부
    private bool deleteRecordPressed; // 소비되지 않은 기록 삭제 입력 여부
    private bool exclusiveCloneInputThisFrame; // 같은 프레임의 중복 잔상 입력 차단 여부
    private int slotChange; // 소비되지 않은 슬롯 이동량

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
        selectSlotAction = playerInput.actions.FindAction(SelectSlotActionName, true);
        executeAction = playerInput.actions.FindAction(ExecuteActionName, true);
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
    /// 한 프레임 동안 유지한 잔상 입력 차단 상태를 해제.
    /// </summary>
    private void LateUpdate()
    {
        exclusiveCloneInputThisFrame = false;
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
    ///  ConsumeJumpPressed() ~ ConsumeSlotChange() 각 한번씩만 입력 받고 반환하는 역할을 수행.
    /// </summary>

    public bool ConsumeJumpPressed()
    {
        if (!jumpPressed) return false;
        jumpPressed = false;
        return true;
    }

  
    public bool ConsumeAttackPressed()
    {
        if (!attackPressed) return false;
        attackPressed = false;
        return true;
    }


    public bool ConsumeCloneTogglePressed()
    {
        if (!cloneTogglePressed) return false;
        cloneTogglePressed = false;
        return true;
    }

   
    public bool ConsumeExecutePressed()
    {
        if (!executePressed) return false;
        executePressed = false;
        return true;
    }

    
    public bool ConsumeDeleteRecordPressed()
    {
        if (!deleteRecordPressed) return false;
        deleteRecordPressed = false;
        return true;
    }

    public int ConsumeSlotChange()
    {
        int value = slotChange;
        slotChange = 0;
        return value;
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
        deleteRecordPressed = false;
        slotChange = 0;
    }

    /// <summary>
    /// 수행된 Input Action을 게임에서 소비할 상태값으로 변환한다.
    /// </summary>
    private void OnActionTriggered(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (context.action == jumpAction)
            jumpPressed = true;
        else if (context.action == attackAction)
            attackPressed = true;
        else if (context.action == cloneToggleAction)
        {
            bool isDelete = Keyboard.current != null
                && context.control == Keyboard.current.rKey
                && (Keyboard.current.leftCtrlKey.isPressed
                    || Keyboard.current.rightCtrlKey.isPressed);

            slotChange = 0;
            executePressed = false;
            exclusiveCloneInputThisFrame = true;

            if (isDelete)
                deleteRecordPressed = true;
            else
                cloneTogglePressed = true;
        }
        else if (context.action == selectSlotAction)
        {
            if (exclusiveCloneInputThisFrame)
                return;

            float value = context.ReadValue<float>();
            slotChange += value > 0f ? -1 : value < 0f ? 1 : 0;
        }
        else if (context.action == executeAction && !exclusiveCloneInputThisFrame)
            executePressed = true;
    }
}
