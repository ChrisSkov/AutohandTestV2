using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using NaughtyAttributes;

namespace Autohand {
    public class Grabbable : MonoBehaviour {


        [Header("Holding Settings")]

        [Tooltip("The physics body to connect this colliders grab to - if left empty will default to local body")]
        public Rigidbody body;
        public Vector3 heldPositionOffset;
        public Vector3 heldRotationOffset;

        [Tooltip("Which hand this can be held by")]
        public HandType handType = HandType.both;
        
        [Tooltip("Experimental - ignores weight of held object while held")]
        public bool ignoreWeight = false;

        

        [Space, Header("Grab Settings")]

        [Tooltip("If false will not be grabbeable by hands. If turned false while being held, will be dropped")]
        public bool isGrabbable = true;

        [Tooltip("Whether or not this can be grabbed with more than one hand")]
        public bool singleHandOnly = false;

        [ShowIf("singleHandOnly")]
        [Tooltip("if false single handed items cannot be passes back and forth on grab")]
        public bool allowHeldSwapping = true;

        [Tooltip("Will the item automatically return the hand on grab - good for saved poses, bad for heavy things")]
        public bool instantGrab = false;

        [Tooltip("Creates an offset an grab so the hand will not return to the hand on grab - Good for statically jointed grabbable objects")]
        public bool maintainGrabOffset = false;





        [Space, Header("Advanced Grab Settings")]

        [Tooltip("Adds and links a GrabbableChild to each child with a collider on start - So the hand can grab them")]
        public bool makeChildrenGrabbable = true;

        [Tooltip("This will NOT parent the object under the hands on grab. This will parent the object to the parents of the hand, which allow you to move the hand parent object smoothly while holding an item, but will also allow you to move items that are very heavy - recommended for all objects that aren't very heavy or jointed to other rigidbodies")]
        public bool parentOnGrab = true;

        [Tooltip("Lock hand in place on grab")]
        public bool lockHandOnGrab = false;

        [Tooltip("I.E. Grab Prioirty - SMALLER IS BETTER - Multiplys highlight distance by this when calculating which object to grab. Hands always grab closest object to palm")]
        [Min(0)]
        public float grabDistancePriority = 1;

        [ShowIf("parentOnGrab")]
        [Tooltip("For the special use case of having grabbable objects with physics jointed peices move properly while being held")]
        public List<Rigidbody> jointedBodies;
        


        
        [Space, Header("Release Settings")]
        [Tooltip("How much to multiply throw by for this grabbable when releasing - 0-1 for no or reduced throw strength")]
        public float throwMultiplyer = 1;
        [Tooltip("How much to multiply throw anglular velocity by for this grabbable when releasing - 0-1 for no or reduced throw strength")]
        public float throwAngleMultiplyer = 1;

        [Tooltip("Whether or not this item is dropped on teleport, good for static object and heavy things")]
        public bool releaseOnTeleport = false;

        [Tooltip("The number of seconds that the hand collision should ignore the released object\n (Good for increased placement precision and resolves clipping errors)"), Min(0)]
        public float ignoreReleaseTime = 0.25f;



        [Space, Header("Highlight Settings")]
        [Tooltip("Multiplies look assist speed")]
        public float lookAssistMultiplyer = 1f;
        [Tooltip("A copy of the mesh will be created and slighly scaled and this material will be applied to create a highlight effect with options")]
        public Material hightlightMaterial;



        [Space, Header("Break Settings")]
        [Tooltip("The required force to break the fixedJoint\n " +
                 "Turn this to \"infinity\" to disable (Might cause jitter)\n" +
                "Ideal value depends on hand mass and velocity settings")]
        public float jointBreakForce = 5000;
        
        [Tooltip("The required torque to break the fixedJoint\n " +
                 "Turn this to \"infinity\" to disable (Might cause jitter)\n" +
                "Ideal value depends on hand mass and velocity settings")]
        public float jointBreakTorque = 3000;

        [Space]
        
