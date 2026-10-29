/// <summary>
/// 스크립트 최종 수정일: 2026-10-27
/// 기능: 조준 방향으로 짧게 뻗는 칼날 잔상과 색 분리 잔선을 메시로 생성하고 풀로 반환한다.
/// </summary>

using System.Collections.Generic;
using UnityEngine;

public sealed class SlashEffect : MonoBehaviour
{
    [SerializeField, Range(0.08f, 0.2f)] private float duration = 0.12f;         // 참격 이펙트 유지 시간
    [SerializeField, Range(4, 12)] private int segmentCount = 6;                 // 칼날을 구성하는 선분 수
    [SerializeField] private float emission = 3f;                       // HDR 발광 강도
    [SerializeField, ColorUsage(true, true)]
    private Color bodyColor =
        new(0.35f, 0.02f, 0.55f, 0.72f);                                       // 보라색 후행 잔상 색상
    [SerializeField, ColorUsage(true, true)]
    private Color bladeColor =
        new(0.02f, 1.2f, 2.5f, 0.95f);                                         // 청록색 칼날 외곽 색상
    [SerializeField, ColorUsage(true, true)]
    private Color edgeColor =
        new(2.5f, 2.8f, 3f, 1f);                                               // 흰색 칼날 중심선 색상

    public bool IsPlaying
    {
        get;
        private set;
    }                                 // 현재 이펙트 재생 여부

    private static readonly int progressId = Shader.PropertyToID("_Progress");
    private static readonly int effectAgeId = Shader.PropertyToID("_EffectAge");
    private static readonly int emissionId = Shader.PropertyToID("_Emission");
    private static readonly int seedId = Shader.PropertyToID("_Seed");
    private static readonly int bodyColorId = Shader.PropertyToID("_BodyColor");
    private static readonly int bladeColorId = Shader.PropertyToID("_BladeColor");
    private static readonly int edgeColorId = Shader.PropertyToID("_EdgeColor");
    private static int playSequence;

    private readonly List<Vector3> vertices = new(512);                          // 재사용할 메시 정점 목록
    private readonly List<Vector2> uvs = new(512);                              // 재사용할 메시 UV 목록
    private readonly List<Vector2> layers = new(512);                           // 정점별 칼날 층 정보
    private readonly List<Color> colors = new(512);                             // 재사용할 메시 색상 목록
    private readonly List<int> triangles = new(1536);                           // 재사용할 메시 삼각형 목록

    private Mesh mesh;                                                          // 런타임에 갱신할 참격 메시
    private MeshRenderer meshRenderer;                                          // 참격 메시를 출력할 렌더러
    private MaterialPropertyBlock properties;                                   // 개별 이펙트 셰이더 값
    private Vector2[] path;                                                      // 현재 프레임의 베지어 중심 곡선
    private Transform target;                                                    // 위치를 따라갈 공격 주체
    private Vector2 offset;                                                      // 공격 주체 기준 월드 좌표 오프셋
    private float age;                                                           // 이펙트 재생 경과 시간
    private float seed;                                                          // 이펙트별 셰이더 변화값

    /// <summary>
    /// 재사용할 메시와 셰이더 값을 준비한다.
    /// </summary>
    private void Awake()
    {
        mesh = new Mesh
        {
            name = "Slash Effect Mesh"
        };
        mesh.MarkDynamic();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        properties = new MaterialPropertyBlock();
        path = new Vector2[segmentCount + 1];
    }

    /// <summary>
    /// 공격 방향과 크기에 맞춰 참격 이펙트를 처음부터 재생한다.
    /// </summary>
    public void Play(
        Transform followTarget,
        Vector2 worldOffset,
        Vector2 worldScale,
        Vector2 direction,
        int sortingLayer,
        int sortingOrder
    )
    {
        target = followTarget;
        offset = worldOffset;
        age = 0f;
        seed = ++playSequence;

        transform.position = (Vector2)target.position + offset;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        transform.localScale = new Vector3(
            Mathf.Abs(worldScale.x),
            Mathf.Abs(worldScale.y),
            1f
        );

        meshRenderer.sortingLayerID = sortingLayer;
        meshRenderer.sortingOrder = sortingOrder;
        IsPlaying = true;
        gameObject.SetActive(true);
        Draw(0f);
    }

