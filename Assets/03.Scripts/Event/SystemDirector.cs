/// <summary>
/// 스크립트 최종 수정일: 2026-10-13
/// 기능: 마지막으로 밟은 체크포인트를 관리하고 사망한 플레이어를 복귀시킨다.
/// </summary>

using UnityEngine;

public class SystemDirector : MonoBehaviour
{
    public bool isDie; // 사망 처리 테스트
    public Checkpoint CurrentCheckpoint => currentCheckpoint; // 마지막으로 밟은 체크포인트

    private GameObject playerObj;
    private Checkpoint currentCheckpoint;
    private Health playerHealth;
    private Rigidbody2D playerBody; // 플레이어의 물리 이동 객체
    private PlayerMotor playerMotor; // 부활 시 이동 상태를 초기화할 컴포넌트
    private Vector3 startPosition; // 체크포인트가 없을 때 돌아갈 시작 위치
    private bool isRespawning; // 부활 처리 중 중복 사망 방지

    /// <summary>
    /// 플레이어가 마지막으로 들어간 체크포인트를 저장한다.
    /// </summary>
    public void SetCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null)
            return;

        currentCheckpoint = checkpoint;
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
        }
    }

    /// <summary>
    /// 플레이어의 사망 이벤트에 사망 처리를 연결한다.
    /// </summary>
    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.Died += HelthCheck;
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
    }

    /// <summary>
    /// 실제 사망 또는 테스트 요청을 확인하고 플레이어를 체크포인트로 복귀시킨다.
    /// </summary>
    public void HelthCheck()
    {
        if (playerObj == null || playerHealth == null) return;
        if (isRespawning) return;

        if (!isDie && !playerHealth.IsDead) return;

        isDie = false;

        if (!playerObj.activeSelf) return;

        isRespawning = true;
        playerObj.SetActive(false);
        Respawn();
        isRespawning = false;
    }

    /// <summary>
    /// 저장된 위치로 이동하고 이동 상태와 체력을 회복한 뒤 플레이어를 활성화한다.
    /// </summary>
    private void Respawn()
    {
        Vector3 respawnPosition = startPosition;

        if (currentCheckpoint != null)
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
            playerObj = GameObject.Find("Player");
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
    }
}
