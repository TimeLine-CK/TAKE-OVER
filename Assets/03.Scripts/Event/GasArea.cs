/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 누출 영역의 가스 농도와 예고 시간 및 반복 환경 피해를 처리한다.
/// </summary>

using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class GasArea : MonoBehaviour
{
    [SerializeField] private string valveId = "RoomGas"; // 시대를 연결할 밸브 식별자
    [SerializeField] private MeshRenderer gasRenderer; // 영역을 덮는 연무 렌더러
    [SerializeField] private bool isLeaking = true;     // 누출 여부
    [SerializeField] private float warningDuration = 1.2f; // 누출 후 피해가 시작되기까지의 시간
    [SerializeField] private float damageInterval = 0.65f; // 반복 피해 간격
    [SerializeField] private int damage = 9;             // 한 번에 적용할 환경 피해량
    [SerializeField] private float fadeSpeed = 1.5f;    // 가스가 나타나고 걷히는 속도

    public bool IsLeaking => isLeaking;
    public string ValveId => valveId;
    private BoxCollider2D area;
    private float nextDamageTime;
    private float density;
    private bool hasBeenVisible;
    private readonly Collider2D[] hits = new Collider2D[32];
    private MaterialPropertyBlock properties;
    private static readonly int densityId = Shader.PropertyToID("_Density");

    private void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        properties = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (area == null)
            area = GetComponent<BoxCollider2D>();
        if (properties == null)
            properties = new MaterialPropertyBlock();
    }

    /// <summary>
    /// 누출 상태를 바꾸고 새로 누출할 때만 피해 예고 시간을 시작한다.
    /// </summary>
    public void SetLeaking(bool value)
    {
        if (isLeaking == value)
            return;
        isLeaking = value;
        if (value)
            nextDamageTime = Time.time + Mathf.Max(0f, warningDuration);
    }

    /// <summary>
    /// 연무를 부드럽게 전환하고 범위 안 플레이어에게 한 번씩 피해를 적용한다.
    /// </summary>
    private void Update()
    {
        if (isLeaking && !hasBeenVisible)
        {
            hasBeenVisible = true;
            nextDamageTime = Time.time + Mathf.Max(0f, warningDuration);
        }
        float targetDensity = 0f;
        if (isLeaking)
            targetDensity = 1f;
        density = Mathf.MoveTowards(density, targetDensity, Mathf.Max(0.01f, fadeSpeed) * Time.deltaTime);
        if (gasRenderer != null)
        {
            properties.SetFloat(densityId, density);
            gasRenderer.SetPropertyBlock(properties);
            gasRenderer.enabled = density > 0.001f;
        }
        if (!isLeaking || Time.time < nextDamageTime)
            return;
        nextDamageTime = Time.time + Mathf.Max(0.01f, damageInterval);
        ContactFilter2D filter = new ContactFilter2D();
        filter.NoFilter();
        int count = area.Overlap(filter, hits);
        for (int index = 0; index < count; index++)
        {
            Health health = hits[index].GetComponentInParent<Health>();
            if (health == null || !health.CompareTag("Player") || health.IsDead)
                continue;
            health.TakeDamage(damage, DamageSource.Environment);
            break;
        }
    }
}
