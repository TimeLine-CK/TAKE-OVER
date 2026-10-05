/// <summary>
/// 스크립트 최종 수정일: 2026-09-28
/// 기능: 단일 재생 잔상의 생성, 실행, 체류와 제거를 관리한다.
/// </summary>

using UnityEngine;

public sealed class EchoPlaybackManager
{
    public bool IsRunning => playback != null && playback.IsRunning;                    // 잔상 행동 재생 여부
    public bool IsLingering => playback != null && playback.IsLingering;                // 잔상 체류 여부
    public bool HasActiveEcho => actor != null;                                         // 실행 또는 체류 중인 잔상 존재 여부
    public float LingerRemaining => playback != null ? playback.LingerRemaining : 0f;   // 남은 잔상 체류 시간
    public Transform ActiveTransform => actor != null ? actor.transform : null;         // 텔레포트 대상 잔상 위치

    private readonly EchoActorFactory actorFactory;         // 재생 잔상을 생성하는 팩토리
    private GameObject actor;                               // 현재 생성된 재생 잔상
    private EchoPlayback playback;                          // 현재 잔상 재생 컴포넌트

    /// <summary>
    /// 재생 잔상을 생성할 팩토리를 저장한다.
    /// </summary>
    public EchoPlaybackManager(EchoActorFactory actorFactory)
    {
        this.actorFactory = actorFactory;
    }

    /// <summary>
    /// 저장된 단일 기록을 실행하는 잔상을 생성한다.
    /// </summary>
    public void Execute(EchoRecording recording, float lingerDuration)
    {
        Clear();
        actor = actorFactory.CreatePlayback(recording, lingerDuration, PlaybackFinished);
        playback = actor.GetComponent<EchoPlayback>();
    }

    /// <summary>
    /// 현재 재생 또는 체류 중인 잔상을 제거한다.
    /// </summary>
    public void Clear()
    {
        if (actor != null)
        {
            Object.Destroy(actor);
        }

        actor = null;
        playback = null;
    }

    /// <summary>
    /// 체류가 끝난 잔상을 제거하고 참조를 초기화한다.
    /// </summary>
    private void PlaybackFinished()
    {
        Clear();
    }
}
