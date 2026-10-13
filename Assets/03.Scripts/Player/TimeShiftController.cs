/// <summary>
/// 스크립트 최종 수정일: 2026-10-13
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

    private TimeFlag currentFlag = TimeFlag.Present;                        // 현재 활성화된 시점
    private TimeFlagWolrdRoot pastWorld;                                    // 과거 시점의 월드 루트
    private bool isReady;                                                   // 과거 씬 준비 완료 여부
    private bool isPreparing;                                               // 시점 전환 준비 중 여부

    /// <summary>
    /// 필수 참조를 확인하고 과거 씬을 Additive 방식으로 준비한다.
    /// </summary>
    private IEnumerator Start()
    {
        if (!ValidateReferences())
            yield break;

        yield return LoadPastScene();
    }

    /// <summary>
    /// 시간 전환 입력을 소비하고 중복 실행을 막는다.
    /// </summary>
    private void Update()
    {
        if (!inputReader.ConsumeTimeShiftPressed())
            return;

        if (!isReady || isPreparing)
            return;

        StartCoroutine(PrepareTimeShift());
    }

    /// <summary>
    /// 지정된 준비 시간이 지난 뒤 반대 시점으로 전환한다.
    /// </summary>
    private IEnumerator PrepareTimeShift()
    {
        isPreparing = true;

        if (shiftVfxPlayer != null)
        {
            yield return shiftVfxPlayer.Play(TryShiftWorld);
        }
        else
        {
            if (preparationDuration > 0f)
                yield return new WaitForSeconds(preparationDuration);

            TryShiftWorld();
        }

        isPreparing = false;
    }

    /// <summary>
    /// 비활성화되면 진행 중인 전환과 화면 효과를 취소하고 다음 전환을 허용한다.
    /// </summary>
    private void OnDisable()
    {
        StopAllCoroutines();
        isPreparing = false;

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
        isReady = true;
    }

    /// <summary>
    /// 목적 시점의 월드를 활성화하고 플레이어 위치에 장애물이 있으면 전환을 취소한다.
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

        currentWorld.SetWorldActive(false);
        destinationWorld.SetWorldActive(true);
        Physics2D.SyncTransforms();

        if (IsDestinationBlocked())
        {
            destinationWorld.SetWorldActive(false);
            currentWorld.SetWorldActive(true);
            Physics2D.SyncTransforms();
            return;
        }

        currentFlag = destinationFlag;
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

        return Physics2D.OverlapBox(bounds.center, checkSize, 0f, destinationObstacleMask) != null;
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
        if (inputReader == null || playerRd == null || playerCollider == null || presentWorld == null)
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
