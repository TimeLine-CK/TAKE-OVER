/// <summary>
/// 스크립트 최종 수정일: 2026-09-28
/// 기능: 플레이어의 잔상 기록 가능 구역 진입과 이탈을 감지한다.
/// </summary>

using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class EchoZone : MonoBehaviour
{
    public int ZoneId => zoneId; // 잔상 기록 구역 식별 번호
    public int RecordCapacity => 1; // 이 구역에서 사용할 기록 슬롯 수

    [SerializeField] private int zoneId = 1; // 잔상 기록 구역 식별 번호

    /// <summary>
    /// 구역 콜라이더가 Trigger로 설정됐는지 검사한다.
    /// </summary>
    private void Awake()
    {
        Debug.Assert(GetComponent<Collider2D>().isTrigger,"EchoZone의 Collider2D는 Is Trigger여야 합니다. 확인해라", this);
    }


    /// <summary>
    /// 플레이어가 들어오면 현재 잔상 구역을 설정한다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        EchoController controller = other.GetComponentInParent<EchoController>();
        if (controller != null)
            controller.EnterZone(this);
    }

    /// <summary>
    /// 플레이어가 나가면 현재 잔상 구역을 해제한다.
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        EchoController controller = other.GetComponentInParent<EchoController>();
        if (controller != null)
        {
            controller.ExitZone(this);
        }
    }
}