        [Header("Events")]
        public bool showEvents = true;

        [Space]
        [ShowIf("showEvents")]
        public UnityEvent onGrab;
        [ShowIf("showEvents")]
        public UnityEvent onRelease;

        [ShowIf("showEvents")]
        [Space, Space]
        public UnityEvent onSqueeze;
        [ShowIf("showEvents")]
        public UnityEvent onUnsqueeze;

        [Space, Space]
        [ShowIf("showEvents")]
        public UnityEvent onHighlight;
        [ShowIf("showEvents")]
        public UnityEvent onUnhighlight;
        [Space, Space]

        [Tooltip("Whether or not the break call made only when holding with multiple hands - if this is false the break event can be called by forcing an object into a static collider")]
        public bool pullApartBreakOnly = true;
        [ShowIf("showEvents")]
        public UnityEvent OnJointBreak;



        //For programmers <3
        public HandGrabEvent OnBeforeGrabEvent;
        public HandGrabEvent OnGrabEvent;
        public HandGrabEvent OnReleaseEvent;

        public HandGrabEvent OnForceReleaseEvent;
        public HandGrabEvent OnJointBreakEvent;

        public HandGrabEvent OnSqueezeEvent;
        public HandGrabEvent OnUnsqueezeEvent;

        public HandGrabEvent OnHighlightEvent;
        public HandGrabEvent OnUnhighlightEvent;


        protected bool beingHeld = false;

        protected List<Hand> heldBy;
        protected bool throwing;
        protected bool hightlighting;
        protected GameObject highlightObj;
        protected PlacePoint placePoint = null;
        protected PlacePoint lastPlacePoint = null;

        Transform originalParent;
        Vector3 lastCenterOfMassPos;
        Quaternion lastCenterOfMassRot;
        CollisionDetectionMode detectionMode;
        
        internal bool beingGrabbed = false;
        bool wasIsGrabbable = false;
        bool beingDestroyed = false;
        int originalLayer;
        Coroutine resetLayerRoutine;
        List<GrabbableChild> grabChildren;
        List<Transform> jointedParents;
        

        static Hand[] hands;
        static bool setSceneManagerLoad = false;

        protected void Awake() {
            OnAwake();
        }

        /// <summary>Virtual substitute for Awake()</summary>
        public virtual void OnAwake() {
            if(!setSceneManagerLoad)
                SceneManager.sceneLoaded += (scene, mode) => { hands = null; };
            setSceneManagerLoad = true;
            if(hands == null)
                hands = FindObjectsOfType<Hand>();

            //Delete these layer setters if you want to use your own custom layer set
            if(gameObject.layer == LayerMask.NameToLayer("Default"))
                gameObject.layer = LayerMask.NameToLayer(Hand.grabbableLayerNameDefault);
            else
                //Will set the layer to grabbable by default if the hands are set to the default layermask
                foreach(var hand in hands) {
                    if(hand.enabled && (gameObject.layer == LayerMask.NameToLayer("Default") || hand.grabLayers == LayerMask.GetMask(Hand.grabbableLayerNameDefault, Hand.grabbingLayerName, Hand.releasingLayerName) || hand.grabLayers == 0)){
                        gameObject.layer = LayerMask.NameToLayer(Hand.grabbableLayerNameDefault);
                    }
                }
            
            if(makeChildrenGrabbable)
                MakeChildrenGrabbable();


            grabChildren = new List<GrabbableChild>();
            if(heldBy == null)
                heldBy = new List<Hand>();

            if(body == null){
                if(GetComponent<Rigidbody>())
                    body = GetComponent<Rigidbody>();
                else
                    Debug.LogError("RIGIDBODY MISSING FROM GRABBABLE: " + transform.name + " \nPlease add/attach a rigidbody", this);
            }

            originalLayer = gameObject.layer;
            originalParent = body.transform.parent;
            jointedParents = new List<Transform>();
            for (int i = 0; i < jointedParents.Count; i++){
                jointedParents[i] = (jointedBodies[i].transform.parent);
            }
            detectionMode = body.collisionDetectionMode;
            body.maxDepenetrationVelocity /= 2f;
            
        }
        

