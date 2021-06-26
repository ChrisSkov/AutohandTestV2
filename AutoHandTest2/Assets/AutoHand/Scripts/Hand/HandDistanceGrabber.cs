using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using CustomAttributes;


namespace Autohand{
    [DefaultExecutionOrder(2)]
    public class HandDistanceGrabber : MonoBehaviour{
        [Header("Hands")]
        [Tooltip("The primaryHand used to trigger pulling or flicking")]
        public Hand primaryHand;
        [Tooltip("This is important for catch assistance")]
        public Hand secondaryHand;

        [Header("Grabbable")]
        [Header("Pointer Options")]
        public Transform forwardPointer;
        public LineRenderer line;
        [Space]
        public float maxRange = 5;
        [Tooltip("Defaults to grabbable on start if none")]
        public LayerMask layers;
        [Space]
        public Material defaultTargetedMaterial;
        [Tooltip("The highlight material to use when pulling")]
        public Material defaultSelectedMaterial;

        [Header("Pull Options")]
        public bool useInstantPull = false;
        [Tooltip("If false will default to distance pull, set pullGrabDistance to 0 for instant pull on select")]
        public bool useFlickPull = false;

        [Tooltip("The magnitude of your hands angular velocity for \"flick\" to start")]
        [ConditionalHide("useFlickPull")]
        public float flickThreshold = 7f; 

        
        [Tooltip("Set to 0 for instant grab")]
        [ConditionalShow("useFlickPull")]
        public float pullGrabDistance = 0.1f;

        [Space]
        [Tooltip("The radius around of thrown object")]
        public float catchAssistRadius = 0.2f;

        [Space]
        [Header("Events")]
        public UnityEvent OnPull;
        [Space]
        [Space]
        public UnityEvent StartPoint;
        public UnityEvent StopPoint;
        [Space]
        [Space]
        public UnityEvent StartTarget;
        public UnityEvent StopTarget;
        [Space]
        [Space]
        public UnityEvent StartSelect;
        public UnityEvent StopSelect;
        
        List<CatchAssistData> catchAssisted;

        DistanceGrabbable targetingDistanceGrabbable;
        DistanceGrabbable selectingDistanceGrabbable;

        float catchAssistSeconds = 3f;
        bool pointing;
        bool pulling;
        Vector3 startPullPosition;
        RaycastHit hit;
        RaycastHit selectionHit;
        Quaternion lastRotation;
        float selectedEstimatedRadius;
        float startLookAssist;
        bool lastInstantPull;

        void Start(){
            catchAssisted = new List<CatchAssistData>();
            if(layers == 0)
                layers = LayerMask.GetMask(Hand.grabbableLayerNameDefault);

            startLookAssist = primaryHand.lookAssistSpeed;

            primaryHand.OnTriggerGrab += TryCatchAssist;
            if(secondaryHand != null)
                secondaryHand.OnTriggerGrab += TryCatchAssist;

            primaryHand.OnBeforeGrabbed += (hand, grabbable) => { StopPointing(); CancelSelect(); };

            if(useInstantPull){
                SetInstantPull();
            }
        }

        public void SetInstantPull(){
            useInstantPull = true;
        }

        public void SetPull(float distance) {
            useInstantPull = false;
            useFlickPull = false;
            pullGrabDistance = distance;
        }

        public void SetFlickPull(float threshold) {
            useInstantPull = false;
            useFlickPull = true;
            flickThreshold = threshold;
        }

        void Update() {
            CheckDistanceGrabbable();
            if (lastInstantPull != useInstantPull) {
                if (useInstantPull) {
                    useFlickPull = false;
                    pullGrabDistance = 0;
                }
                lastInstantPull = useInstantPull;
            }
        }