    /// <summary>
    /// 진행률에 맞춰 참격 메시를 펼치고 꼬리부터 제거한다.
    /// </summary>
    private void Update()
    {
        if (!IsPlaying)
        {
            return;
        }

        age += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(age / duration);

        if (progress >= 1f)
        {
            Clear();
            return;
        }

        Draw(progress);
    }

    private void LateUpdate()
    {
        if (IsPlaying && target != null)
        {
            transform.position = (Vector2)target.position + offset;
        }
    }

    /// <summary>
    /// 현재 진행률에 맞춰 주 칼날선과 잔선 및 작은 파편을 생성한다.
    /// </summary>
    private void Draw(float progress)
    {
        vertices.Clear();
        uvs.Clear();
        layers.Clear();
        colors.Clear();
        triangles.Clear();

        float head = EaseOut(Mathf.Clamp01(progress / 0.16f));
        float tail;
        if (progress < 0.2f)
        {
            tail = 0f;
        }
        else
        {
            tail = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, progress));
        }
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 1f, progress));
        float thinFade = fade * (1f - progress * 0.22f);
        float fragmentFade = thinFade * Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(0.04f, 0.2f, progress)
        );

        BuildPath(
            new Vector2(-0.08f, -0.03f),
            new Vector2(0.42f, -0.11f),
            new Vector2(0.94f, 0.04f),
            new Vector2(1.34f, 0.09f),
            tail,
            head
        );
        AddStrip(0.11f * (1f - progress * 0.5f), fade, -0.03f, 0f);
        AddStrip(0.075f, thinFade, 0f, 1f);
        AddStrip(0.022f, thinFade, 0.01f, 2f);

        BuildPath(
            new Vector2(-0.18f, 0.13f),
            new Vector2(0.2f, 0.08f),
            new Vector2(0.68f, 0.14f),
            new Vector2(1.08f, 0.17f),
            Mathf.Clamp01(tail + 0.1f),
            Mathf.Clamp01(head - 0.12f)
        );
        AddStrip(0.026f, thinFade * 0.72f, 0f, 1f);

        BuildPath(
            new Vector2(-0.12f, -0.18f),
            new Vector2(0.18f, -0.16f),
            new Vector2(0.56f, -0.09f),
            new Vector2(0.88f, -0.04f),
            Mathf.Clamp01(tail + 0.18f),
            Mathf.Clamp01(head - 0.2f)
        );
        AddStrip(0.016f, thinFade * 0.5f, 0f, 0f);

        AddFragment(new Vector2(0.18f, 0.2f) + new Vector2(-0.12f, 0.05f) * progress, 0.035f, fragmentFade);
        AddFragment(new Vector2(0.48f, -0.2f) + new Vector2(-0.08f, -0.04f) * progress, 0.028f, fragmentFade);
        AddFragment(new Vector2(1.18f, 0.2f) + new Vector2(0.08f, 0.04f) * progress, 0.024f, fragmentFade);

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, layers);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0, false);
        mesh.RecalculateBounds();

        properties.Clear();
        properties.SetFloat(progressId, progress);
        properties.SetFloat(effectAgeId, age);
        properties.SetFloat(emissionId, emission);
        properties.SetFloat(seedId, seed);
        properties.SetColor(bodyColorId, bodyColor);
        properties.SetColor(bladeColorId, bladeColor);
        properties.SetColor(edgeColorId, edgeColor);
        meshRenderer.SetPropertyBlock(properties);
    }

    /// <summary>
    /// 표시할 시작점과 끝점 사이의 cubic Bezier 곡선을 계산한다.
    /// </summary>
    private void BuildPath(
        Vector2 pointA,
        Vector2 pointB,
        Vector2 pointC,
        Vector2 pointD,
        float start,
        float end
    )
    {
        for (int index = 0; index < path.Length; index++)
        {
            float ratio = index / (float)(path.Length - 1);
            float time = Mathf.Lerp(start, end, ratio);
            float inverse = 1f - time;

            path[index] = inverse * inverse * inverse * pointA
                + 3f * inverse * inverse * time * pointB
                + 3f * inverse * time * time * pointC
                + time * time * time * pointD;
        }
    }

    /// <summary>
    /// 참격 주변에 흩어지는 작은 마름모 파편을 추가한다.
    /// </summary>
    private void AddFragment(Vector2 center, float size, float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        int firstVertex = vertices.Count;
        Color vertexColor = new(1f, 1f, 1f, alpha);

        Vector2 halfSize = new(size * 1.8f, size * 0.5f);
        vertices.Add(center + new Vector2(-halfSize.x, halfSize.y));
        vertices.Add(center + halfSize);
        vertices.Add(center + new Vector2(halfSize.x, -halfSize.y));
        vertices.Add(center - halfSize);

        for (int index = 0; index < 4; index++)
        {
            uvs.Add(new Vector2(0.5f, 0.5f));
            layers.Add(new Vector2(1f, 0f));
            colors.Add(vertexColor);
        }

        triangles.Add(firstVertex);
        triangles.Add(firstVertex + 1);
        triangles.Add(firstVertex + 2);
        triangles.Add(firstVertex);
        triangles.Add(firstVertex + 2);
        triangles.Add(firstVertex + 3);
    }

    /// <summary>
    /// 중심 곡선의 tangent에 수직인 방향으로 좌우 정점을 생성한다.
    /// </summary>
    private void AddStrip(float width, float alpha, float normalOffset, float layer)
    {
        int firstVertex = vertices.Count;

        for (int index = 0; index < path.Length; index++)
        {
            float ratio = index / (float)(path.Length - 1);
            Vector2 tangent = (
                path[Mathf.Min(index + 1, path.Length - 1)]
                - path[Mathf.Max(index - 1, 0)]
            ).normalized;
            Vector2 normal = new(-tangent.y, tangent.x);
            float taper = Mathf.Pow(Mathf.Max(0.0001f, Mathf.Sin(ratio * Mathf.PI)), 0.62f);
            float halfWidth = width * taper;
            Vector2 center = path[index] + normal * normalOffset * taper;
            Color vertexColor = new(1f, 1f, 1f, alpha);

            vertices.Add(center - normal * halfWidth);
            vertices.Add(center + normal * halfWidth);
            uvs.Add(new Vector2(ratio, 0f));
            uvs.Add(new Vector2(ratio, 1f));
            layers.Add(new Vector2(layer, 0f));
            layers.Add(new Vector2(layer, 0f));
            colors.Add(vertexColor);
            colors.Add(vertexColor);

            if (index == 0)
            {
                continue;
            }

            int previous = firstVertex + (index - 1) * 2;
            int current = firstVertex + index * 2;

            triangles.Add(previous);
            triangles.Add(current);
            triangles.Add(previous + 1);
            triangles.Add(previous + 1);
            triangles.Add(current);
            triangles.Add(current + 1);
        }
    }

    private static float EaseOut(float value)
    {
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    /// <summary>
    /// 메시와 재생 상태를 초기화하고 오브젝트를 풀로 반환한다.
    /// </summary>
    public void Clear()
    {
        IsPlaying = false;
        target = null;
        age = 0f;

        if (mesh != null)
        {
            mesh.Clear();
        }

        if (meshRenderer != null)
        {
            meshRenderer.SetPropertyBlock(null);
        }

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }
}
