/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 시대별 또는 전체 적 전멸 조건을 확인하고 다음 씬으로 이동한다.
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public sealed class StageExit : MonoBehaviour
{
    [SerializeField] private string nextSceneName; // 다음 현재 씬, 비어 있으면 튜토리얼 완료
    [SerializeField] private bool requiresClear;   // 적 전멸 조건
    [SerializeField] private bool requiresAllEras; // 양쪽 시대의 적을 모두 검사
    [SerializeField] private SpriteRenderer indicator; // 출구 열림 상태 표시
    private TimeShiftController timeShift;
    private SystemDirector director;
    private Health[] presentEnemies;
    private Health[] pastEnemies;
    private Health[] sharedEnemies;
    private bool isReady;
    private bool isLeaving;
    private float nextHintTime;

    private IEnumerator Start()
    {
        timeShift = FindFirstObjectByType<TimeShiftController>();
        director = FindFirstObjectByType<SystemDirector>();
        while (timeShift != null && !timeShift.IsReady)
            yield return null;
        if (timeShift != null)
        {
            presentEnemies = timeShift.GetWorld(TimeFlag.Present).GetComponentsInChildren<Health>(true);
            pastEnemies = timeShift.GetWorld(TimeFlag.Past).GetComponentsInChildren<Health>(true);
        }
        List<Health> shared = new List<Health>();
        foreach (Health enemy in FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (enemy.GetComponentInParent<TimeFlagWolrdRoot>(true) != null)
                continue;
            if (enemy.GetComponent<RobotEnemy>() != null || enemy.GetComponent<TurretController>() != null)
                shared.Add(enemy);
        }
        sharedEnemies = shared.ToArray();
        isReady = true;
    }

    /// <summary>
    /// 설정된 시대 범위와 공용 영역에 생존한 적이 있는지 확인한다.
    /// </summary>
    public bool CanExit()
    {
        if (!isReady || (director != null && director.IsStopped))
            return false;
        if (!requiresClear)
            return true;
        if (requiresAllEras)
        {
            if (HasLivingEnemy(presentEnemies) || HasLivingEnemy(pastEnemies))
                return false;
        }
        else
        {
            Health[] enemies = presentEnemies;
            if (timeShift != null && timeShift.CurrentFlag == TimeFlag.Past)
                enemies = pastEnemies;
            if (HasLivingEnemy(enemies))
                return false;
        }
        return !HasLivingEnemy(sharedEnemies);
    }

    private static bool HasLivingEnemy(Health[] enemies)
    {
        if (enemies == null)
            return true;
        foreach (Health enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
                return true;
        }
        return false;
    }

    private void Update()
    {
        if (indicator == null)
            return;
        if (CanExit())
            indicator.color = new Color(0.15f, 1f, 0.5f, 0.8f);
        else
            indicator.color = new Color(1f, 0.25f, 0.2f, 0.8f);
    }

    /// <summary>
    /// 출구 안에 있는 플레이어가 전멸 조건을 만족하면 다음 구간으로 이동한다.
    /// </summary>
    private void OnTriggerStay2D(Collider2D other)
    {
        GameObject player = other.gameObject;
        if (other.attachedRigidbody != null)
            player = other.attachedRigidbody.gameObject;
        if (!player.CompareTag("Player") || isLeaving || !isReady)
            return;
        Health health = player.GetComponent<Health>();
        if (health == null || health.IsDead)
            return;
        if (!CanExit())
        {
            if (Time.unscaledTime >= nextHintTime)
            {
                if (requiresAllEras)
                    director?.ShowMessage("모든 적을 처치해야 이동할 수 있습니다.");
                else
                    director?.ShowMessage("Defeat the enemies in this era");
                nextHintTime = Time.unscaledTime + 2f;
            }
            return;
        }
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            isLeaving = true;
            director?.CompleteTutorial();
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            director?.ShowMessage("Next stage is not in Build Settings");
            return;
        }
        isLeaving = true;
        if (requiresClear)
            StageProgress.Switches["StageClear/" + gameObject.scene.name] = true;
        HitStopController.Cancel();
        Time.timeScale = 1f;
        player.SetActive(false);
        SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
    }
}