        void CheckDistanceGrabbable() {
            if(!pulling && pointing && primaryHand.holdingObj == null){
                bool didHit = Physics.Raycast(forwardPointer.position, forwardPointer.forward, out hit, maxRange, layers);
                DistanceGrabbable hitGrabbable;
                GrabbableChild hitGrabbableChild;
                if(didHit) {
                    if(hit.transform.CanGetComponent(out hitGrabbable)) {
                        if(hitGrabbable != targetingDistanceGrabbable) {
                            StartTargeting(hitGrabbable);
                        }
                    }
                    else if(hit.transform.CanGetComponent(out hitGrabbableChild)) {
                        if(hitGrabbableChild.grabParent.transform.CanGetComponent(out hitGrabbable)) {
                            if(hitGrabbable != targetingDistanceGrabbable) {
                                StartTargeting(hitGrabbable);
                            }
                        }
                    }
                }
                else {
                    StopTargeting();
                }

                if(line != null) {
                    if(didHit) {
                        line.positionCount = 2;
                        line.SetPositions(new Vector3[]{forwardPointer.position, hit.point});
                    }
                    else{
                        line.positionCount = 2;
                        line.SetPositions(new Vector3[]{forwardPointer.position, forwardPointer.position + forwardPointer.forward*maxRange});
                    }
                }
            }
            else if(pulling && primaryHand.holdingObj == null) {
                if(useFlickPull){
                    TryFlickPull(selectingDistanceGrabbable);
                }
                else{
                    TryDistancePull(selectingDistanceGrabbable);
                }
            }
            else if(targetingDistanceGrabbable != null){
                StopTargeting();
            }
        }

        


        public virtual void StartPointing(){
            pointing = true;
            primaryHand.lookAssistSpeed = 0;
            StartPoint?.Invoke();
        }

        public virtual void StopPointing() {
            pointing = false;
            primaryHand.lookAssistSpeed = startLookAssist;
            if(line != null) {
                line.positionCount = 0;
                line.SetPositions(new Vector3[0]);
            }
            StopPoint?.Invoke();
            StopTargeting();
        }



        public virtual void StartTargeting(DistanceGrabbable target) {
            if(target.targetable && primaryHand.CanGrab(target.grabbable)){
                StopTargeting();
                targetingDistanceGrabbable = target;
                targetingDistanceGrabbable?.grabbable.Highlight(primaryHand, GetTargetedMaterial(targetingDistanceGrabbable));
                targetingDistanceGrabbable?.StartTargeting?.Invoke();
                StartTarget?.Invoke();
            }
        }

        public virtual void StopTargeting() {
            targetingDistanceGrabbable?.grabbable.Unhighlight(primaryHand);
            targetingDistanceGrabbable?.StopTargeting?.Invoke();
            targetingDistanceGrabbable = null;
            StopTarget?.Invoke();
        }
        
        public virtual void SelectTarget() {
            if(targetingDistanceGrabbable != null){
                pulling = true;
                startPullPosition = primaryHand.transform.localPosition;
                lastRotation = transform.rotation;
                selectionHit = hit;
                selectingDistanceGrabbable = targetingDistanceGrabbable;
                selectedEstimatedRadius = Vector3.Distance(selectionHit.point, selectingDistanceGrabbable.transform.position);
                selectingDistanceGrabbable.grabbable.Unhighlight(primaryHand);
                selectingDistanceGrabbable.grabbable.Highlight(primaryHand, GetSelectedMaterial(selectingDistanceGrabbable));
                selectingDistanceGrabbable?.StartSelecting?.Invoke();
                targetingDistanceGrabbable?.StopTargeting?.Invoke();
                targetingDistanceGrabbable = null;
                StartSelect?.Invoke();
                StopPointing();
            }
        }
        
        public virtual void CancelSelect() {
            pulling = false;
            selectingDistanceGrabbable?.grabbable.Unhighlight(primaryHand);
            selectingDistanceGrabbable?.StopSelecting.Invoke();
            StopSelect?.Invoke();
            selectingDistanceGrabbable = null;
        }

        public virtual void ActivatePull(){
            if(selectingDistanceGrabbable){
                OnPull?.Invoke();
                selectingDistanceGrabbable.OnPull?.Invoke();
                if(selectingDistanceGrabbable.instantPull){
                    primaryHand.Grab(selectionHit, selectingDistanceGrabbable.grabbable);
                }
                else{
                    StartCoroutine(StartCatchAssist(selectingDistanceGrabbable, selectedEstimatedRadius));
                    selectingDistanceGrabbable.SetTarget(primaryHand.palmTransform);
                }

                CancelSelect();
            }
        }
        

