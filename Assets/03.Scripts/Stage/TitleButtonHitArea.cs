/// <summary>
/// 스크립트 최종 수정일: 2026-11-01
/// 기능: 타이틀 버튼 이미지의 투명한 부분을 클릭 판정에서 제외한다.
/// </summary>

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public sealed class TitleButtonHitArea : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Image>().alphaHitTestMinimumThreshold = 0.1f;
    }
}