        protected void FixedUpdate() {
            if(beingHeld) {
                lastCenterOfMassRot = body.transform.rotation;
                lastCenterOfMassPos = body.transform.position;
            }

            if(wasIsGrabbable && !isGrabbable) {
                ForceHandsRelease();
            }
            wasIsGrabbable = isGrabbable;
        }

        /// <summary>Called when the hand starts aiming at this item for pickup</summary>
        public virtual void Highlight(Hand hand, Material customMat = null) {
            if(!hightlighting){
                hightlighting = true;
                onHighlight?.Invoke();
                OnHighlightEvent?.Invoke(hand, this);
                var highlightMat = customMat != null ? customMat : hightlightMaterial;
                if(highlightMat != null){
                    if(!gameObject.CanGetComponent<MeshRenderer>(out _)) {
                        return;
                    }

                    //Creates a slightly larger copy of the mesh and sets its material to highlight material
                    highlightObj = new GameObject();
                    highlightObj.transform.parent = transform;
                    highlightObj.transform.localPosition = Vector3.zero;
                    highlightObj.transform.localRotation = Quaternion.identity;
                    highlightObj.transform.localScale = Vector3.one * 1.001f;

                    highlightObj.AddComponent<MeshFilter>().sharedMesh = GetComponent<MeshFilter>().sharedMesh;
                    highlightObj.AddComponent<MeshRenderer>().materials = new Material[GetComponent<MeshRenderer>().materials.Length];
                    var mats = new Material[GetComponent<MeshRenderer>().materials.Length];
                    for(int i = 0; i < mats.Length; i++) {
                        mats[i] = highlightMat;
                    }
                    highlightObj.GetComponent<MeshRenderer>().materials = mats;
                }
            }
        }
        
        /// <summary>Called when the hand stops aiming at this item</summary>
        public virtual void Unhighlight(Hand hand) {
            if(hightlighting){
                onUnhighlight?.Invoke();
                OnUnhighlightEvent?.Invoke(hand, this);
                hightlighting = false;
                if(highlightObj != null)
                    Destroy(highlightObj);
            }
        }



        /// <summary>Called by the hands Squeeze() function is called and this item is being held</summary>
        public virtual void OnSqueeze(Hand hand) {
            OnSqueezeEvent?.Invoke(hand, this);
            onSqueeze?.Invoke();
        }
        
        /// <summary>Called by the hands Unsqueeze() function is called and this item is being held</summary>
        public virtual void OnUnsqueeze(Hand hand) {
            OnUnsqueezeEvent?.Invoke(hand, this);
            onUnsqueeze?.Invoke();
        }

        public virtual void OnBeforeGrab(Hand hand) {
            OnBeforeGrabEvent?.Invoke(hand, this);
            beingGrabbed = true;
            if(resetLayerRoutine != null)
                StopCoroutine(resetLayerRoutine);

            if (parentOnGrab) {
                body.transform.parent = hand.transform.parent;

                foreach (var jointedBody in jointedBodies){
                    jointedBody.transform.parent = hand.transform.parent;
                }
            }
        }

        /// <summary>Called by the hand whenever this item is grabbed</summary>
        public virtual void OnGrab(Hand hand) {
            placePoint?.Remove(this);
            placePoint = null;
            if(lockHandOnGrab)
                hand.body.isKinematic = true;
            
            if(!body.isKinematic)
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            else
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            
            if(resetLayerRoutine != null)
                StopCoroutine(resetLayerRoutine);
            resetLayerRoutine = StartCoroutine(ResetLayer(0.1f, LayerMask.NameToLayer(Hand.grabbingLayerName)));


            heldBy?.Add(hand);
            throwing = false;
            beingHeld = true;
            beingGrabbed = false;
            onGrab?.Invoke();

            OnGrabEvent?.Invoke(hand, this);
        }
        
