/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 켜진 밸브 수에 따라 수위를 합산하고 시대별 물 색상과 물 접촉 사망을 처리한다.
/// </summary>

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class SewerWaterArea : MonoBehaviour
{
    public bool UsePlayerHeight = true;
    [Min(0.01f)] public float PlayerHeightMultiplier = 1f;
    [FormerlySerializedAs("MaximumDepth"), Min(0f)]
    public float HeightPerValve = 1.5f; // 밸브 하나가 올리는 수위
    [Min(0f)] public float RiseSpeed = 0.6f;
    [Min(0f)] public float DrainSpeed = 0.8f;
    public Transform WaterVisual;
    public Transform SurfaceVisual;

    public int ValveCount => valves.Count;
    public int ActiveValveCount
    {
        get
        {
            int count = 0;
            foreach (SewerValve valve in valves)
            {
                if (valve != null && valve.IsOpen)
                    count++;
            }
            return count;
        }
    }
    public float MaximumDepth => valves.Count * Mathf.Max(0f, HeightPerValve);
    public float TargetDepth => ActiveValveCount * Mathf.Max(0f, HeightPerValve);
    public float Depth => depth;
    public float SurfaceY => area.bounds.min.y + depth;

    [Header("Present Water")]
    [SerializeField] private Color presentShallowColor = new(0.12f, 0.58f, 0.62f, 0.88f);
    [SerializeField] private Color presentDeepColor = new(0.025f, 0.19f, 0.27f, 0.94f);
    [SerializeField] private Color presentFoamColor = new(0.68f, 0.96f, 0.91f, 1f);

    [Header("Past Water")]
    [SerializeField] private Color pastShallowColor = new(0.28f, 0.43f, 0.12f, 0.88f);
    [SerializeField] private Color pastDeepColor = new(0.085f, 0.17f, 0.045f, 0.94f);
    [SerializeField] private Color pastFoamColor = new(0.65f, 0.79f, 0.34f, 1f);

    private readonly List<SewerValve> valves = new();
    private BoxCollider2D area;
    private float depth;
    private AudioSource waterRiseSound;
    private float bottomOffset;
    private TimeShiftController timeShift;
    private Renderer waterRenderer;
    private MaterialPropertyBlock waterProperties;
    private static readonly int waterSizeId = Shader.PropertyToID("_WaterSize");
    private static readonly int shallowColorId = Shader.PropertyToID("_ShallowColor");
    private static readonly int deepColorId = Shader.PropertyToID("_DeepColor");
    private static readonly int foamColorId = Shader.PropertyToID("_FoamColor");
    private readonly Collider2D[] hits = new Collider2D[16];

    private void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        area.isTrigger = true;
        if (WaterVisual != null)
        {
            waterRenderer = WaterVisual.GetComponent<Renderer>();
            waterProperties = new MaterialPropertyBlock();
            if (waterRenderer != null)
                waterRenderer.GetPropertyBlock(waterProperties);
        }
        bottomOffset = area.offset.y - area.size.y * 0.5f;
        UpdateVolume();
        UpdateVisuals();
    }

    private void OnEnable()
    {
        timeShift = FindFirstObjectByType<TimeShiftController>();
        TimeFlag flag = TimeFlag.Present;
        if (timeShift != null)
        {
            timeShift.WorldChanged += UpdateWaterColors;
            timeShift.WorldsReady += CollectValves;
            flag = timeShift.CurrentFlag;
        }
        CollectValves();
        UpdateWaterColors(flag);
    }

    private void OnDisable()
    {
        GameSfx.SetLoop(gameObject, ref waterRiseSound, GameSfx.Cue.WaterUp, false);
        if (timeShift != null)
        {
            timeShift.WorldChanged -= UpdateWaterColors;
            timeShift.WorldsReady -= CollectValves;
        }
    }

    /// <summary>
    /// 시대가 바뀌면 수면과 깊은 물, 거품 색상을 함께 갱신한다.
    /// </summary>
    private void UpdateWaterColors(TimeFlag flag)
    {
        if (waterRenderer == null || waterProperties == null)
            return;

        Color shallow = presentShallowColor;
        Color deep = presentDeepColor;
        Color foam = presentFoamColor;
        if (flag == TimeFlag.Past)
        {
            shallow = pastShallowColor;
            deep = pastDeepColor;
            foam = pastFoamColor;
        }
        waterProperties.SetColor(shallowColorId, shallow);
        waterProperties.SetColor(deepColorId, deep);
        waterProperties.SetColor(foamColorId, foam);
        waterRenderer.SetPropertyBlock(waterProperties);
    }

    private void Start()
    {
        if (UsePlayerHeight)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.TryGetComponent(out Collider2D playerCollider))
                HeightPerValve = playerCollider.bounds.size.y * Mathf.Max(0.01f, PlayerHeightMultiplier);
        }
        UpdateVolume();
        UpdateVisuals();
    }

    private void CollectValves()
    {
        foreach (SewerValve valve in FindObjectsByType<SewerValve>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (valve.WaterArea == this)
                RegisterValve(valve);
        }
    }

    public void RegisterValve(SewerValve valve)
    {
        if (valve == null || valve.WaterArea != this || valves.Contains(valve))
            return;
        valve.InitializeState();
        valves.Add(valve);
    }

    public void UnregisterValve(SewerValve valve)
    {
        valves.Remove(valve);
    }

    /// <summary>
    /// 수위를 갱신하고 물에 잠긴 플레이어에게 사망 판정을 적용한다.
    /// </summary>
    private void FixedUpdate()
    {
        UpdateVolume();
        float target = TargetDepth;
        float speed = RiseSpeed;
        if (depth > target)
            speed = DrainSpeed;
        float nextDepth = Mathf.MoveTowards(depth, target, Mathf.Max(0f, speed) * Time.fixedDeltaTime);
        GameSfx.SetLoop(gameObject, ref waterRiseSound, GameSfx.Cue.WaterUp, nextDepth > depth);
        depth = nextDepth;
        UpdateVisuals();
        if (depth <= 0.001f)
            return;

        ContactFilter2D filter = ContactFilter2D.noFilter;
        int count = area.Overlap(filter, hits);
        for (int i = 0; i < count; i++)
        {
            Health health = hits[i].GetComponentInParent<Health>();
            if (health == null || !health.CompareTag("Player") || health.IsDead
                || hits[i].bounds.min.y >= SurfaceY - 0.02f)
            {
                continue;
            }
            health.TakeDamage(int.MaxValue, DamageSource.Environment);
            break;
        }
    }

    private void UpdateVolume()
    {
        Vector2 size = area.size;
        float height = Mathf.Max(0.001f, Mathf.Max(MaximumDepth, depth)) / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        if (Mathf.Approximately(size.y, height))
            return;
        size.y = height;
        area.size = size;
        Vector2 offset = area.offset;
        offset.y = bottomOffset + size.y * 0.5f;
        area.offset = offset;
    }

    private void UpdateVisuals()
    {
        Bounds bounds = area.bounds;
        if (WaterVisual != null)
        {
            WaterVisual.gameObject.SetActive(depth > 0.001f);
            WaterVisual.position = new Vector3(bounds.center.x, bounds.min.y + depth * 0.5f,
                WaterVisual.position.z);
            WaterVisual.localScale = new Vector3(bounds.size.x / Mathf.Abs(transform.lossyScale.x),
                depth / Mathf.Abs(transform.lossyScale.y), 1f);
        }
        if (waterRenderer != null)
        {
            waterProperties.SetVector(waterSizeId, new Vector4(bounds.size.x, depth, 0f, 0f));
            waterRenderer.SetPropertyBlock(waterProperties);
        }
        if (SurfaceVisual != null)
        {
            SurfaceVisual.gameObject.SetActive(depth > 0.001f);
            SurfaceVisual.position = new Vector3(bounds.center.x, SurfaceY, SurfaceVisual.position.z);
        }
    }
}
