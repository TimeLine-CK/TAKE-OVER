/// <summary>
/// 스크립트 최종 수정일: 2026-09-29
/// 기능: 씬에 배치된 UI를 이용해 TP와 단일 잔상의 녹화, 저장, 실행 상태를 표시한다.
/// </summary>

using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class EchoStatusUI : MonoBehaviour
{
    private static readonly Color EmptyColor = new(0.05f, 0.06f, 0.09f, 0.9f);      // 빈 슬롯 색상
    private static readonly Color OccupiedColor = new(0.12f, 0.2f, 0.42f, 0.95f);   // 저장된 슬롯 색상
    private static readonly Color RecordingColor = new(0.18f, 0.25f, 0.85f, 1f);    // 녹화 중인 슬롯 색상
    private static readonly Color BorderColor = new(0.8f, 0.85f, 0.95f, 1f);        // 기본 슬롯 테두리 색상

    public Text stateText;              // 현재 잔상 상태를 표시할 텍스트
    public Text tpText;                 // 현재 TP Point를 표시할 텍스트
    public SlotVisual[] slots;          // 씬에 배치된 슬롯 UI 목록

    private EchoController controller;  // 표시할 잔상 시스템 상태 제공자

    /// <summary>
    /// 잔상 시스템을 연결하고 슬롯 표시를 초기화.
    /// </summary>
    public void Initialize(EchoController source)
    {
        controller = source;

        for (int index = 1; index < slots.Length; index++)
        {
            if (slots[index]?.slotObject != null)
                slots[index].slotObject.SetActive(false);
        }
    }

    /// <summary>
    /// 현재 잔상 상태를 매 프레임 화면에 표시.
    /// </summary>
    private void Update()
    {
        if (controller == null)
            return;

        UpdateStateText();
        UpdatePointText();
        UpdateSlots();
    }

    /// <summary>
    /// 녹화, 복귀, 실행과 회수 상태 안내 문구를 갱신.
    /// </summary>
    private void UpdateStateText()
    {
        if (controller.IsRecording)
        {
            float duration = controller.RecordingProgress * controller.MaximumRecordingDuration;
            stateText.text = $"R  RECORD     E  EXECUTE     Q  TELEPORT {duration:0.0} / {controller.MaximumRecordingDuration:0.0} sec";
            return;
        }

        if (controller.IsReturning)
        {
            stateText.text = "R  RECORD     E  EXECUTE     Q  TELEPORT";
            return;
        }

        if (controller.IsPlaybackRunning)
        {
            stateText.text = "PLAYING";
        }
        else if (controller.IsEchoLingering)
        {
            stateText.text = $"ECHO HOLD  {controller.EchoLingerRemaining:0.0} sec";
        }
        else if (controller.RecordCount > 0)
        {
            stateText.text = "R  RECORD     E  EXECUTE     Q  TELEPORT";
        }
        else
        {
            stateText.text = "R  RECORD     E  EXECUTE     Q  TELEPORT";
        }

    }

    /// <summary>
    /// 현재 TP Point를 최대치와 함께 표시한다.
    /// </summary>
    private void UpdatePointText()
    {
        if (tpText != null)
            tpText.text = $"TP  {controller.CurrentTP:0} / {controller.MaximumTP:0}";
    }

    /// <summary>
    /// 각 슬롯의 선택, 저장과 녹화 상태 색상을 갱신한다.
    /// </summary>
    private void UpdateSlots()
    {
        int slotCount = Mathf.Min(slots.Length, controller.RecordCapacity);

        for (int index = 0; index < slotCount; index++)
        {
            bool isRecordingSlot = controller.IsRecording
                && index == controller.RecordingSlotIndex;
            bool isOccupied = index < controller.RecordCount;

            slots[index].border.color = BorderColor;
            slots[index].fill.color = isRecordingSlot ? RecordingColor: isOccupied ? OccupiedColor : EmptyColor;
        }
    }

    [Serializable]
    public sealed class SlotVisual
    {
        public GameObject slotObject;   // 슬롯 전체 오브젝트
        public Image border;            // 슬롯 테두리 이미지
        public Image fill;              // 슬롯 내부 상태 이미지
    }
}
