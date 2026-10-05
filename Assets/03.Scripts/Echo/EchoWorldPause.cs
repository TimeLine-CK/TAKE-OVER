/// <summary>
/// 스크립트 최종 수정일: 2026-09-29
/// 기능: 잔상 녹화 중 월드 물리만 느리게 진행하고 녹화 잔상의 속도는 유지한다.
/// </summary>

using UnityEngine;

public sealed class EchoWorldPause
{
    private float previousTimeScale;                        // 정지 전 게임 시간 배율
    private SimulationMode2D previousSimulationMode;        // 정지 전 2D 물리 실행 방식
    private float recordingTimeScale = 1f;                  // 녹화 중 적용할 시간 배율

    /// <summary>
    /// 월드 시간 배율을 낮추고 2D 물리를 수동 실행 방식으로 전환한다.
    /// </summary>
    public void SlowDown(float timeScale)
    {
        previousTimeScale = Time.timeScale;
        previousSimulationMode = Physics2D.simulationMode;
        recordingTimeScale = Mathf.Clamp(timeScale, 0.01f, 1f);
        Physics2D.simulationMode = SimulationMode2D.Script;
        Time.timeScale = recordingTimeScale;
    }

    /// <summary>
    /// 녹화용 한 틱만큼 월드 물리를 진행하고 지정한 잔상의 감속을 보정한다.
    /// </summary>
    public void Simulate(float unscaledDeltaTime, Rigidbody2D unscaledBody = null)
    {
        if (unscaledBody != null && unscaledBody.simulated)
        {
            unscaledBody.linearVelocity /= recordingTimeScale;
        }

        Physics2D.Simulate(unscaledDeltaTime * recordingTimeScale);

        if (unscaledBody != null && unscaledBody.simulated)
        {
            unscaledBody.linearVelocity *= recordingTimeScale;
        }
    }

    /// <summary>
    /// 정지 전에 저장한 물리 상태와 게임 시간을 복구한다.
    /// </summary>
    public void Resume()
    {
        Physics2D.simulationMode = previousSimulationMode;
        Time.timeScale = previousTimeScale;
        recordingTimeScale = 1f;
    }
}
