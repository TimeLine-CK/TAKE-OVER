/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 과거의 자외선 범위에서 코드 표시와 플레이어의 수평 감속을 처리한다.
/// </summary>

using System.Collections.Generic;
using UnityEngine;

public sealed class UltravioletLight : MonoBehaviour
{
    public Collider2D LightArea;
    public Renderer BeamRenderer;
    [Range(0f, 1f)] public float MovementMultiplier = 0.5f;
    public float RespawnBelowY = -20f;

    private TimeShiftController timeShift;
    private TimeFlagWolrdRoot worldRoot;
    private RollingCarrier carrier;
    private Rigidbody2D cartBody;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Collider2D[] hits = new Collider2D[32];
    private readonly HashSet<PlayerMotor> players = new();
    private readonly HashSet<PlayerMotor> previousPlayers = new();
    private readonly HashSet<UltravioletCode> codes = new();
    private readonly HashSet<UltravioletCode> previousCodes = new();

    private void Awake()
    {
        timeShift = FindFirstObjectByType<TimeShiftController>();
        worldRoot = GetComponentInParent<TimeFlagWolrdRoot>(true);
        carrier = GetComponentInParent<RollingCarrier>();
        if (carrier != null)
        {
            cartBody = carrier.GetComponent<Rigidbody2D>();
            startPosition = carrier.transform.position;
            startRotation = carrier.transform.rotation;
        }
        if (LightArea == null)
        {
            Debug.LogError("UltravioletLight의 Light Area를 설정해 주세요.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (carrier != null && carrier.transform.position.y < RespawnBelowY)
            RespawnCart();
        bool active = false;
        if (timeShift != null)
            active = timeShift.IsReady && timeShift.CurrentFlag == TimeFlag.Past;
        else if (worldRoot != null)
            active = worldRoot.TimeFlag == TimeFlag.Past;
        if (BeamRenderer != null)
            BeamRenderer.enabled = active;
        if (!active || LightArea == null || !LightArea.enabled || !LightArea.gameObject.activeInHierarchy)
        {
            ClearExposure();
            return;
        }
        Physics2D.SyncTransforms();
        ContactFilter2D filter = ContactFilter2D.noFilter;
        int count = LightArea.Overlap(filter, hits);
        while (count == hits.Length)
        {
            System.Array.Resize(ref hits, hits.Length * 2);
            count = LightArea.Overlap(filter, hits);
        }
        players.Clear();
        codes.Clear();
        for (int i = 0; i < count; i++)
        {
            PlayerMotor player = hits[i].GetComponentInParent<PlayerMotor>();
            if (player != null && player.isActiveAndEnabled)
                players.Add(player);
            UltravioletCode code = hits[i].GetComponentInParent<UltravioletCode>();
            if (code != null && code.isActiveAndEnabled)
                codes.Add(code);
        }
        foreach (PlayerMotor player in previousPlayers)
            if (player != null && !players.Contains(player))
                player.RemoveUltravioletSlow(this);
        foreach (UltravioletCode code in previousCodes)
            if (code != null && !codes.Contains(code))
                code.SetIlluminated(this, false);
        previousPlayers.Clear();
        previousCodes.Clear();
        foreach (PlayerMotor player in players)
        {
            player.SetUltravioletSlow(this, MovementMultiplier);
            previousPlayers.Add(player);
        }
        foreach (UltravioletCode code in codes)
        {
            code.SetIlluminated(this, true);
            previousCodes.Add(code);
        }
    }

    public void RespawnCart()
    {
        if (carrier == null)
            return;
        ClearExposure();
        carrier.transform.SetPositionAndRotation(startPosition, startRotation);
        if (cartBody != null)
        {
            cartBody.position = startPosition;
            cartBody.rotation = startRotation.eulerAngles.z;
            cartBody.linearVelocity = Vector2.zero;
            cartBody.angularVelocity = 0f;
        }
        carrier.RestoreState();
        Physics2D.SyncTransforms();
    }

    private void ClearExposure()
    {
        foreach (PlayerMotor player in previousPlayers)
            if (player != null)
                player.RemoveUltravioletSlow(this);
        foreach (UltravioletCode code in previousCodes)
            if (code != null)
                code.SetIlluminated(this, false);
        previousPlayers.Clear();
        previousCodes.Clear();
    }

    private void OnDisable()
    {
        ClearExposure();
        if (BeamRenderer != null)
            BeamRenderer.enabled = false;
    }
}
