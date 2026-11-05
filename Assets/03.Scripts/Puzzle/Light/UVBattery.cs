/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 배터리에 필요한 코드를 표시하고 과거에서 정답 차단기 조합이 켜지면 충전한다.
/// </summary>

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(200)]
public sealed class UVBattery : BatteryCharge
{
    public UltravioletCode Code;
    public CrossingGate[] Gates;
    public string PuzzleId; // 비워 두면 Gates를 사용하고 입력하면 같은 ID의 차단기를 자동 연결
    [Range(1, 5)] public int RequiredCodeCount = 1; // 충전에 필요한 서로 다른 코드 수
    [Min(0f)] public float CodeSpacing = 0.15f;     // 코드 이미지 사이의 간격

    public IReadOnlyList<int> RequiredCodes => requiredCodes;

    private TimeShiftController timeShift;
    private List<int> requiredCodes = new();
    private List<SpriteRenderer> codeIcons = new();
    private Vector3 codeOrigin;
    private int requiredCodeMask;
    private int configuredCodeCount;
    private float displayedSpacing;
    private bool started;
    private string configuredPuzzleId;

    protected override void Awake()
    {
        base.Awake();
        timeShift = FindFirstObjectByType<TimeShiftController>();
    }

    private void OnEnable()
    {
        if (timeShift != null)
            timeShift.WorldsReady += RefreshGates;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        if (started)
            RefreshGates();
    }

