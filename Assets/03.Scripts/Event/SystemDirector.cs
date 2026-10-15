/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 체크포인트 상태 저장과 사망 화면 및 재시작 복원을 관리한다.
/// </summary>

using UnityEngine;
using UnityEngine.SceneManagement;

public class SystemDirector : MonoBehaviour
{
    public bool isDie; // 사망 처리 테스트
    public Checkpoint CurrentCheckpoint => currentCheckpoint; // 마지막으로 밟은 체크포인트
    public bool IsStopped => isStopped;
    [SerializeField] private GameObject deathPanel;      // 사망 후 재시작 화면
    [SerializeField] private GameObject clearPanel;      // 튜토리얼 완료 화면
    [SerializeField] private TMPro.TMP_Text messageLabel; // 저장과 출구 안내

    private GameObject playerObj;
    private Checkpoint currentCheckpoint;
    private Health playerHealth;
    private Rigidbody2D playerBody; // 플레이어의 물리 이동 객체
    private PlayerMotor playerMotor; // 부활 시 이동 상태를 초기화할 컴포넌트
    private Vector3 startPosition; // 체크포인트가 없을 때 돌아갈 시작 위치
    private bool isRespawning; // 부활 처리 중 중복 사망 방지
    private bool isStopped;
    private float previousTimeScale = 1f;
    private float messageUntil;
    private TimeShiftController timeShift;
    private CheckpointState savedState;

    /// <summary>
    /// 플레이어가 마지막으로 들어간 체크포인트를 저장한다.
    /// </summary>
    public void SetCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null || isStopped || isRespawning)
            return;

        currentCheckpoint = checkpoint;
        if (timeShift != null && !timeShift.IsReady)
            return;
        SaveState();
        ShowMessage("CHECKPOINT SAVED");
    }

    /// <summary>
    /// 플레이어와 체력 컴포넌트를 찾는다.
    /// </summary>
    private void Awake()
    {
        FindHelth();

        if (playerObj != null)
        {
            startPosition = playerObj.transform.position;
            playerBody = playerObj.GetComponent<Rigidbody2D>();
            playerMotor = playerObj.GetComponent<PlayerMotor>();
            timeShift = playerObj.GetComponent<TimeShiftController>();
        }
    }

    private void SaveInitialState()
    {
        if (savedState == null)
            SaveState();
    }

    private void SaveState()
    {
        if (playerHealth == null)
            return;
        Vector3 position = startPosition;
        if (currentCheckpoint != null)
            position = currentCheckpoint.Position;
        savedState = new CheckpointState(timeShift, playerHealth, position);
    }

    /// <summary>
    /// 플레이어의 사망 이벤트에 사망 처리를 연결한다.
    /// </summary>
    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.Died += HelthCheck;
        if (timeShift != null)
        {
            timeShift.WorldsReady += SaveInitialState;
            if (timeShift.IsReady)
                SaveInitialState();
        }
        else
            SaveInitialState();
    }

    /// <summary>
    /// 테스트 값이 켜졌을 때 사망 처리를 실행한다.
    /// </summary>
    private void Update()
    {
        if (isDie)
        {
            HelthCheck();
        }
        if (messageLabel != null && Time.unscaledTime >= messageUntil)
            messageLabel.gameObject.SetActive(false);
    }

    /// <summary>
    /// 실제 사망 또는 테스트 요청을 확인하고 플레이어를 체크포인트로 복귀시킨다.
    /// </summary>
    public void HelthCheck()
    {
        if (playerObj == null || playerHealth == null)
            return;
        if (isRespawning || isStopped)
            return;

        if (!isDie && !playerHealth.IsDead)
            return;

        isDie = false;

        if (!playerObj.activeSelf)
            return;

        playerObj.SetActive(false);
        isStopped = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (deathPanel != null)
            deathPanel.SetActive(true);
        else
            Restart();
    }

    /// <summary>
    /// 재시작 버튼을 누르면 저장된 스테이지와 시대를 복원한다.
    /// </summary>
    public void Restart()
    {
        if (!isStopped || playerObj == null || isRespawning)
            return;
        isRespawning = true;
        if (deathPanel != null)
            deathPanel.SetActive(false);
        Time.timeScale = previousTimeScale;
        savedState?.Restore();
        if (timeShift != null && savedState != null)
            timeShift.RestoreWorld(savedState.Era);
        Respawn();
        isStopped = false;
        isRespawning = false;
    }

    /// <summary>
    /// 저장된 위치로 이동하고 이동 상태와 체력을 회복한 뒤 플레이어를 활성화한다.
    /// </summary>
    private void Respawn()
    {
        Vector3 respawnPosition = startPosition;
        if (savedState != null)
            respawnPosition = savedState.SpawnPosition;
        else if (currentCheckpoint != null)
        {
            Vector2 checkpointPosition = currentCheckpoint.Position;
            respawnPosition.x = checkpointPosition.x;
            respawnPosition.y = checkpointPosition.y;
        }

        playerObj.transform.position = respawnPosition;

        if (playerBody != null)
        {
            playerBody.position = respawnPosition;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }

        if (playerMotor != null)
            playerMotor.ResetMovement();

        playerHealth.RestoreHealth();
        playerObj.SetActive(true);
        Physics2D.SyncTransforms();
    }

    /// <summary>
    /// 플레이어 객체에서 사망 이벤트를 제공하는 체력 컴포넌트를 가져온다.
    /// </summary>
    private void FindHelth()
    {
        if (playerObj == null)
        {
            playerObj = GameObject.FindGameObjectWithTag("Player");
        }

        if (playerObj != null)
        {
            playerHealth = playerObj.GetComponent<Health>();
        }
    }

    /// <summary>
    /// 비활성화될 때 사망 이벤트 연결을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.Died -= HelthCheck;
        if (timeShift != null)
            timeShift.WorldsReady -= SaveInitialState;
        if (isStopped)
            Time.timeScale = previousTimeScale;
    }

    /// <summary>
    /// 체크포인트와 출구 안내를 짧은 시간 표시한다.
    /// </summary>
    public void ShowMessage(string message)
    {
        if (messageLabel == null)
            return;
        messageLabel.text = message;
        messageUntil = Time.unscaledTime + 2f;
        messageLabel.gameObject.SetActive(true);
    }

    /// <summary>
    /// 마지막 구간의 출구에서 완료 화면을 표시한다.
    /// </summary>
    public void CompleteTutorial()
    {
        if (isStopped || playerObj == null)
            return;
        playerObj.SetActive(false);
        isStopped = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (clearPanel != null)
            clearPanel.SetActive(true);
    }

    public void RestartTutorial()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("UR-1-A");
    }
}