        void TryDistancePull(DistanceGrabbable selectedGrabbable) {
            if(Vector3.Distance(startPullPosition, primaryHand.transform.localPosition) > pullGrabDistance) {
                ActivatePull();
            }
        }

        void TryFlickPull(DistanceGrabbable selectedGrabbable){
            Quaternion deltaRotation = transform.rotation * Quaternion.Inverse(lastRotation);
            lastRotation = transform.rotation;
            var getAngle = 0f;
            Vector3 getAxis = Vector3.zero;
            deltaRotation.ToAngleAxis(out getAngle, out getAxis);
            getAngle *= Mathf.Deg2Rad;
            float speed = (getAxis * getAngle * (1f / Time.deltaTime)).magnitude;
            
            if (speed > flickThreshold){
                if(selectedGrabbable){
                    ActivatePull();
                }
            }
        }


        

        Material GetSelectedMaterial(DistanceGrabbable grabbable) {
            if(grabbable.ignoreHighlights)
                return null;
            return grabbable.selectedMaterial != null ? grabbable.selectedMaterial : defaultSelectedMaterial; 
        }
        Material GetTargetedMaterial(DistanceGrabbable grabbable) {
            if(grabbable.ignoreHighlights)
                return null;
            return grabbable.selectedMaterial != null ? grabbable.targetedMaterial : defaultTargetedMaterial; 
        }

        void TryCatchAssist(Hand hand, Grabbable grab) {
            for(int i = 0; i < catchAssisted.Count; i++) {

                var distance = Vector3.Distance(hand.palmTransform.position+ hand.palmTransform.forward*catchAssistRadius, catchAssisted[i].grab.transform.position)-catchAssisted[i].estimatedRadius;
                if(distance < catchAssistRadius) {
                    RaycastHit catchHit;
                    Ray ray = new Ray(hand.palmTransform.position,  catchAssisted[i].grab.transform.position-hand.palmTransform.position);
                    if(Physics.Raycast(ray, out catchHit, catchAssistRadius*2, LayerMask.GetMask(Hand.grabbableLayerNameDefault, Hand.grabbingLayerName))){
                        if(catchHit.transform.gameObject == catchAssisted[i].grab.gameObject){
                            hand.Grab(catchHit, catchAssisted[i].grab);
                        }
                    }
                }
            }
        }


        IEnumerator StartCatchAssist(DistanceGrabbable grab, float estimatedRadius) {
            var catchData = new CatchAssistData(grab.grabbable, catchAssistRadius);
            catchAssisted.Add(catchData);
            grab.grabbable.OnGrabEvent += (hand, grabbable) => { if(catchAssisted.Contains(catchData)) catchAssisted.Remove(catchData); };
            grab.OnPullCanceled += (hand, grabbable) => { if(catchAssisted.Contains(catchData)) catchAssisted.Remove(catchData); };

            yield return new WaitForSeconds(catchAssistSeconds);

            grab.grabbable.OnGrabEvent -= (hand, grabbable) => { if(catchAssisted.Contains(catchData)) catchAssisted.Remove(catchData); };
            grab.OnPullCanceled -= (hand, grabbable) => { if(catchAssisted.Contains(catchData)) catchAssisted.Remove(catchData); };
            if(catchAssisted.Contains(catchData))
                catchAssisted.Remove(catchData);
        }

        private void OnDrawGizmosSelected() {
            if(primaryHand)
                Gizmos.DrawWireSphere(primaryHand.palmTransform.position + primaryHand.palmTransform.forward*catchAssistRadius*4/5f + primaryHand.palmTransform.up*catchAssistRadius*1/4f, catchAssistRadius);
        }
    }

    struct CatchAssistData {
        public Grabbable grab;
        public float estimatedRadius;

        public CatchAssistData(Grabbable grab, float estimatedRadius) {
            this.grab = grab;
            this.estimatedRadius = estimatedRadius;
        }
    }
}
