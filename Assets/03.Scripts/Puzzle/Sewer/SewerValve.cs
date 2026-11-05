/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 과거에서 밸브를 조작해 연결된 증기를 끄고 공용 수조에 한 단계 수위를 더한다.
/// </summary>

using UnityEngine;

public sealed class SewerValve : Interactable
{
    public string ProgressId; // 밸브 상태 저장 키
    public SewerWaterArea WaterArea; // 수위를 더할 수조
    public GasArea[] SteamAreas = System.Array.Empty<GasArea>(); // 이 밸브로 차단할 증기
    [Min(0.01f)] public float HoldDuration = 3f;
    public Transform Wheel;
    public Transform ProgressBar;
    public Renderer StateIndicator;

    public bool IsOpen => isOpen;
    public float Progress => Mathf.Clamp01(heldTime / Mathf.Max(0.01f, HoldDuration));

    [SerializeField] private bool isOpen;

    private bool initialized;
    private float heldTime;
    private AudioSource valveSound;
    private bool completed;
    private MaterialPropertyBlock properties;

    private void Awake()
    {
        InitializeState();
    }

    internal void InitializeState()
    {
        if (initialized)
            return;
        initialized = true;
        if (string.IsNullOrWhiteSpace(ProgressId))
            ProgressId = gameObject.scene.path + "/" + name;
        if (StageProgress.Switches.TryGetValue(ProgressId, out bool saved))
            isOpen = saved;
        ApplySteam();
    }

    private void OnEnable()
    {
        if (WaterArea != null)
            WaterArea.RegisterValve(this);
        UpdateVisuals();
    }

    private void Start()
    {
        UpdateVisuals();
    }

    public override bool CanInteract(TimeFlag flag)
    {
        return flag == TimeFlag.Past && WaterArea != null && base.CanInteract(flag);
    }

    public override void Interact(PlayerInteractor player)
    {
        EndHoldInteract(player);
    }

    /// <summary>
    /// 유지 시간이 차면 밸브를 한 번 전환하고 키를 놓을 때까지 재전환을 막는다.
    /// </summary>
    public override void HoldInteract(PlayerInteractor player, float deltaTime)
    {
        if (completed || player == null || !player.TryGetComponent(out TimeShiftController shift)
            || !shift.IsReady || shift.IsPreparing || !CanInteract(shift.CurrentFlag))
        {
            return;
        }

        GameSfx.SetLoop(gameObject, ref valveSound, GameSfx.Cue.Valve, deltaTime > 0f);
        heldTime += Mathf.Max(0f, deltaTime);
        if (Wheel != null)
            Wheel.Rotate(0f, 0f, -120f * deltaTime);
        if (heldTime >= Mathf.Max(0.01f, HoldDuration))
        {
            completed = true;
            GameSfx.SetLoop(gameObject, ref valveSound, GameSfx.Cue.Valve, false);
            SetOpen(!isOpen);
            base.Interact(player);
        }
        UpdateVisuals();
    }

    public override void EndHoldInteract(PlayerInteractor player)
    {
        GameSfx.SetLoop(gameObject, ref valveSound, GameSfx.Cue.Valve, false);
        heldTime = 0f;
        completed = false;
        UpdateVisuals();
    }

    public void SetOpen(bool value)
    {
        InitializeState();
        isOpen = value;
        StageProgress.Switches[ProgressId] = value;
        ApplySteam();
        UpdateVisuals();
    }

    private void ApplySteam()
    {
        foreach (GasArea steam in SteamAreas)
        {
            if (steam != null)
                steam.SetLeaking(!isOpen);
        }
    }

    private void UpdateVisuals()
    {
        if (ProgressBar != null)
        {
            ProgressBar.gameObject.SetActive(heldTime > 0f);
            Vector3 scale = ProgressBar.localScale;
            scale.x = Progress;
            ProgressBar.localScale = scale;
        }
        if (StateIndicator != null)
        {
            Color color = new Color(1f, 0.42f, 0.12f);
            if (isOpen)
                color = new Color(0.25f, 0.9f, 0.65f);
            if (properties == null)
                properties = new MaterialPropertyBlock();
            StateIndicator.GetPropertyBlock(properties);
            properties.SetColor("_Color", color);
            StateIndicator.SetPropertyBlock(properties);
        }
    }

    private void OnDisable()
    {
        EndHoldInteract(null);
    }

    private void OnDestroy()
    {
        if (WaterArea != null)
            WaterArea.UnregisterValve(this);
    }
}
