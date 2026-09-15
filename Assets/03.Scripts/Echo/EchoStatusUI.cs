/// <summary>
/// 스크립트 최종 수정일: 2026-09-16
/// 기능: 씬에 배치된 UI를 이용해 잔상 슬롯의 선택, 녹화, 저장과 실행 상태를 표시한다.
/// </summary>

using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class EchoStatusUI : MonoBehaviour
{
    private static readonly Color EmptyColor = new(0.05f, 0.06f, 0.09f, 0.9f); // 빈 슬롯 색상
    private static readonly Color OccupiedColor = new(0.12f, 0.2f, 0.42f, 0.95f); // 저장된 슬롯 색상
    private static readonly Color RecordingColor = new(0.18f, 0.25f, 0.85f, 1f); // 녹화 중인 슬롯 색상
    private static readonly Color BorderColor = new(0.8f, 0.85f, 0.95f, 1f); // 기본 슬롯 테두리 색상
    private static readonly Color SelectedColor = new(0.65f, 0.02f, 0.12f, 1f); // 선택된 슬롯 테두리 색상

    public Text stateText; // 현재 잔상 상태를 표시할 텍스트
    public SlotVisual[] slots; // 씬에 배치된 슬롯 UI 목록

    private EchoController controller; // 표시할 잔상 시스템 상태 제공자

    /// <summary>
    /// 잔상 시스템을 연결하고 슬롯 표시를 초기화.
    /// </summary>
    public void Initialize(EchoController source)
    {
        controller = source;
    }

    /// <summary>
    /// 현재 잔상 상태를 매 프레임 화면에 표시.
    /// </summary>
    private void Update()
    {
        if (controller == null)
            return;

        UpdateStateText();
        UpdateSlots();
    }

    /// <summary>
    /// 녹화, 복귀, 실행과 회수 상태 안내 문구를 갱신.
    /// </summary>
    private void UpdateStateText()
    {
        if (controller.IsRecording)
        {
            stateText.text = $"RECORDING  {controller.RecordingProgress * 8f:0.0} / 8.0 sec";
            return;
        }

        if (controller.IsReturning)
        {
            stateText.text = "RETURNING";
            return;
        }

        if (controller.IsPlaybackRunning)
            stateText.text = "PLAYING";
        else if (controller.HasUnrecoveredEchoes)
            stateText.text = "F  RECALL";
        else
            stateText.text = "WHEEL  SELECT     R  RECORD     F  EXECUTE";
    }

    /// <summary>
    /// 각 슬롯의 선택, 저장과 녹화 상태 색상을 갱신한다.
    /// </summary>
    private void UpdateSlots()
    {
        int selectedIndex = controller.SelectedRecordIndex;

        if (selectedIndex < 0 && controller.IsRecording)
            selectedIndex = controller.RecordingSlotIndex;

        int slotCount = Mathf.Min(slots.Length, controller.RecordCapacity);

        for (int index = 0; index < slotCount; index++)
        {
            bool isRecordingSlot = controller.IsRecording
                && index == controller.RecordingSlotIndex;
            bool isOccupied = index < controller.RecordCount;

            slots[index].border.color = index == selectedIndex
                ? SelectedColor
                : BorderColor;
            slots[index].fill.color = isRecordingSlot
                ? RecordingColor
                : isOccupied ? OccupiedColor : EmptyColor;
        }
    }

    [Serializable]
    public sealed class SlotVisual
    {
        public GameObject slotObject; // 슬롯 전체 오브젝트
        public Image border; // 슬롯 테두리 이미지
        public Image fill; // 슬롯 내부 상태 이미지
    }
}
