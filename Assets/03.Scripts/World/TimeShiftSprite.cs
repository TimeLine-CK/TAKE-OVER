/// <summary>
/// 스크립트 최종 수정일: 2026-11-04
/// 기능: 공용 오브젝트의 충돌 상태를 유지하고 시대에 맞는 스프라이트를 표시한다.
/// </summary>

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class TimeShiftSprite : MonoBehaviour
{
    public SpriteRenderer Target; // 외형을 변경할 렌더러
    public Sprite PresentSprite;  // 현재 시대의 이미지
    public Sprite PastSprite;     // 과거 시대의 이미지

    private TimeShiftController timeShift;

    private void OnEnable()
    {
        if (Target == null)
            Target = GetComponent<SpriteRenderer>();

        timeShift = FindFirstObjectByType<TimeShiftController>(FindObjectsInactive.Include);
        if (timeShift == null)
            return;

        timeShift.WorldChanged += Apply;
        Apply(timeShift.CurrentFlag);
    }

    private void OnDisable()
    {
        if (timeShift != null)
            timeShift.WorldChanged -= Apply;
    }

    /// <summary>
    /// 위치와 콜라이더를 바꾸지 않고 해당 시대의 이미지를 적용한다.
    /// </summary>
    public void Apply(TimeFlag flag)
    {
        if (Target == null)
            return;

        Sprite sprite = PresentSprite;
        if (flag == TimeFlag.Past)
            sprite = PastSprite;

        if (sprite != null)
            Target.sprite = sprite;
    }
}
