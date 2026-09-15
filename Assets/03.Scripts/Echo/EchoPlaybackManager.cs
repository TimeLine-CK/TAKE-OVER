/// <summary>
/// 스크립트 최종 수정일: 2026-09-15
/// 기능: 저장된 잔상들의 생성, 동시 실행, 정지와 제거를 관리한다.
/// </summary>

using System.Collections.Generic;
using UnityEngine;

public sealed class EchoPlaybackManager
{
    public bool IsRunning => runningCount > 0; // 하나 이상의 잔상이 실행 중인지 여부

    private readonly EchoActorFactory actorFactory; // 재생 잔상을 생성하는 팩토리
    private readonly List<GameObject> actors = new(); // 현재 생성된 재생 잔상 목록
    private int runningCount; // 아직 재생 중인 잔상 수

    /// <summary>
    /// 재생 잔상을 생성할 팩토리를 저장한다.
    /// </summary>
    public EchoPlaybackManager(EchoActorFactory actorFactory)
    {
        this.actorFactory = actorFactory;
    }

    /// <summary>
    /// 전달된 모든 기록을 재생하는 잔상을 생성한다.
    /// </summary>
    public void Execute(IReadOnlyList<EchoRecording> recordings)
    {
        runningCount = recordings.Count;

        foreach (EchoRecording recording in recordings)
            actors.Add(actorFactory.CreatePlayback(recording, PlaybackFinished));
    }

    /// <summary>
    /// 현재 실행 중인 모든 잔상을 정지한다.
    /// </summary>
    public void Stop()
    {
        foreach (GameObject actor in actors)
        {
            if (actor != null
                && actor.TryGetComponent(out EchoPlayback playback)
                && playback.IsRunning)
            {
                playback.Pause();
            }
        }

        runningCount = 0;
    }

    /// <summary>
    /// 지정한 기록 번호에 해당하는 재생 잔상을 제거한다.
    /// </summary>
    public void Remove(int recordId)
    {
        for (int index = actors.Count - 1; index >= 0; index--)
        {
            GameObject actor = actors[index];

            if (actor == null)
            {
                actors.RemoveAt(index);
                continue;
            }

            if (actor.name != $"Echo Playback {recordId}")
                continue;

            if (actor.TryGetComponent(out EchoPlayback playback) && playback.IsRunning)
                runningCount = Mathf.Max(0, runningCount - 1);

            Object.Destroy(actor);
            actors.RemoveAt(index);
            return;
        }
    }

    /// <summary>
    /// 생성된 모든 재생 잔상을 제거하고 실행 상태를 초기화한다.
    /// </summary>
    public void Clear()
    {
        foreach (GameObject actor in actors)
        {
            if (actor != null)
                Object.Destroy(actor);
        }

        actors.Clear();
        runningCount = 0;
    }

    /// <summary>
    /// 잔상 하나의 재생 완료를 실행 수에 반영한다.
    /// </summary>
    private void PlaybackFinished()
    {
        runningCount = Mathf.Max(0, runningCount - 1);
    }
}
