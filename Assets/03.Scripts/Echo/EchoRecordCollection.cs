/// <summary>
/// 스크립트 최종 수정일: 2026-09-15
/// 기능: 잔상 기록 슬롯의 저장, 선택, 삭제와 최대 개수를 관리한다.
/// </summary>

using System;
using System.Collections.Generic;

public sealed class EchoRecordCollection
{
    public const int MaximumRecordCount = 6; // 지원하는 최대 기록 슬롯 수

    public IReadOnlyList<EchoRecording> Records => records; // 저장된 잔상 기록 읽기 전용 목록
    public EchoRecording Selected => selectedIndex >= 0
        ? records[selectedIndex]
        : null; // 현재 선택된 잔상 기록
    public int Count => records.Count; // 현재 저장된 기록 수
    public int Capacity => capacity; // 사용할 수 있는 슬롯 수
    public int SelectedIndex => selectedIndex; // 현재 선택된 기록 위치
    public bool HasUnrecoveredEchoes { get; set; } // 실행 후 회수되지 않은 잔상 존재 여부

    private readonly List<EchoRecording> records; // 저장된 잔상 기록 목록
    private readonly int capacity; // 현재 사용할 수 있는 기록 슬롯 수
    private int selectedIndex = -1; // 현재 선택된 기록 위치
    private int nextId = 1; // 새 기록에 부여할 번호

    /// <summary>
    /// 지정한 슬롯 수로 빈 잔상 기록 모음을 생성한다.
    /// </summary>
    public EchoRecordCollection(int capacity = 3)
    {
        if (capacity < 1 || capacity > MaximumRecordCount)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        this.capacity = capacity;
        records = new List<EchoRecording>(capacity);
    }

    /// <summary>
    /// 다음 번호와 시작 상태를 가진 새 기록을 생성한다.
    /// </summary>
    public EchoRecording Create(int zoneId, PlayerMotor.MotorSnapshot startState)
    {
        return new EchoRecording(nextId++, zoneId, startState);
    }

    /// <summary>
    /// 완성된 기록을 슬롯에 추가하고 선택 상태를 유지한다.
    /// </summary>
    public void Add(EchoRecording recording)
    {
        int selectedId = Selected?.Id ?? -1;

        if (records.Count == capacity)
            records.RemoveAt(0);

        records.Add(recording);

        selectedIndex = records.FindIndex(item => item.Id == selectedId);

        if (selectedIndex < 0)
            selectedIndex = 0;
    }

    /// <summary>
    /// 지정한 방향만큼 선택 슬롯을 이동한다.
    /// </summary>
    public void MoveSelection(int direction)
    {
        if (records.Count == 0 || direction == 0)
            return;

        selectedIndex = Math.Clamp(
            selectedIndex + Math.Sign(direction),
            0,
            records.Count - 1
        );
    }

    /// <summary>
    /// 현재 선택된 기록을 삭제하고 선택 위치를 보정한다.
    /// </summary>
    public bool DeleteSelected()
    {
        if (selectedIndex < 0)
            return false;

        records.RemoveAt(selectedIndex);

        if (records.Count == 0)
        {
            selectedIndex = -1;
            HasUnrecoveredEchoes = false;
        }
        else
        {
            selectedIndex = Math.Min(selectedIndex, records.Count - 1);
        }

        return true;
    }

    /// <summary>
    /// 모든 기록과 선택 및 회수 상태를 초기화한다.
    /// </summary>
    public void Clear()
    {
        records.Clear();
        selectedIndex = -1;
        HasUnrecoveredEchoes = false;
    }
}
