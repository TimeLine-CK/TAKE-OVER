/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 플레이어가 있는 시대의 적 생사를 검사하고 다음 튜토리얼로 이동한다.
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public sealed class StageExit : MonoBehaviour
{
    [SerializeField] private string nextSceneName; // 다음 현재 씬, 비어 있으면 튜토리얼 완료
    [SerializeField] private bool requiresClear;   // 현재 시대의 적 전멸 조건
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
        while (timeShift != null && !timeShift.IsReady) yield return null;
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
    /// 반대 시대의 적은 제외하고 현재 시대의 생존 적만 검사한다.
    /// </summary>
    public bool CanExit()
    {
        if (!isReady || (director != null && director.IsStopped))
            return false;
        if (!requiresClear)
            return true;
        Health[] enemies = presentEnemies;
        if (timeShift != null && timeShift.CurrentFlag == TimeFlag.Past)
            enemies = pastEnemies;
        if (enemies == null)
            return false;
        foreach (Health enemy in enemies)
            if (enemy != null && !enemy.IsDead)
                return false;
        foreach (Health enemy in sharedEnemies)
            if (enemy != null && !enemy.IsDead)
                return false;
        return true;
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
        if (!other.CompareTag("Player") || isLeaving || !isReady)
            return;
        if (!CanExit())
        {
            if (Time.unscaledTime >= nextHintTime)
            {
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
        other.gameObject.SetActive(false);
        SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
    }
}
