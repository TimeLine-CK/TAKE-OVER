/// <summary>
/// 스크립트 최종 수정일: 2026-09-14
/// 기능: 플레이어의 잔상 기록 가능 구역 진입과 이탈을 감지한다.
/// </summary>

using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class EchoZone : MonoBehaviour
{
    public int ZoneId => zoneId; // 잔상 기록 구역 식별 번호
    public int RecordCapacity => recordCapacity; // 이 구역에서 사용할 기록 슬롯 수

    [SerializeField] private int zoneId = 1; // 잔상 기록 구역 식별 번호
    [SerializeField, Range(1, 6)] private int recordCapacity = 3; // 이 구역에서 사용할 기록 슬롯 수

    /// <summary>
    /// 구역 콜라이더가 Trigger로 설정됐는지 검사한다.
    /// </summary>
    private void Awake()
    {
        Debug.Assert(GetComponent<Collider2D>().isTrigger,
            "EchoZone의 Collider2D는 Is Trigger여야 합니다.", this);
    }



    /// <summary>
    /// 플레이어가 들어오거나 나갈때 잔상 구역을 설정하거나 해제한다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        EchoController controller = other.GetComponentInParent<EchoController>();
        if (controller != null)
            controller.EnterZone(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        EchoController controller = other.GetComponentInParent<EchoController>();
        if (controller != null)
            controller.ExitZone(this);
    }
}
