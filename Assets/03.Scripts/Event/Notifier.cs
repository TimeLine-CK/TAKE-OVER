using System;
using UnityEngine;

public class Notifier : MonoBehaviour
{
    public bool AnimeAnd = false;
    public float FloatValue;
    public int IntValue;

    public Action EventAction;
    public Action RenderEvnetAction;

    public void SetEvent(Action eventAction) => EventAction = eventAction;
    public void SetRenderEvent(Action renderEvnetAction) => EventAction = renderEvnetAction;
    public void InvokeEvent() => EventAction?.Invoke();
    public void InvokeRenderEvent() => RenderEvnetAction?.Invoke();
}
