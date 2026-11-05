/// <summary>
/// 스크립트 최종 수정일: 2026-11-02
/// 기능: 플레이 중 씬을 이동해도 배터리와 장치 진행 상태를 유지한다.
/// </summary>
using System;
using System.Collections.Generic;
using UnityEngine;
public static class StageProgress
{
    public static event Action Changed;
    public static readonly Dictionary<string, float> Charges = new();
    public static readonly Dictionary<string, bool> Switches = new();
    public static readonly Dictionary<string, int> Codes = new();
    public static readonly Dictionary<string, Pose> Poses = new();
    public static readonly Dictionary<string, float> Angles = new();
    public static bool SewerUnlocked
    {
        get; private set;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Charges.Clear();
        Switches.Clear();
        Codes.Clear();
        Poses.Clear();
        Angles.Clear();
        SewerUnlocked = false;
        Changed = null;
    }
    public static void SetCharge(string id, float value)
    {
        Charges[id] = Mathf.Clamp(value, 0, 100);
        Changed?.Invoke();
    }
    public static bool AllCharged(string[] ids)
    {
        if (ids == null || ids.Length == 0)
            return false;
        foreach (string id in ids)
            if (string.IsNullOrWhiteSpace(id) || !Charges.TryGetValue(id, out float value) || value < 100)
                return false;
        return true;
    }
    public static void UnlockSewer()
    {
        if (SewerUnlocked)
            return;
        SewerUnlocked = true;
        Changed?.Invoke();
    }
}