    private void OnDisable()
    {
        if (timeShift != null)
            timeShift.WorldsReady -= RefreshGates;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    protected override void Start()
    {
        started = true;
        RefreshGates();
        base.Start();
    }

    protected override void LateUpdate()
    {
        if (configuredPuzzleId != PuzzleId)
            RefreshGates();
        else if (configuredCodeCount != RequiredCodeCount)
            ConfigureCodes();
        else if (!Mathf.Approximately(displayedSpacing, CodeSpacing))
            UpdateCodeIcons();
        base.LateUpdate();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (started)
            RefreshGates();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (started)
            RefreshGates();
    }

    /// <summary>
    /// 두 시대의 씬이 준비되면 같은 퍼즐 ID의 차단기를 비활성 상태까지 찾아 연결한다.
    /// </summary>
    private void RefreshGates()
    {
        configuredPuzzleId = PuzzleId;
        if (!string.IsNullOrWhiteSpace(PuzzleId))
        {
            var gates = new List<CrossingGate>();
            if (timeShift != null && timeShift.IsReady)
            {
                TimeFlagWolrdRoot pastWorld = timeShift.GetWorld(TimeFlag.Past);
                string id = PuzzleId.Trim();
                foreach (CrossingGate gate in FindObjectsByType<CrossingGate>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (gate.gameObject.scene != gameObject.scene
                        && (pastWorld == null || gate.gameObject.scene != pastWorld.gameObject.scene))
                        continue;
                    if (!string.IsNullOrWhiteSpace(gate.PuzzleId) && gate.PuzzleId.Trim() == id)
                        gates.Add(gate);
                }
                if (gates.Count == 0)
                    Debug.LogWarning("같은 Puzzle Id의 차단기를 찾을 수 없습니다: " + PuzzleId, this);
            }
            Gates = gates.ToArray();
        }

        if (Code != null)
            Code.Initialize();
        if (Gates != null)
            foreach (CrossingGate gate in Gates)
                if (gate != null && gate.Code != null)
                    gate.Code.Initialize();
        ConfigureCodes();
    }

    /// <summary>
    /// 연결된 차단기로 풀 수 있는 코드 조합을 정하고 저장된 정답을 복원한다.
    /// </summary>
    private void ConfigureCodes()
    {
        configuredCodeCount = RequiredCodeCount;
        requiredCodes.Clear();
        requiredCodeMask = 0;
        var gates = new List<CrossingGate>();
        if (Gates != null)
        {
            foreach (CrossingGate gate in Gates)
            {
                if (gate != null && gate.Code != null && !gates.Contains(gate))
                    gates.Add(gate);
            }
        }
        if (Code == null || gates.Count == 0)
        {
            UpdateCodeIcons();
            return;
        }

        var random = new System.Random(System.Guid.NewGuid().GetHashCode());
        var availableCodes = GetGateCodes(gates);
        if (!Code.RandomizeOnStart && !availableCodes.Contains(Code.CodeIndex))
        {
            CrossingGate gate = FindDuplicateGate(gates);
            if (gate == null)
                gate = gates[random.Next(gates.Count)];
            gate.Code.SetCode(Code.CodeIndex);
            availableCodes = GetGateCodes(gates);
        }

        int count = Mathf.Min(Mathf.Clamp(RequiredCodeCount, 1, 5), gates.Count);
        if (RequiredCodeCount > gates.Count)
            Debug.LogWarning("필요한 코드 수보다 연결된 차단기가 적어 코드 수를 " + count + "개로 제한합니다.", this);

        while (availableCodes.Count < Mathf.Min(gates.Count, 5))
        {
            CrossingGate gate = FindDuplicateGate(gates);
            if (gate == null)
                break;
            int index = random.Next(5);
            while (availableCodes.Contains(index))
                index = (index + 1) % 5;
            gate.Code.SetCode(index);
            availableCodes.Add(index);
        }

        for (int i = 0; i < count; i++)
        {
            string key = BatteryId + "/TargetCode";
            if (i > 0)
                key += "/" + i;

            int index;
            if (i == 0 && !Code.RandomizeOnStart)
            {
                index = Code.CodeIndex;
            }
            else if (!StageProgress.Codes.TryGetValue(key, out index) || !availableCodes.Contains(index))
            {
                index = availableCodes[random.Next(availableCodes.Count)];
            }

            requiredCodes.Add(index);
            requiredCodeMask |= 1 << index;
            availableCodes.Remove(index);
            StageProgress.Codes[key] = index;
        }

        Code.AlwaysVisible = true;
        Code.SetCode(requiredCodes[0]);
        UpdateCodeIcons();
    }

    private static List<int> GetGateCodes(List<CrossingGate> gates)
    {
        var codes = new List<int>();
        foreach (CrossingGate gate in gates)
            if (!codes.Contains(gate.Code.CodeIndex))
                codes.Add(gate.Code.CodeIndex);
        return codes;
    }

    private static CrossingGate FindDuplicateGate(List<CrossingGate> gates)
    {
        for (int i = 0; i < gates.Count; i++)
            for (int j = 0; j < i; j++)
                if (gates[i].Code.CodeIndex == gates[j].Code.CodeIndex)
                    return gates[i];
        return null;
    }

    /// <summary>
    /// 필요한 코드 이미지를 같은 간격으로 배터리 위에 나열한다.
    /// </summary>
    private void UpdateCodeIcons()
    {
        displayedSpacing = CodeSpacing;
        if (Code == null || Code.Icon == null)
            return;
        if (codeIcons.Count == 0)
        {
            codeOrigin = Code.Icon.transform.localPosition;
            codeIcons.Add(Code.Icon);
        }
        while (codeIcons.Count < requiredCodes.Count)
        {
            GameObject iconObject = new("RequiredCode" + (codeIcons.Count + 1));
            iconObject.transform.SetParent(Code.Icon.transform.parent, false);
            iconObject.transform.localRotation = Code.Icon.transform.localRotation;
            iconObject.transform.localScale = Code.Icon.transform.localScale;
            SpriteRenderer icon = iconObject.AddComponent<SpriteRenderer>();
            icon.sharedMaterial = Code.Icon.sharedMaterial;
            icon.color = Code.Icon.color;
            icon.sortingLayerID = Code.Icon.sortingLayerID;
            icon.sortingOrder = Code.Icon.sortingOrder;
            codeIcons.Add(icon);
        }

        float width = 0f;
        if (Code.CodeSprites != null)
        {
            foreach (Sprite sprite in Code.CodeSprites)
                if (sprite != null)
                    width = Mathf.Max(width, sprite.bounds.size.x * Mathf.Abs(Code.Icon.transform.localScale.x));
        }
        float spacing = width + Mathf.Max(0f, CodeSpacing);
        for (int i = 0; i < codeIcons.Count; i++)
        {
            SpriteRenderer icon = codeIcons[i];
            bool visible = i < requiredCodes.Count;
            icon.enabled = visible;
            if (!visible)
                continue;
            int index = requiredCodes[i];
            if (Code.CodeSprites != null && index < Code.CodeSprites.Length)
                icon.sprite = Code.CodeSprites[index];
            icon.transform.localPosition = codeOrigin
                + Vector3.right * (i - (requiredCodes.Count - 1) * 0.5f) * spacing;
        }
    }

    protected override bool CanCharge()
    {
        if (timeShift == null || !timeShift.IsReady || timeShift.CurrentFlag != TimeFlag.Past
            || Code == null || Gates == null || requiredCodeMask == 0)
            return false;

        int activeCodes = 0;
        foreach (CrossingGate gate in Gates)
        {
            if (gate == null || !gate.IsOn)
                continue;
            if (gate.Code == null || (requiredCodeMask & (1 << gate.Code.CodeIndex)) == 0)
                return false;
            activeCodes |= 1 << gate.Code.CodeIndex;
        }
        return (activeCodes & requiredCodeMask) == requiredCodeMask;
    }
}