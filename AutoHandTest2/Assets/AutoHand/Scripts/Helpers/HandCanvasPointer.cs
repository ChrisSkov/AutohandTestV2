using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Camera))]
public class HandCanvasPointer : MonoBehaviour
{
    [Header("References")]
    public GameObject hitPointMarker;
    private LineRenderer lineRenderer;


    [Header("Ray settings")]
    public float raycastLength = 8.0f;
    public bool autoShowTarget = true;
    public LayerMask UILayer;
    

    [Header("Events")]
    public UnityEvent StartSelect;
    public UnityEvent StopSelect;
    public UnityEvent StartPoint;
    public UnityEvent StopPoint;
    
    // Internal variables
    private bool hover = false;
    AHInputModule inputModule;
    Camera cam;

    void OnEnable() {
        cam = GetComponent<Camera>();
        foreach (var canvas in FindObjectsOfType<Canvas>())
        {
            canvas.worldCamera = cam;
        }
        cam.enabled = false;

    }
    public void Press()
    {
        // Handle the UI events
        inputModule.ProcessPress();

        // Show the ray when they attemp to press
        if(!autoShowTarget && hover) ShowRay(true);

        // Fire the Unity event
        StartSelect?.Invoke();
    }

    public void Release()
    {
        // Handle the UI events
        inputModule.ProcessRelease();
        
        // Fire the Unity event
        StopSelect?.Invoke();
    }

    private void Awake()
    {
        if (lineRenderer == null)
            gameObject.CanGetComponent(out lineRenderer);

        if(inputModule == null) {
            if(gameObject.CanGetComponent<AHInputModule>(out var inputMod)) {
                inputModule = inputMod;
            }
            else if (!(inputModule = FindObjectOfType<AHInputModule>())) {
                EventSystem system;
                if (system = FindObjectOfType<EventSystem>())
                    inputModule = system.gameObject.AddComponent<AHInputModule>();
            }
        }
    }

    private void Update()
    {
        UpdateLine();
    }

    private void UpdateLine()
    {
        PointerEventData data = inputModule.GetData();
        float targetLength = data.pointerCurrentRaycast.distance == 0 ? raycastLength : data.pointerCurrentRaycast.distance;

        if (data.pointerCurrentRaycast.distance != 0 && !hover)
        {
            // Fire the Unity event
            StartPoint?.Invoke();

            // Show the ray if autoShowTarget is on when they enter the canvas
            if(autoShowTarget) ShowRay(true);

            hover = true;
        }
        else if(data.pointerCurrentRaycast.distance == 0 && hover)
        {
            // Fire the Unity event
            StopPoint?.Invoke();

            // Hide the ray when they leave the canvas
            ShowRay(false);

            hover = false;
        }

        RaycastHit hit = CreateRaycast(targetLength);

        Vector3 endPosition = transform.position + (transform.forward * targetLength);

        if(hit.collider) endPosition = hit.point;

        //Handle the hitmarker
        hitPointMarker.transform.position = endPosition;

        //Handle the line renderer
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, endPosition);
    }

    private RaycastHit CreateRaycast(float dist)
    {
        RaycastHit hit;
        Ray ray = new Ray(transform.position, transform.forward);
        Physics.Raycast(ray, out hit, dist, UILayer.value);

        return hit;
    }

    private void ShowRay(bool show)
    {
        hitPointMarker.SetActive(show);
        lineRenderer.enabled = show;
    }

}
