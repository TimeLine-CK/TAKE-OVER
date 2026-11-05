/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 두 시대의 적 전멸 상태를 기록하고 완료한 스테이지의 적을 다시 생성하지 않는다.
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class StageCombatProgress : MonoBehaviour
{
    private readonly List<Health> enemies = new();

    private IEnumerator Start()
    {
        TimeShiftController timeShift = FindFirstObjectByType<TimeShiftController>();
        while (timeShift != null && !timeShift.IsReady)
            yield return null;

        var pastScene = gameObject.scene;
        if (timeShift != null && timeShift.GetWorld(TimeFlag.Past) != null)
            pastScene = timeShift.GetWorld(TimeFlag.Past).gameObject.scene;

        foreach (Health health in FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (health.gameObject.scene != gameObject.scene && health.gameObject.scene != pastScene)
                continue;
            if (health.GetComponent<RobotEnemy>() != null || health.GetComponent<TurretController>() != null)
                enemies.Add(health);
        }

        string key = "StageClear/" + gameObject.scene.name;
        if (StageProgress.Switches.TryGetValue(key, out bool cleared) && cleared)
        {
            foreach (Health enemy in enemies)
            {
                enemy.RestoreState(0);
                enemy.gameObject.SetActive(false);
            }
            yield break;
        }

        foreach (Health enemy in enemies)
            enemy.Died += UpdateClearState;
        UpdateClearState();
    }

    private void UpdateClearState()
    {
        foreach (Health enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
                return;
        }
        StageProgress.Switches["StageClear/" + gameObject.scene.name] = true;
    }

    private void OnDestroy()
    {
        foreach (Health enemy in enemies)
        {
            if (enemy != null)
                enemy.Died -= UpdateClearState;
        }
    }
}
