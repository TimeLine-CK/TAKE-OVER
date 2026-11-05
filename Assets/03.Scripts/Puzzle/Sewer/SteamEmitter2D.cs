/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 분출 방향에 맞춘 삼각형 증기와 피해 범위를 같은 크기로 맞춘다.
/// </summary>

using UnityEngine;

[RequireComponent(typeof(GasArea), typeof(PolygonCollider2D))]
public sealed class SteamEmitter2D : MonoBehaviour
{
    [Min(0.01f)] public float Length = 1.8f;
    [Range(1f, 170f)] public float SpreadAngle = 50f;
    public float DirectionAngle = 90f;

    [SerializeField] private Transform steamVisual;

    private void Awake()
    {
        RebuildShape();
        Debug.Assert(steamVisual != null, "SteamEmitter2D의 Steam Visual을 연결하세요.", this);
    }

    private void OnValidate()
    {
        RebuildShape();
    }

    /// <summary>
    /// 분출구 쪽의 넓은 밑변과 진행 방향의 끝점을 연결해 증기와 판정을 맞춘다.
    /// </summary>
    public void RebuildShape()
    {
        PolygonCollider2D polygon = GetComponent<PolygonCollider2D>();
        if (polygon == null)
            return;

        float length = Mathf.Max(0.01f, Length);
        float halfWidth = length * Mathf.Tan(Mathf.Clamp(SpreadAngle, 1f, 170f) * Mathf.Deg2Rad * 0.5f);
        Quaternion rotation = Quaternion.Euler(0f, 0f, DirectionAngle - 90f);
        polygon.pathCount = 1;
        polygon.offset = Vector2.zero;
        polygon.SetPath(0, new[]
        {
            (Vector2)(rotation * new Vector3(-halfWidth, 0f, 0f)),
            (Vector2)(rotation * new Vector3(0f, length, 0f)),
            (Vector2)(rotation * new Vector3(halfWidth, 0f, 0f))
        });
        polygon.isTrigger = true;
        GetComponent<GasArea>().SetDamageArea(polygon);

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
            box.enabled = false;

        if (steamVisual != null)
        {
            steamVisual.localPosition = rotation * new Vector3(0f, length * 0.5f, 0f);
            steamVisual.localRotation = rotation;
            steamVisual.localScale = new Vector3(halfWidth * 2f, length, 1f);
        }
    }
}
