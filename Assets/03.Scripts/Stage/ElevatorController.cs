/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 엘리베이터의 목적지 선택과 세이브포인트에서의 복귀 메뉴를 처리한다.
/// </summary>

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ElevatorController : Interactable
{
    public GameObject Menu;
    public GameObject SewerOption;
    public string InfraredScene = "G_Pr_2";
    public string UltravioletScene = "G_Pr_3";
    public string SewerScene;
    public Button InfraredButton;
    public Button UltravioletButton;
    public Button SewerButton;
    public Button CloseButton;
    public Button TravelButton;

    [SerializeField] private GameObject[] selectionHighlights = System.Array.Empty<GameObject>();

    [SerializeField] private bool returnToHubOnly; // 세이브포인트 복귀 메뉴
    [SerializeField] private string hubScene = "G_Pr_1";

    private PlayerInteractor currentPlayer;
    private PlayerInputReader playerInput;
    private Health playerHealth;
    private InputAction cancelAction;
    private bool inputWasEnabled;
    private int selectedDestination;

    private void Awake()
    {
        if (InfraredButton != null)
            InfraredButton.onClick.AddListener(SelectInfrared);
        if (UltravioletButton != null)
            UltravioletButton.onClick.AddListener(SelectUltraviolet);
        if (SewerButton != null)
            SewerButton.onClick.AddListener(SelectSewer);
        if (CloseButton != null)
            CloseButton.onClick.AddListener(Close);
        if (TravelButton != null)
            TravelButton.onClick.AddListener(TravelSelected);
        Close();
    }

    private void OnEnable()
    {
        StageProgress.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        StageProgress.Changed -= Refresh;
        Close();
    }

    private void Refresh()
    {
        if (SewerOption != null)
            SewerOption.SetActive(!returnToHubOnly && StageProgress.SewerUnlocked);
        if (returnToHubOnly && UltravioletButton != null)
            UltravioletButton.gameObject.SetActive(false);
        if (selectedDestination == 2 && !StageProgress.SewerUnlocked)
            selectedDestination = 0;
        RefreshSelection();
    }

    public override bool CanInteract(TimeFlag flag)
    {
        return (returnToHubOnly || flag == TimeFlag.Present) && base.CanInteract(flag);
    }

    public override void Interact(PlayerInteractor player)
    {
        TimeFlag flag = TimeFlag.Present;
        if (player != null && player.TryGetComponent(out TimeShiftController shift))
            flag = shift.CurrentFlag;
        if (!CanInteract(flag) || Menu == null || Menu.activeSelf)
            return;

        currentPlayer = player;
        if (player != null)
        {
            playerInput = player.GetComponent<PlayerInputReader>();
            playerHealth = player.GetComponent<Health>();
            if (playerHealth != null)
                playerHealth.Died += Close;
            if (playerInput != null)
            {
                inputWasEnabled = playerInput.enabled;
                playerInput.enabled = false;
            }
        }

        selectedDestination = 0;
        Refresh();
        Menu.SetActive(true);
        if (EventSystem.current != null)
        {
            var module = EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (module != null && module.cancel != null)
            {
                cancelAction = module.cancel.action;
                if (cancelAction != null)
                    cancelAction.performed += OnCancel;
            }
            if (InfraredButton != null)
                EventSystem.current.SetSelectedGameObject(InfraredButton.gameObject);
        }
    }

    public void Close()
    {
        if (cancelAction != null)
            cancelAction.performed -= OnCancel;
        cancelAction = null;
        if (playerHealth != null)
            playerHealth.Died -= Close;
        playerHealth = null;
        if (Menu != null)
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(Menu.transform))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            Menu.SetActive(false);
        }

        if (playerInput != null)
            playerInput.enabled = inputWasEnabled;
        playerInput = null;
        currentPlayer = null;
        inputWasEnabled = false;
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        Close();
    }

    private void Update()
    {
        if (Menu == null || !Menu.activeSelf)
            return;
        if (currentPlayer == null)
            return;
        if (!currentPlayer.gameObject.activeInHierarchy)
        {
            Close();
            return;
        }
        if (!returnToHubOnly && currentPlayer.TryGetComponent(out TimeShiftController shift) && shift.CurrentFlag != TimeFlag.Present)
            Close();
    }

    public void SelectInfrared()
    {
        SelectDestination(0);
    }

    public void SelectUltraviolet()
    {
        SelectDestination(1);
    }

    public void SelectSewer()
    {
        if (!returnToHubOnly && StageProgress.SewerUnlocked)
            SelectDestination(2);
    }

    private void SelectDestination(int index)
    {
        selectedDestination = index;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < selectionHighlights.Length; i++)
        {
            if (selectionHighlights[i] != null)
                selectionHighlights[i].SetActive(i == selectedDestination);
        }

        if (TravelButton != null)
        {
            string scene = InfraredScene;
            if (returnToHubOnly)
                scene = hubScene;
            else if (selectedDestination == 1)
                scene = UltravioletScene;
            else if (selectedDestination == 2)
                scene = SewerScene;
            TravelButton.interactable = !string.IsNullOrWhiteSpace(scene)
                && Application.CanStreamedLevelBeLoaded(scene);
        }
    }

    /// <summary>
    /// 이동 버튼을 눌렀을 때 현재 선택한 목적지로 이동한다.
    /// </summary>
    public void TravelSelected()
    {
        if (Menu == null || !Menu.activeInHierarchy)
            return;
        if (selectedDestination == 0)
            GoInfrared();
        else if (selectedDestination == 1)
            GoUltraviolet();
        else if (selectedDestination == 2)
            GoSewer();
    }

    public void GoInfrared()
    {
        if (returnToHubOnly)
            Travel(hubScene);
        else
            Travel(InfraredScene);
    }

    public void GoUltraviolet()
    {
        if (!returnToHubOnly)
            Travel(UltravioletScene);
    }

    public void GoSewer()
    {
        if (!returnToHubOnly && StageProgress.SewerUnlocked)
            Travel(SewerScene);
    }

    private void Travel(string scene)
    {
        if (string.IsNullOrWhiteSpace(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            var director = FindFirstObjectByType<SystemDirector>();
            if (director != null)
                director.ShowMessage("이동할 씬을 설정해 주세요.");
            Debug.LogWarning("엘리베이터 목적지 씬을 확인해 주세요: " + scene, this);
            return;
        }

        Close();
        HitStopController.Cancel();
        Time.timeScale = 1;
        SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
    }
}