        /// <summary>Called by the hand whenever this item is release</summary>
        public virtual void OnRelease(Hand hand, bool thrown) {
            if(beingHeld) {
                if(lockHandOnGrab)
                    hand.body.isKinematic = false;

                if(!heldBy.Remove(hand))
                    return;


                if(heldBy.Count == 0){
                    beingHeld = false;
                    if(!beingDestroyed){
                        SetOriginalParent();
                        if (gameObject.layer == LayerMask.NameToLayer(Hand.releasingLayerName))
                            thrown = false;
                    }
                }

                SetLayerRecursive(transform, gameObject.layer, LayerMask.NameToLayer(Hand.releasingLayerName));
                if(resetLayerRoutine != null)
                    StopCoroutine(resetLayerRoutine);
                resetLayerRoutine = StartCoroutine(ResetLayer(ignoreReleaseTime, LayerMask.NameToLayer(Hand.releasingLayerName)));

                OnReleaseEvent?.Invoke(hand, this);
                onRelease?.Invoke();

                if(body != null) {
                    if(!beingHeld && thrown && !throwing) {
                        throwing = true;
                        body.velocity = hand.ThrowVelocity() * throwMultiplyer;
                        try {
                            body.angularVelocity = hand.ThrowAngularVelocity() * throwAngleMultiplyer;
                        }
                        catch { }
                    }
                    if(!thrown) {
                        body.velocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                }
                
                if(placePoint != null){
                    if(placePoint.CanPlace(transform)){
                        placePoint.Place(this);
                    }

                    if(placePoint.grabbableHighlight)
                        Unhighlight(hand);

                    placePoint.StopHighlight(this);
                }
            }
        }



        /// <summary>Tells each hand holding this object to release</summary>
        public virtual void HandRelease() {
            for(int i = heldBy.Count - 1; i >= 0; i--) {
                heldBy[i].ReleaseGrabLock();
            }
        }

        /// <summary>Forces all the hands on this object to relese without applying throw force or calling OnRelease event</summary>
        public virtual void ForceHandsRelease() {
            for(int i = heldBy.Count - 1; i >= 0; i--) {
                var hand = heldBy[i];
                hand.ForceReleaseGrab();
                if(beingHeld) {
                    if(lockHandOnGrab)
                        hand.body.isKinematic = false;
                    
                    
                    heldBy.Remove(hand);
                    if(heldBy.Count == 0){
                        beingHeld = false;
                        if(!beingDestroyed){
                            SetOriginalParent();
                        }

                        if(body != null) {
                            SetLayerRecursive(transform, gameObject.layer, LayerMask.NameToLayer(Hand.releasingLayerName));
                            if(resetLayerRoutine != null)
                                StopCoroutine(resetLayerRoutine);

                            if(!beingDestroyed)
                                resetLayerRoutine = StartCoroutine(ResetLayer(ignoreReleaseTime, LayerMask.NameToLayer(Hand.releasingLayerName)));
                        }
                    }
                    
                    beingGrabbed = false;
                    OnForceReleaseEvent?.Invoke(hand, this);
                }
            }
        }


        /// <summary>Forces all the hands on this object to relese without applying throw force or calling OnRelease event</summary>
        public virtual void ForceHandRelease(Hand hand) {
                hand.ForceReleaseGrab();

                if(lockHandOnGrab)
                    hand.GetComponent<Rigidbody>().isKinematic = false;
                    
                heldBy.Remove(hand);
                if(heldBy.Count == 0){
                    beingHeld = false;
                    if(!beingDestroyed){
                        SetOriginalParent();
                    }

                    if(body != null) {
                        SetLayerRecursive(transform, gameObject.layer, LayerMask.NameToLayer(Hand.releasingLayerName));
                        if(resetLayerRoutine != null)
                            StopCoroutine(resetLayerRoutine);

                        if(!beingDestroyed)
                            resetLayerRoutine = StartCoroutine(ResetLayer(ignoreReleaseTime, LayerMask.NameToLayer(Hand.releasingLayerName)));
                    }
                }
                OnForceReleaseEvent?.Invoke(hand, this);
        }



        private void OnDestroy() {
            beingDestroyed = true;
            ForceHandsRelease();
            MakeChildrenUngrabbable();
        }

        /// <summary>Called when the joint between the hand and this item is broken\n - Works to simulate pulling item apart event</summary>
        public virtual void OnHandJointBreak(Hand hand) {
            if(!pullApartBreakOnly || heldBy.Count > 1){
                OnJointBreakEvent?.Invoke(hand, this);
                OnJointBreak?.Invoke();
                body.WakeUp();
                body.velocity /= 1000;
                body.angularVelocity /= 1000;
            }
            ForceHandsRelease();
        }


        public void OnCollisionEnter(Collision collision) {
            if(throwing && (collision.gameObject.layer != (collision.gameObject.layer | (1 << Hand.GetHandsLayerMask())))) {
                Invoke("ResetThrowing", Time.fixedDeltaTime);
            }
        }
        
        public void OnTriggerEnter(Collider other) {
            PlacePoint otherPoint;
            if(other.CanGetComponent(out otherPoint)) {
                if(heldBy.Count == 0 && !otherPoint.onlyPlaceWhileHolding) return;
                if(otherPoint == null) return;

                if(placePoint != null && placePoint.GetPlacedObject() != null)
                    return;

                if(placePoint == null){
                    if(otherPoint.CanPlace(transform)){
                        placePoint = otherPoint;
                        if(placePoint.forcePlace){
                            if(lastPlacePoint == null || (lastPlacePoint != null && !lastPlacePoint.Equals(placePoint))){
                                ForceHandsRelease();
                                placePoint.Place(this);
                                lastPlacePoint = placePoint;
                            }
                        }
                        else{
                            placePoint.Highlight(this);
                            if(placePoint.grabbableHighlight)
                                Highlight(heldBy[0]);
                        }
                    }
                }
            }
        }
        
        public void OnTriggerExit(Collider other){
            PlacePoint otherPoint;
            if(!other.CanGetComponent(out otherPoint))
                return;

            if(placePoint != null && placePoint.Equals(otherPoint) && placePoint.Distance(transform) > 0.01f) {
                placePoint.StopHighlight(this);
                if(placePoint.grabbableHighlight)
                    Unhighlight(heldBy[0]);
                placePoint = null;
            }

            if(lastPlacePoint != null && lastPlacePoint.Equals(otherPoint) && Vector3.Distance(lastPlacePoint.transform.position, transform.position) > lastPlacePoint.placeRadius){
                lastPlacePoint = null;
            }
        }


        public bool IsThrowing(){
            return throwing;
        }

        public Vector3 GetVelocity(){
            return lastCenterOfMassPos - transform.position;
        }
        
        public Vector3 GetAngularVelocity(){
            Quaternion deltaRotation = transform.rotation * Quaternion.Inverse(lastCenterOfMassRot);
            deltaRotation.ToAngleAxis(out var angle, out var axis);
            angle *= Mathf.Deg2Rad;
            return (1.0f / Time.fixedDeltaTime) * angle/1.2f * axis ;
        }


        public List<Hand> GetHeldBy() {
            return heldBy;
        }

        public int HeldCount() {
            return heldBy.Count;
        }

        public bool IsHeld() {
            return beingHeld;
        }

        /// <summary>Returns true during hand grabbing coroutine</summary>
        public bool BeingGrabbed() {
            return beingGrabbed;
        }
        

        internal void SetPlacePoint(PlacePoint point) {
            placePoint = point;
        }

        public void SetGrabbableChild(GrabbableChild child) {
            if(!grabChildren.Contains(child))
                grabChildren.Add(child);
        }

        internal void SetLayerRecursive(Transform obj, int oldLayer, int newLayer) {
            for(int i = 0; i < grabChildren.Count; i++) {
                if(grabChildren[i].gameObject.layer == oldLayer)
                    grabChildren[i].gameObject.layer = newLayer;
            }
            SetChildrenLayers(obj);

            void SetChildrenLayers(Transform obj1){
                if(obj1.gameObject.layer == oldLayer)
                    obj1.gameObject.layer = newLayer;
                for(int i = 0; i < obj1.childCount; i++) {
                    SetChildrenLayers(obj1.GetChild(i));
                }
            }
        }
        
        internal void SetOriginalParent(){
            if(!body.CanGetComponent(out Stabbable stabbed1) || stabbed1.StabbedCount() == 0)
                body.transform.parent = originalParent;

            try {
                for (int i = 0; i < jointedBodies.Count; i++){
                    if(!jointedBodies[i].CanGetComponent(out Stabbable stabbed) || stabbed.StabbedCount() == 0) {
                        jointedBodies[i].transform.parent = jointedParents[i];
                    }
                }
            }
            catch { }
        }

        
        public void AddJointedBody(Rigidbody body){
            jointedBodies.Add(body);
            jointedParents.Add(body.transform.parent);
            if (!body.isKinematic && HeldCount() > 0){
                body.transform.parent = this.body.transform.parent;
            }
        }
        public void RemoveJointedBody(Rigidbody body){
            var i = jointedBodies.IndexOf(body);
            if (!body.CanGetComponent(out Grabbable grab) || grab.HeldCount() == 0)
                body.transform.parent = grab.originalParent;
            jointedBodies.RemoveAt(i);
            jointedParents.RemoveAt(i);
        }

        //Adds a reference script to child colliders so they can be grabbed
        void MakeChildrenGrabbable() {
            for(int i = 0; i < transform.childCount; i++) {
                AddChildGrabbableRecursive(transform.GetChild(i));
            }
        }

        void AddChildGrabbableRecursive(Transform obj) {
            if(obj.CanGetComponent(out Collider col) && col.isTrigger == false && !obj.CanGetComponent<Grabbable>(out _) && !obj.CanGetComponent<PlacePoint>(out _)){
                var child = obj.gameObject.AddComponent<GrabbableChild>();
                child.gameObject.layer = originalLayer;
                child.grabParent = this;
            }
            for(int i = 0; i < obj.childCount; i++){
                if(!obj.CanGetComponent<Grabbable>(out _))
                    AddChildGrabbableRecursive(obj.GetChild(i));
            }
        }

        //Adds a reference script to child colliders so they can be grabbed
        void MakeChildrenUngrabbable() {
            for(int i = 0; i < transform.childCount; i++) {
                RemoveChildGrabbableRecursive(transform.GetChild(i));
            }
        }

        void RemoveChildGrabbableRecursive(Transform obj) {
            if(obj.GetComponent<GrabbableChild>() && obj.GetComponent<GrabbableChild>().grabParent == this){
                Destroy(obj.gameObject.GetComponent<GrabbableChild>());
            }
            for(int i = 0; i < obj.childCount; i++){
                RemoveChildGrabbableRecursive(obj.GetChild(i));
            }
        }

        public int GetOriginalLayer() {
            return originalLayer;
        }

        public void DoDestroy() {
            Destroy(gameObject);
        }


        //Invoked one fixedupdate after impact
        protected void ResetThrowing() {
            throwing = false;
        }

        //Invoked a quatersecond after releasing
        protected IEnumerator ResetLayer(float delay, int fromLayer) {
            yield return new WaitForSeconds(delay);
            body.WakeUp();
            if(gameObject.layer == fromLayer){
                SetLayerRecursive(transform, fromLayer, originalLayer);
            }
            OriginalCollisionDetection();
            resetLayerRoutine = null;
        }
        
        //Resets to original collision dection
        protected void OriginalCollisionDetection() {
            if(body != null && gameObject.layer == originalLayer)
                body.collisionDetectionMode = detectionMode;
        }
    }
}
