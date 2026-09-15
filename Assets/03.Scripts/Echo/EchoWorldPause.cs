/// <summary>
/// 스크립트 최종 수정일: 2026-09-15
/// 기능: 잔상 녹화 중 선택된 잔상만 움직이도록 시간과 다른 물리 오브젝트를 정지한다.
/// </summary>

using System.Collections.Generic;
using UnityEngine;

public sealed class EchoWorldPause
{
    private readonly List<(Rigidbody2D Body, bool Simulated)> bodies = new(); // 정지 전 리지드바디 상태 목록
    private float previousTimeScale; // 정지 전 게임 시간 배율
    private SimulationMode2D previousSimulationMode; // 정지 전 2D 물리 실행 방식

    /// <summary>
    /// 조작할 잔상을 제외한 물리 오브젝트와 게임 시간을 정지한다.
    /// </summary>
    public void Pause(Rigidbody2D activeBody)
    {
        previousTimeScale = Time.timeScale;
        previousSimulationMode = Physics2D.simulationMode;

        foreach (Rigidbody2D body in Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
        {
            if (body == activeBody)
                continue;

            bodies.Add((body, body.simulated));
            body.simulated = false;
        }

        Physics2D.simulationMode = SimulationMode2D.Script;
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 정지 전에 저장한 물리 상태와 게임 시간을 복구한다.
    /// </summary>
    public void Resume()
    {
        foreach ((Rigidbody2D body, bool simulated) in bodies)
        {
            if (body != null)
                body.simulated = simulated;
        }

        bodies.Clear();
        Physics2D.simulationMode = previousSimulationMode;
        Time.timeScale = previousTimeScale;
    }
}
