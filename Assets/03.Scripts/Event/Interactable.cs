/// <summary>
/// 스크립트 최종 수정일: 2026-10-16
/// 기능: 장치의 상호작용 안내와 조작 가능한 시대를 정의한다.
/// </summary>

using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    public enum InteractionEra { Both, Past, Present }

    [SerializeField] private InteractionEra allowedEra = InteractionEra.Both; // 조작 가능한 시대
    [SerializeField] private string prompt = "Interact";                      // 상호작용 안내 문구
    [SerializeField] private UnityEvent onInteract = new();                    // F 입력으로 호출할 장치 동작

    public string Prompt => prompt;
    public InteractionEra AllowedEra => allowedEra;

    /// <summary>
    /// 현재 시대에서 사용할 수 있는 조작 안내를 반환한다.
    /// </summary>
    public virtual string GetPrompt(TimeFlag currentFlag)
    {
        if (CanInteract(currentFlag))
            return "F - " + prompt;
        if (allowedEra == InteractionEra.Past)
            return "Past only";
        return "Present only";
    }

    /// <summary>
    /// 활성 상태와 현재 시대를 확인해 조작 가능 여부를 반환한다.
    /// </summary>
    public virtual bool CanInteract(TimeFlag currentFlag)
    {
        if (!isActiveAndEnabled)
            return false;
        if (allowedEra == InteractionEra.Both)
            return true;
        if (allowedEra == InteractionEra.Past)
            return currentFlag == TimeFlag.Past;
        return currentFlag == TimeFlag.Present;
    }

    public virtual void Interact(PlayerInteractor player)
    {
        onInteract.Invoke();
    }
}
