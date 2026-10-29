/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 다른 시대 지형의 충돌 외곽을 충돌 없는 점선 메시로 표시한다.
/// </summary>

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TimeWorldPreview
{
    private readonly GameObject previewObject;
    private readonly Transform worldRoot;
    private readonly Collider2D[] colliders;
    private readonly Matrix4x4[] previousMatrices;
    private readonly Mesh mesh;
    private readonly float lineWidth;
    private readonly float dashLength;
    private readonly float gapLength;
    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();
    private bool isDisposed;

    /// <summary>
    /// 지형 레이어의 콜라이더를 모으고 미리보기 전용 렌더러를 생성한다.
    /// </summary>
    public TimeWorldPreview(Transform root, LayerMask terrainMask, Material material, float width, float dash, float gap)
    {
        worldRoot = root;
        lineWidth = Mathf.Max(0.005f, width);
        dashLength = Mathf.Max(0.02f, dash);
        gapLength = Mathf.Max(0.01f, gap);
        List<Collider2D> terrain = new List<Collider2D>();

        foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true))
        {
            if (collider.isTrigger || !collider.enabled || (terrainMask.value & (1 << collider.gameObject.layer)) == 0)
                continue;

            if (collider.compositeOperation != Collider2D.CompositeOperation.None)
                continue;

            terrain.Add(collider);
        }

        colliders = terrain.ToArray();
        previousMatrices = new Matrix4x4[colliders.Length];
        previewObject = new GameObject(root.name + " Terrain Preview");
        mesh = new Mesh
        {
            name = root.name + " Dashed Outline",
            indexFormat = IndexFormat.UInt32
        };
        previewObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = previewObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = "Foreground";
        renderer.sortingOrder = 100;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Rebuild();
        previewObject.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (!isDisposed && previewObject != null)
            previewObject.SetActive(visible);
    }

    /// <summary>
    /// 지형 위치가 바뀌었을 때 외곽 메시를 다시 생성한다.
    /// </summary>
    public void Refresh()
    {
        if (isDisposed || previewObject == null || mesh == null || !previewObject.activeSelf || worldRoot == null)
            return;

        for (int index = 0; index < colliders.Length; index++)
        {
            if (colliders[index] != null && previousMatrices[index] != colliders[index].transform.localToWorldMatrix)
            {
                Rebuild();
                return;
            }
        }
    }

    /// <summary>
    /// 콜라이더의 실제 외곽 경로를 점선으로 변환한다.
    /// </summary>
    private void Rebuild()
    {
        vertices.Clear();
        triangles.Clear();

        for (int index = 0; index < colliders.Length; index++)
        {
            Collider2D collider = colliders[index];
            if (collider == null)
                continue;
            previousMatrices[index] = collider.transform.localToWorldMatrix;

            if (collider is PolygonCollider2D polygon)
            {
                for (int path = 0; path < polygon.pathCount; path++)
                    AddPath(collider, polygon.GetPath(path), true);
            }
            else if (collider is CompositeCollider2D composite)
            {
                for (int path = 0; path < composite.pathCount; path++)
                {
                    Vector2[] points = new Vector2[composite.GetPathPointCount(path)];
                    composite.GetPath(path, points);
                    AddPath(collider, points, true);
                }
            }
            else if (collider is EdgeCollider2D edge)
            {
                AddPath(collider, edge.points, false);
            }
            else if (collider is BoxCollider2D box)
            {
                Vector2 half = box.size * 0.5f;
                AddPath(collider, new[]
{
                    new Vector2(-half.x, -half.y),
new Vector2(half.x, -half.y),
                    new Vector2(half.x, half.y),
new Vector2(-half.x, half.y)
                }, true);
            }
            else
            {
                Debug.LogWarning("점선 미리보기를 지원하지 않는 지형 콜라이더입니다: " + collider.GetType().Name, collider);
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    /// <summary>
    /// 경로를 따라 일정한 길이의 선과 빈 간격을 이어 붙인다.
    /// </summary>
    private void AddPath(Collider2D collider, Vector2[] points, bool closed)
    {
        if (points.Length < 2)
            return;
        int segmentCount = points.Length - 1;
        if (closed)
            segmentCount++;
        float travelled = 0f;
        float period = dashLength + gapLength;

        for (int index = 0; index < segmentCount; index++)
        {
            Vector3 start = collider.transform.TransformPoint(points[index] + collider.offset);
            Vector3 end = collider.transform.TransformPoint(points[(index + 1) % points.Length] + collider.offset);
            Vector3 difference = end - start;
            float length = difference.magnitude;
            if (length <= 0.0001f)
                continue;
            Vector3 direction = difference / length;
            float distance = 0f;

            while (distance < length)
            {
                float phase = Mathf.Repeat(travelled + distance, period);
                bool draw = phase < dashLength;
                float remaining = period - phase;
                if (draw)
                    remaining = dashLength - phase;
                float step = Mathf.Min(length - distance, Mathf.Max(0.0001f, remaining));
                if (draw)
                    AddDash(start + direction * distance, start + direction * (distance + step));
                distance += step;
            }
            travelled += length;
        }
    }

    /// <summary>
    /// 점선 한 조각을 얇은 사각형 메시로 추가한다.
    /// </summary>
    private void AddDash(Vector3 start, Vector3 end)
    {
        Vector3 direction = (end - start).normalized;
        Vector3 normal = new Vector3(-direction.y, direction.x, 0f) * lineWidth * 0.5f;
        int index = vertices.Count;
        vertices.Add(start - normal);
        vertices.Add(start + normal);
        vertices.Add(end + normal);
        vertices.Add(end - normal);
        triangles.Add(index);
        triangles.Add(index + 1);
        triangles.Add(index + 2);
        triangles.Add(index);
        triangles.Add(index + 2);
        triangles.Add(index + 3);
    }

    public void Dispose()
    {
        if (isDisposed)
            return;
        isDisposed = true;

        if (previewObject != null)
            Object.Destroy(previewObject);
        if (mesh != null)
            Object.Destroy(mesh);
    }
}
