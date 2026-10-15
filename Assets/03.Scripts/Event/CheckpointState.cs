/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 실행 중 체크포인트의 시대, 적 생사, 물리 위치와 밸브 상태를 보관한다.
/// </summary>

using System.Collections.Generic;
using UnityEngine;

public sealed class CheckpointState
{
    private struct UnitState
    {
        public Health Health;
        public int HitPoints;
        public bool Active;
    }
    private struct BodyState
    {
        public Rigidbody2D Body;
        public Vector2 Position;
        public float Rotation;
        public Vector2 Velocity;
        public float AngularVelocity;
    }
    private struct ValveState
    {
        public GasValve Valve;
        public bool Open;
        public bool ClosedInPast;
    }

    public TimeFlag Era { get; }
    public Vector3 SpawnPosition { get; }
    private readonly List<UnitState> units = new();
    private readonly List<BodyState> bodies = new();
    private readonly List<ValveState> valves = new();

    /// <summary>
    /// 로드된 스테이지에서 플레이어 이외의 상태를 메모리에 저장한다.
    /// </summary>
    public CheckpointState(TimeShiftController timeShift, Health player, Vector3 spawnPosition)
    {
        SpawnPosition = spawnPosition;
        Era = TimeFlag.Present;
        if (timeShift != null)
            Era = timeShift.CurrentFlag;
        foreach (Health health in Object.FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (health == player)
                continue;
            units.Add(new UnitState
            {
                Health = health,
                HitPoints = health.CurrentHealth,
                Active = health.gameObject.activeSelf
            });
        }
        foreach (Rigidbody2D body in Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (body.gameObject == player.gameObject || body.GetComponent<TurretProjectile>() != null)
                continue;
            bodies.Add(new BodyState
            {
                Body = body,
                Position = body.position,
                Rotation = body.rotation,
                Velocity = body.linearVelocity,
                AngularVelocity = body.angularVelocity
            });
        }
        foreach (GasValve valve in Object.FindObjectsByType<GasValve>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            valves.Add(new ValveState
            {
                Valve = valve,
                Open = valve.IsOpen,
                ClosedInPast = valve.HasClosedInPast
            });
        }
    }

    /// <summary>
    /// 남아 있는 탄환을 지우고 저장한 적, 밸브와 위치를 복원한다.
    /// </summary>
    public void Restore()
    {
        foreach (TurretProjectile projectile in Object.FindObjectsByType<TurretProjectile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            projectile.gameObject.SetActive(false);
            Object.Destroy(projectile.gameObject);
        }
        foreach (UnitState unit in units)
        {
            if (unit.Health == null)
                continue;
            unit.Health.RestoreState(unit.HitPoints);
            unit.Health.gameObject.SetActive(unit.Active && unit.HitPoints > 0);
        }
        foreach (ValveState valve in valves)
            if (valve.Valve != null)
                valve.Valve.RestoreState(valve.Open, valve.ClosedInPast);
        foreach (BodyState saved in bodies)
        {
            if (saved.Body == null)
                continue;
            RollingCarrier carrier = saved.Body.GetComponent<RollingCarrier>();
            Vector3 position = saved.Body.transform.position;
            position.x = saved.Position.x;
            position.y = saved.Position.y;
            saved.Body.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, saved.Rotation));
            saved.Body.position = saved.Position;
            saved.Body.rotation = saved.Rotation;
            saved.Body.linearVelocity = saved.Velocity;
            saved.Body.angularVelocity = saved.AngularVelocity;
            if (carrier != null)
                carrier.RestoreState();
        }
        Physics2D.SyncTransforms();
    }
}
