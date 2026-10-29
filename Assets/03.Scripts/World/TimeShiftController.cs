/// <summary>
/// 스크립트 최종 수정일: 2026-10-27
/// 기능: 현재와 과거 월드를 준비하고 입력에 따라 플레이어의 시점을 전환한다.
/// </summary>

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TimeShiftController : MonoBehaviour
{
    [SerializeField] private string pastSceneName;                         // Additive로 불러올 과거 씬 이름
    [SerializeField] private float preparationDuration = 1.5f;             // 입력 후 실제 전환까지 걸리는 시간
    [SerializeField] private PlayerInputReader inputReader;                // 시간 전환 입력을 전달하는 컴포넌트
    [SerializeField] private Rigidbody2D playerRd;                          // 동일한 좌표를 유지할 플레이어 물리 객체
    [SerializeField] private Collider2D playerCollider;                     // 전환 위치의 충돌 여부를 검사할 콜라이더
    [SerializeField] private TimeFlagWolrdRoot presentWorld;                // 현재 시점의 월드 루트
    [SerializeField] private LayerMask destinationObstacleMask;             // 시점 전환을 막는 지형 레이어
    [SerializeField] private TimeShiftVfxPlayer shiftVfxPlayer;              // 화면 전환 영상을 재생하는 컴포넌트
    [SerializeField] private Material previewMaterial;                     // 다른 시대 지형의 점선 머티리얼
    [SerializeField] private float previewLineWidth = 0.04f;                // 점선 두께
    [SerializeField] private float previewDashLength = 0.18f;               // 점선 한 조각의 길이
    [SerializeField] private float previewGapLength = 0.12f;                // 점선 조각 사이의 간격

    public bool IsPreparing => isPreparing;                               // 공격과 상호작용을 제한할 준비 상태
    public TimeFlag CurrentFlag => currentFlag;                           // 상호작용에 적용할 현재 시대
    public bool IsReady => isReady;                                       // 시대별 월드 준비 완료 여부
    public event System.Action WorldsReady;                               // 두 시대의 준비가 끝났을 때 호출하는 이벤트

    private TimeFlag currentFlag = TimeFlag.Present;                        // 현재 활성화된 시점
    private TimeFlagWolrdRoot pastWorld;                                    // 과거 시점의 월드 루트
    private bool isReady;                                                   // 과거 씬 준비 완료 여부
    private bool isPreparing;                                               // 시점 전환 준비 중 여부
    private bool isTransitioning;                                          // 준비 또는 전환 연출 진행 여부
    private Coroutine transitionRoutine;                                  // 적 피격으로 취소할 전환 코루틴
    private TimeWorldPreview presentPreview;                               // 현재 지형의 외곽 미리보기
    private TimeWorldPreview pastPreview;                                  // 과거 지형의 외곽 미리보기
    private Health playerHealth;                                          // 끼임 사망을 처리할 체력 컴포넌트
    private readonly Collider2D[] obstacleHits = new Collider2D[16];        // 목적 시대의 장애물 검사 결과

    public TimeFlagWolrdRoot GetWorld(TimeFlag flag)
    {
        if (flag == TimeFlag.Present)
            return presentWorld;
        return pastWorld;
    }

    /// <summary>
    /// 저장된 시대를 연출과 끼임 판정 없이 복원한다.
    /// </summary>
    public void RestoreWorld(TimeFlag flag)
    {
        if (!isReady)
            return;
        StopAllCoroutines();
        shiftVfxPlayer?.StopPlayback();
        transitionRoutine = null;
        isPreparing = false;
        isTransitioning = false;
        currentFlag = flag;
        presentWorld.SetWorldActive(flag == TimeFlag.Present);
        pastWorld.SetWorldActive(flag == TimeFlag.Past);
        UpdatePreviews();
        Physics2D.SyncTransforms();
    }

    private void Awake()
    {
        playerHealth = GetComponent<Health>();
    }

    /// <summary>
    /// 부활하면 현재 시대에 맞는 점선 표시를 복원한다.
    /// </summary>
    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.Damaged += OnDamaged;

        if (isReady)
            UpdatePreviews();
    }

    private void LateUpdate()
    {
        presentPreview?.Refresh();
        pastPreview?.Refresh();
    }

    /// <summary>
    /// 연결된 컴포넌트를 확인하고 과거 씬을 추가로 불러온다.
    /// </summary>
    private IEnumerator Start()
    {
        if (!ValidateReferences())
            yield break;

        yield return LoadPastScene();
    }

    private void Update()
    {
        if (!inputReader.ConsumeTimeShiftPressed())
            return;

        TryStartTimeShift();
    }

    /// <summary>
    /// 월드 준비와 현재 진행 상태를 확인한 뒤 시간 전환 준비를 시작한다.
    /// </summary>
    public bool TryStartTimeShift()
    {
        if (!isActiveAndEnabled || !isReady || isTransitioning || playerHealth.IsDead)
            return false;

        HitStopController.Cancel();
        GetComponent<PlayerCombat>()?.ClearEffects();
        transitionRoutine = StartCoroutine(PrepareTimeShift());
        return true;
    }

    /// <summary>
    /// 적에게 피해를 받으면 준비를 취소하고 기존 시대를 유지한다.
    /// </summary>
    private void OnDamaged(DamageSource source)
    {
        if (source != DamageSource.Enemy || !isPreparing)
            return;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = null;
        isPreparing = false;
        isTransitioning = false;
        shiftVfxPlayer?.StopPlayback();
    }

    /// <summary>
    /// 지정된 준비 시간이 지난 뒤 반대 시점으로 전환한다.
    /// </summary>
    private IEnumerator PrepareTimeShift()
    {
        isPreparing = true;
        isTransitioning = true;

        if (preparationDuration > 0f)
            yield return new WaitForSeconds(preparationDuration);

        if (shiftVfxPlayer != null)
        {
            yield return shiftVfxPlayer.Play(TryShiftWorld);
        }
        else
        {
            TryShiftWorld();
        }

        isPreparing = false;
        isTransitioning = false;
        transitionRoutine = null;
    }

    /// <summary>
    /// 비활성화되면 진행 중인 전환과 화면 효과를 취소하고 다음 전환을 허용한다.
    /// </summary>
    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.Damaged -= OnDamaged;

        StopAllCoroutines();
        transitionRoutine = null;
        isPreparing = false;
        isTransitioning = false;
        presentPreview?.SetVisible(false);
        pastPreview?.SetVisible(false);

        if (shiftVfxPlayer != null)
            shiftVfxPlayer.StopPlayback();
    }

    /// <summary>
    /// 과거 씬을 불러오고 시점 루트를 찾아 전환 가능한 상태로 만든다.
    /// </summary>
    private IEnumerator LoadPastScene()
    {
        Scene loadedScene = SceneManager.GetSceneByName(pastSceneName);

        if (!loadedScene.isLoaded)
        {
            if (!Application.CanStreamedLevelBeLoaded(pastSceneName))
            {
                Debug.LogError($"Build Settings에서 과거 씬을 찾을 수 없습니다: {pastSceneName}", this);
                yield break;
            }

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(pastSceneName, LoadSceneMode.Additive);
            yield return loadOperation;
            loadedScene = SceneManager.GetSceneByName(pastSceneName);
        }

        pastWorld = FindWorldRoot(loadedScene, TimeFlag.Past);

        if (pastWorld == null)
        {
            Debug.LogError($"{pastSceneName} 씬에서 Past로 설정된 TimeFlagWolrdRoot를 찾을 수 없습니다.", this);
            yield break;
        }

        presentWorld.SetWorldActive(true);
        pastWorld.SetWorldActive(false);
        currentFlag = TimeFlag.Present;
        if (previewMaterial != null)
        {
            presentPreview = new TimeWorldPreview(presentWorld.transform, destinationObstacleMask, previewMaterial, previewLineWidth, previewDashLength, previewGapLength);
            pastPreview = new TimeWorldPreview(pastWorld.transform, destinationObstacleMask, previewMaterial, previewLineWidth, previewDashLength, previewGapLength);
        }
        else
        {
            Debug.LogWarning("TimeShiftController에 점선 미리보기 머티리얼이 설정되지 않았습니다.", this);
        }
        isReady = true;
        UpdatePreviews();
        WorldsReady?.Invoke();
    }

    /// <summary>
    /// 완료 직전 위치에서 목적 시점을 적용하고 몸체가 장애물에 끼면 사망 처리한다.
    /// </summary>
    private void TryShiftWorld()
    {
        TimeFlag destinationFlag;
        TimeFlagWolrdRoot currentWorld;
        TimeFlagWolrdRoot destinationWorld;

        if (currentFlag == TimeFlag.Present)
        {
            destinationFlag = TimeFlag.Past;
            currentWorld = presentWorld;
            destinationWorld = pastWorld;
        }
        else
        {
            destinationFlag = TimeFlag.Present;
            currentWorld = pastWorld;
            destinationWorld = presentWorld;
        }

        HitStopController.Cancel();
        GetComponent<PlayerCombat>()?.ClearEffects();
        currentWorld.SetWorldActive(false);
        destinationWorld.SetWorldActive(true);
        currentFlag = destinationFlag;
        isPreparing = false;
        UpdatePreviews();
        Physics2D.SyncTransforms();

        if (IsDestinationBlocked())
        {
            playerHealth.TakeDamage(int.MaxValue, DamageSource.Environment);
            return;
        }

        playerRd.WakeUp();
    }

    /// <summary>
    /// 플레이어 콜라이더보다 조금 작은 영역으로 목적지의 장애물을 검사한다.
    /// </summary>
    private bool IsDestinationBlocked()
    {
        Bounds bounds = playerCollider.bounds;
        Vector2 checkSize = new(
            Mathf.Max(0.01f, bounds.size.x - 0.02f),
            Mathf.Max(0.01f, bounds.size.y - 0.02f)
        );

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(destinationObstacleMask);
        filter.useTriggers = false;
        int count = Physics2D.OverlapBox(bounds.center, checkSize, 0f, filter, obstacleHits);
        for (int index = 0; index < count; index++)
        {
            Collider2D obstacle = obstacleHits[index];
            if (obstacle == playerCollider || obstacle.transform.IsChildOf(transform))
                continue;
            if (playerCollider.Distance(obstacle).distance < -0.02f)
                return true;
        }
        return false;
    }

    private void UpdatePreviews()
    {
        presentPreview?.SetVisible(currentFlag == TimeFlag.Past);
        pastPreview?.SetVisible(currentFlag == TimeFlag.Present);
    }

    private void OnDestroy()
    {
        presentPreview?.Dispose();
        pastPreview?.Dispose();
    }

    /// <summary>
    /// 불러온 씬에서 지정한 시점의 월드 루트를 찾는다.
    /// </summary>
    private static TimeFlagWolrdRoot FindWorldRoot(Scene scene, TimeFlag targetFlag)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            TimeFlagWolrdRoot[] worldRoots = rootObject.GetComponentsInChildren<TimeFlagWolrdRoot>(true);

            foreach (TimeFlagWolrdRoot worldRoot in worldRoots)
            {
                if (worldRoot.TimeFlag == targetFlag)
                    return worldRoot;
            }
        }

        return null;
    }

    /// <summary>
    /// 실행에 필요한 참조와 플레이어 배치 상태를 확인한다.
    /// </summary>
    private bool ValidateReferences()
    {
        if (destinationObstacleMask.value == 0)
        {
            Debug.LogError("TimeShiftController의 지형 레이어가 설정되지 않았습니다.", this);
            return false;
        }
        if (inputReader == null || playerRd == null || playerCollider == null || presentWorld == null || playerHealth == null)
        {
            Debug.LogError("TimeShiftController의 필수 참조가 설정되지 않았습니다.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(pastSceneName))
        {
            Debug.LogError("과거 씬 이름이 설정되지 않았습니다.", this);
            return false;
        }

        if (transform.IsChildOf(presentWorld.transform))
        {
            Debug.LogError("플레이어와 TimeShiftController는 시점 월드 루트의 자식이 될 수 없습니다.", this);
            return false;
        }

        return true;
    }
}
