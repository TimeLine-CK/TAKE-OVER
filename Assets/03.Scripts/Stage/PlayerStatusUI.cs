/// <summary>
/// 스크립트 최종 수정일: 2026-11-05
/// 기능: 플레이어 체력을 10 단위 막대로 표시하고 현재 시대를 안내한다.
/// </summary>

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerStatusUI : MonoBehaviour
{
    [SerializeField] private Health playerHealth;
    [SerializeField] private TimeShiftController timeShift;
    [SerializeField] private TMP_Text healthLabel;
    [SerializeField] private TMP_Text maximumHealthLabel;
    [SerializeField] private TMP_Text worldLabel;
    [SerializeField] private Image worldBadge;
    [SerializeField] private Sprite presentBadge;
    [SerializeField] private Sprite pastBadge;
    [SerializeField] private RectTransform[] healthSegments = System.Array.Empty<RectTransform>();

    private const int healthPerSegment = 10;
    private float[] segmentWidths;

    private void Awake()
    {
        if (playerHealth == null || timeShift == null)
        {
            PlayerMotor player = FindFirstObjectByType<PlayerMotor>(FindObjectsInactive.Include);
            if (player != null)
            {
                if (playerHealth == null)
                    playerHealth = player.GetComponent<Health>();
                if (timeShift == null)
                    timeShift = player.GetComponent<TimeShiftController>();
            }
        }

        segmentWidths = new float[healthSegments.Length];
        for (int i = 0; i < healthSegments.Length; i++)
        {
            if (healthSegments[i] != null)
                segmentWidths[i] = healthSegments[i].rect.width;
        }
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged += OnDamaged;
            playerHealth.Restored += RefreshHealth;
            RefreshHealth();
        }

        if (timeShift != null)
        {
            timeShift.WorldChanged += RefreshWorld;
            RefreshWorld(timeShift.CurrentFlag);
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged -= OnDamaged;
            playerHealth.Restored -= RefreshHealth;
        }

        if (timeShift != null)
            timeShift.WorldChanged -= RefreshWorld;
    }

    private void OnDamaged(DamageSource source)
    {
        RefreshHealth();
    }

    /// <summary>
    /// 남은 체력에 따라 각 칸의 보이는 너비와 체력 숫자를 갱신한다.
    /// </summary>
    private void RefreshHealth()
    {
        if (healthLabel != null)
            healthLabel.SetText("{0:0}", playerHealth.CurrentHealth);
        if (maximumHealthLabel != null)
            maximumHealthLabel.SetText("/ {0:0}", playerHealth.MaximumHealth);

        for (int i = 0; i < healthSegments.Length; i++)
        {
            if (healthSegments[i] == null)
                continue;

            float amount = Mathf.Clamp01(
                (playerHealth.CurrentHealth - i * healthPerSegment) / (float)healthPerSegment);
            healthSegments[i].SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, segmentWidths[i] * amount);
        }
    }

    private void RefreshWorld(TimeFlag flag)
    {
        if (flag == TimeFlag.Past)
        {
            if (worldLabel != null)
                worldLabel.text = "과거";
            if (worldBadge != null)
                worldBadge.sprite = pastBadge;
        }
        else
        {
            if (worldLabel != null)
                worldLabel.text = "현재";
            if (worldBadge != null)
                worldBadge.sprite = presentBadge;
        }
    }
}
