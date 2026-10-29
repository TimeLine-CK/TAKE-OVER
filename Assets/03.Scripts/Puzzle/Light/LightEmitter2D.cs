/// <summary>
/// 스크립트 최종 수정일: 2026-10-30
/// 기능: 과거의 광원에서 빛의 반사 경로를 계산하고 선으로 표시한다.
/// </summary>

using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public sealed class LightEmitter2D : MonoBehaviour
{
    public Transform LightStartPoint; // 빛이 시작될 위치
    [SerializeField] private LayerMask obstacleMask = ~0; // 빛을 막거나 반사하는 레이어
    [SerializeField, Min(0.1f)] private float maximumDistance = 50f; // 빛이 이동할 전체 거리
    [SerializeField, Range(0, 64)] private int maximumReflections = 16; // 반사 횟수 제한

    private const float SurfaceOffset = 0.005f;
    private LineRenderer lineRenderer;
    private TimeFlagWolrdRoot worldRoot;
    private TimeShiftController timeShift;
    private Vector3[] points;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
    private Collider2D[] mirrorSurfaces;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.numCapVertices = 0;
        lineRenderer.numCornerVertices = 0;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        SpriteRenderer sourceRenderer = GetComponent<SpriteRenderer>();
        if (sourceRenderer != null)
        {
            lineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            lineRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        }
        worldRoot = GetComponentInParent<TimeFlagWolrdRoot>(true);
        timeShift = FindFirstObjectByType<TimeShiftController>();
    }

    private void OnEnable()
    {
        if (timeShift != null)
            timeShift.WorldsReady += RefreshMirrorSurfaces;
        RefreshMirrorSurfaces();
    }

    private void RefreshMirrorSurfaces()
    {
        TimeFlagWolrdRoot past = null;
        TimeFlagWolrdRoot present = null;
        if (timeShift != null && timeShift.IsReady)
        {
            past = timeShift.GetWorld(TimeFlag.Past);
            present = timeShift.GetWorld(TimeFlag.Present);
        }
        List<Collider2D> surfaces = new List<Collider2D>();
        foreach (MirrorReflector2D mirror in FindObjectsByType<MirrorReflector2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mirror.gameObject.scene != gameObject.scene
                && (timeShift == null || mirror.gameObject.scene != timeShift.gameObject.scene)
                && (past == null || mirror.gameObject.scene != past.gameObject.scene)
                && (present == null || mirror.gameObject.scene != present.gameObject.scene))
                continue;
            foreach (Collider2D surface in mirror.GetComponentsInChildren<Collider2D>(true))
            {
                if (!surface.isTrigger && surface.GetComponentInParent<MirrorReflector2D>() == mirror)
                    surfaces.Add(surface);
            }
        }
        mirrorSurfaces = surfaces.ToArray();
    }

    private void LateUpdate()
    {
        RefreshBeam();
    }

    /// <summary>
    /// 벽과 거울 뒷면에서는 멈추고 거울 앞면에서만 다음 광선을 계산한다.
    /// </summary>
    public void RefreshBeam()
    {
        if (lineRenderer == null)
            return;
        if ((worldRoot != null && worldRoot.TimeFlag != TimeFlag.Past)
            || (timeShift != null && (!timeShift.IsReady || timeShift.CurrentFlag != TimeFlag.Past)))
        {
            lineRenderer.positionCount = 0;
            return;
        }

        int reflectionLimit = Mathf.Clamp(maximumReflections, 0, 64);
        if (points == null || points.Length != reflectionLimit + 2)
            points = new Vector3[reflectionLimit + 2];

        // 회전이나 플랫폼 이동이 이번 프레임의 광선 검사에 반영되도록 한다.
        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleMask);
        filter.useTriggers = false;
        Vector2 origin = transform.position;
        if (LightStartPoint != null)
            origin = LightStartPoint.position;
        Vector2 direction = transform.right;
        float remainingDistance = Mathf.Max(0f, maximumDistance);
        int pointCount = 1;
        points[0] = origin;
        Collider2D previousSurface = null;

        for (int reflection = 0; reflection <= reflectionLimit && remainingDistance > 0f; reflection++)
        {
            int count = Physics2D.Raycast(origin, direction, filter, hits, remainingDistance);
            int nearest = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit2D hit = hits[index];
                if (hit.collider == previousSurface && hit.distance <= SurfaceOffset * 2f)
                    continue;
                if (hit.distance < nearestDistance)
                {
                    nearest = index;
                    nearestDistance = hit.distance;
                }
            }

            if (nearest < 0)
            {
                points[pointCount++] = origin + direction * remainingDistance;
                break;
            }

            RaycastHit2D surface = hits[nearest];
            points[pointCount++] = surface.point;
            SolarBattery battery = surface.collider.GetComponentInParent<SolarBattery>();
            if (battery != null)
                battery.ReceiveLight(surface.collider);
            MirrorReflector2D mirror = surface.collider.GetComponentInParent<MirrorReflector2D>();
            if (mirror == null || !mirror.isActiveAndEnabled || reflection == reflectionLimit)
                break;

            // 반사면의 로컬 +Y가 앞면이며 Raycast의 법선은 입사 방향에 따라 뒤집힌다.
            if (Vector2.Dot(direction, surface.collider.transform.up) >= -0.0001f)
                break;

            remainingDistance -= surface.distance + SurfaceOffset;
            direction = Vector2.Reflect(direction, surface.normal).normalized;
            origin = surface.point + direction * SurfaceOffset;
            if (!mirror.IsFixed)
                UpdateMirrorAim(mirror, surface.collider, origin, direction, remainingDistance, filter);
            previousSurface = surface.collider;
        }

        lineRenderer.positionCount = pointCount;
        for (int index = 0; index < pointCount; index++)
            lineRenderer.SetPosition(index, points[index]);
    }

    private void UpdateMirrorAim(MirrorReflector2D mirror, Collider2D source, Vector2 origin,
        Vector2 direction, float distance, ContactFilter2D filter)
    {
        float bestError = 6f;
        foreach (Collider2D target in mirrorSurfaces)
        {
            if (target == null || target == source || !target.enabled || !target.gameObject.activeInHierarchy)
                continue;
            if (target.GetComponentInParent<MirrorReflector2D>() == mirror)
                continue;

            Vector2 offset = (Vector2)target.bounds.center - origin;
            if (offset.sqrMagnitude < SurfaceOffset * SurfaceOffset || offset.sqrMagnitude > distance * distance)
                continue;
            float error = Vector2.SignedAngle(direction, offset);
            if (Mathf.Abs(error) >= Mathf.Abs(bestError) || Vector2.Dot(offset, target.transform.up) >= 0f)
                continue;

            int count = Physics2D.Raycast(origin, offset.normalized, filter, hits, offset.magnitude + SurfaceOffset);
            Collider2D nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                if (hits[index].collider == source && hits[index].distance <= SurfaceOffset * 2f)
                    continue;
                if (hits[index].distance < nearestDistance)
                {
                    nearest = hits[index].collider;
                    nearestDistance = hits[index].distance;
                }
            }
            if (nearest == target)
                bestError = error;
        }
        if (Mathf.Abs(bestError) < 6f)
            mirror.SetAimError(bestError * 0.5f);
    }

    private void OnDisable()
    {
        if (timeShift != null)
            timeShift.WorldsReady -= RefreshMirrorSurfaces;
        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
    }
}
