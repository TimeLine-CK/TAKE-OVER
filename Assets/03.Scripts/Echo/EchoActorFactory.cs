/// <summary>
/// 스크립트 최종 수정일: 2026-09-28
/// 기능: 플레이어 원본을 복제해 녹화용 또는 재생용 잔상으로 구성한다.
/// </summary>

using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class EchoActorFactory
{
    private static readonly Color EchoColor = new(0.35f, 0.9f, 1f, 0.65f); // 잔상 렌더러에 적용할 색상

    private readonly GameObject source; // 잔상 복제에 사용할 플레이어 원본

    /// <summary>
    /// 잔상 생성에 사용할 플레이어 원본을 저장한다.
    /// </summary>
    public EchoActorFactory(GameObject source)
    {
        this.source = source;
    }

    /// <summary>
    /// 플레이어가 직접 조작하며 행동을 기록할 잔상을 생성한다.
    /// </summary>
    public EchoActor CreateRecording(int id, Vector2 position, Quaternion rotation)
    {
        GameObject actor = CreateCopy(position, rotation);
        actor.name = $"Echo Recording {id}";

        EchoActor result = Configure(actor);
        result.Combat.DamageEnabled = false;
        result.Combat.EffectsEnabled = false;

        foreach (Animator animator in actor.GetComponentsInChildren<Animator>())
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        return result;
    }

    /// <summary>
    /// 저장된 기록을 실행할 재생용 잔상을 생성한다.
    /// </summary>
    public GameObject CreatePlayback(EchoRecording recording, float lingerDuration, Action finished)
    {
        GameObject actor = CreateCopy(recording.StartState.Position, source.transform.rotation);
        actor.name = $"Echo Playback {recording.Id}";

        EchoActor result = Configure(actor);
        result.Motor.enabled = false;
        result.Motor.RestoreSnapshot(recording.StartState);

        EchoPlayback playback = actor.AddComponent<EchoPlayback>();
        playback.Initialize(recording, lingerDuration, finished);
        return actor;
    }

    /// <summary>
    /// 원본 입력을 잠시 비활성화한 상태로 플레이어 사본을 생성한다.
    /// </summary>
    private GameObject CreateCopy(Vector2 position, Quaternion rotation)
    {
        PlayerInput sourceInput = source.GetComponent<PlayerInput>();
        sourceInput.enabled = false;

        try
        {
            return UnityEngine.Object.Instantiate(source, position, rotation);
        }
        finally
        {
            sourceInput.enabled = true;
        }
    }

    /// <summary>
    /// 복제된 플레이어의 입력, 충돌 레이어, 렌더링 색상을 잔상용으로 설정한다.
    /// </summary>
    private static EchoActor Configure(GameObject actor)
    {
        UnityEngine.Object.Destroy(actor.GetComponent<EchoController>());
        UnityEngine.Object.Destroy(actor.GetComponent<TPPointController>());
        UnityEngine.Object.Destroy(actor.GetComponent<PlayerCommandController>());
        actor.tag = "Afterimage";
        actor.GetComponent<PlayerInputReader>().enabled = false;

        PlayerMotor motor = actor.GetComponent<PlayerMotor>();
        PlayerCombat combat = actor.GetComponent<PlayerCombat>();
        PlayerAnimator animator = actor.GetComponentInChildren<PlayerAnimator>();

        int echoLayer = LayerMask.NameToLayer("Afterimage");
        if (echoLayer >= 0)
            SetLayerRecursively(actor, echoLayer);

        foreach (Collider2D collider in actor.GetComponentsInChildren<Collider2D>())
            collider.excludeLayers = ~motor.GroundMask;

        foreach (SpriteRenderer renderer in actor.GetComponentsInChildren<SpriteRenderer>())
            renderer.color = EchoColor;

        return new EchoActor(actor, motor, combat, animator);
    }

    /// <summary>
    /// 대상과 모든 자식 오브젝트에 잔상 레이어를 적용한다.
    /// </summary>
    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;

        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}

public readonly struct EchoActor
{
    public GameObject GameObject { get; } // 잔상 루트 오브젝트
    public PlayerMotor Motor { get; } // 잔상 이동 컴포넌트
    public PlayerCombat Combat { get; } // 잔상 공격 컴포넌트
    public PlayerAnimator Animator { get; } // 잔상 애니메이션 컴포넌트
    public Rigidbody2D Body => GameObject.GetComponent<Rigidbody2D>(); // 잔상 리지드바디

    /// <summary>
    /// 잔상 동작에 필요한 컴포넌트 참조를 하나로 묶는다.
    /// </summary>
    public EchoActor(GameObject gameObject, PlayerMotor motor, PlayerCombat combat, PlayerAnimator animator)
    {
        GameObject = gameObject;
        Motor = motor;
        Combat = combat;
        Animator = animator;
    }
}
