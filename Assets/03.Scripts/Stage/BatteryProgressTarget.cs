/// <summary>
/// 스크립트 최종 수정일: 2026-11-02
/// 기능: 필수 배터리 완충 상태를 읽어 연결된 다른 씬의 장치를 활성화한다.
/// </summary>
using UnityEngine;
public sealed class BatteryProgressTarget : MonoBehaviour
{
    public string[] RequiredBatteryIds;
    public GameObject[] Targets;
    private void OnEnable()
    {
        StageProgress.Changed += Refresh;
        Refresh();
    }
    private void OnDisable()
    {
        StageProgress.Changed -= Refresh;
    }
    private void Refresh()
    {
        bool ready = StageProgress.AllCharged(RequiredBatteryIds);
        if (Targets == null)
            return;
        foreach (var target in Targets)
            if (target != null && target != gameObject)
                target.SetActive(ready);
    }
}
