using System;
using UnityEngine;

public class Notifier : MonoBehaviour
{
    public bool AnimeAnd = false;
    public float FloatValue;
    public int IntValue;

    public Action EventAction;
    public Action RenderEventAction;

    public void SetEvent(Action eventAction) => EventAction = eventAction;
    public void SetRenderEvent(Action renderEventAction) => RenderEventAction = renderEventAction;
    public void InvokeEvent() => EventAction?.Invoke();
    public void InvokeRenderEvent() => RenderEventAction?.Invoke();
}
