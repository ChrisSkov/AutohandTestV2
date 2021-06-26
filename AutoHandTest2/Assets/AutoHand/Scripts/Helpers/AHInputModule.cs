using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;

public class AHInputModule : BaseInputModule
{
    private PointerEventData eventData;

    protected override void Awake()
    {
        base.Awake();
        if (eventData == null)
            eventData = new PointerEventData(eventSystem);
    }


    public override void Process()
    {
        // Hooks in to Unity's event system to handle hovering
        try{
            eventSystem.RaycastAll(eventData, m_RaycastResultCache);
        }
        catch { }
        eventData.pointerCurrentRaycast = FindFirstRaycast(m_RaycastResultCache);

        HandlePointerExitAndEnter(eventData, eventData.pointerCurrentRaycast.gameObject);

        ExecuteEvents.Execute(eventData.pointerDrag, eventData, ExecuteEvents.dragHandler);
    }

    public void ProcessPress()
    {
        // Hooks in to Unity's event system to process a release
        eventData.pointerPressRaycast = eventData.pointerCurrentRaycast;

        eventData.pointerPress = ExecuteEvents.GetEventHandler<IPointerClickHandler>(eventData.pointerPressRaycast.gameObject);
        eventData.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(eventData.pointerPressRaycast.gameObject);

        ExecuteEvents.Execute(eventData.pointerPress, eventData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(eventData.pointerDrag, eventData, ExecuteEvents.beginDragHandler);
    }

    public void ProcessRelease()
    {
        // Hooks in to Unity's event system to process a press
        GameObject pointerRelease = ExecuteEvents.GetEventHandler<IPointerClickHandler>(eventData.pointerCurrentRaycast.gameObject);

        if (eventData.pointerPress == pointerRelease)
            ExecuteEvents.Execute(eventData.pointerPress, eventData, ExecuteEvents.pointerClickHandler);

        ExecuteEvents.Execute(eventData.pointerPress, eventData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(eventData.pointerDrag, eventData, ExecuteEvents.endDragHandler);

        eventData.pointerPress = null;
        eventData.pointerDrag = null;

        eventData.pointerCurrentRaycast.Clear();
    }

    public PointerEventData GetData() { return eventData; }
}
