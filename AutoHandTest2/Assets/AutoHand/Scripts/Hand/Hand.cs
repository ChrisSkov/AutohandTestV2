using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Autohand {
    public delegate void HandGrabEvent(Hand hand, Grabbable grabbable);
    public delegate void HandCollisionEvent(Hand hand, GameObject other);

    public enum HandType {
        both,
        right,
        left,
        none
    }

    [Serializable]
    public struct VelocityTimePair{
        public float time;
        public Vector3 velocity;
    }

    [RequireComponent(typeof(Rigidbody))]
    public class Hand : MonoBehaviour {

        [Space, Header("Fingers")]
        public Finger[] fingers;
        

        [Space, Header("Follow Settings")]

        [HideInInspector]
        public Transform follow;
        
        [Tooltip("Follow target, the hand will always try to match this transforms position with rigidbody movements")]
        public Transform followPosition;
        
        [Tooltip("Follow target, the hand will always try to match this transforms rotation with rigidbody movements")]
        public Transform followRotation;

        [Tooltip("Follow target speed (Can cause jittering if turned too high - recommend increasing drag with speed)"), Min(0)]
        public float followPositionStrength = 80;

        [Tooltip("Follow target rotation speed (Can cause jittering if turned too high - recommend increasing angular drag with speed)"), Min(0)]
        public float followRotationStrength = 100;

        [Tooltip("The maximum allowed velocity of the hand"), Min(0)]
        public float maxVelocity = 4;
        
        [Tooltip("Returns hand to the target after this distance"), Min(0)]
        public float maxFollowDistance = 0.75f;


        
        [Space, Header("Hand")]
        public bool left = false;

        [Tooltip("An empty GameObject that should be placed on the surface of the center of the palm")]
        public Transform palmTransform;

        [Tooltip("Maximum distance for pickup"), Min(0)]
        public float reachDistance = 0.3f;
        


        [Space, Header("Throwing")]
        [Tooltip("Amplifier for applied velocity on released object"), Min(0)]
        public float throwPower = 2f;

        [Tooltip("After this many seconds velocity data within a 'throw window' will be tossed out. (This allows you to get only use acceeleration data from the last 'x' seconds of the throw.)")]
        public float throwVelocityExpireTime = 0.2f;
        


        [Space, Header("Pose")]
        [Tooltip("Turn this on when you want to animate the hand or use other IK Drivers")]
        public bool disableIK = false;

        [Tooltip("How much the fingers sway from the velocity")]
        public float swayStrength = 0.7f;

        [Tooltip("This will offset each fingers bend (0 is no bend, 1 is full bend)")]
        public float gripOffset = 0.1f;

        
        [Tooltip("Makes grab smoother; also based on range and reach distance - a very near grab is instant and a max distance grab is [X] frames - RECOMMEND VALUES ~0.1"), Min(0)]
        public float grabTime = 0f;

        [Tooltip("The animation curve based on the grab time 0-1"), Min(0)]
        public AnimationCurve grabCurve;
        
        [Tooltip("Makes grab smoother; also based on range and reach distance - a very near grab is instant and a max distance grab is [X] frames - RECOMMEND VALUES ~0.1"), Min(0)]
        public float grabReturnTime = 0f;

        [Tooltip("This is used in conjunction with custom poses. For a custom pose to work it must has the same PoseIndex as the hand. Used for when your game has multiple hands")]
        public int poseIndex = 0;




        [Space, Header("Advanced")]
        [Tooltip("The layers to grab from --- NONE WILL DEFAULT THIS AND HIGHLIGHT TO GRABBABLE LAYERS [DELETE THIS IN START()]")]
        public LayerMask grabLayers;
        
        [Tooltip("The layers to highlight and use look assist on")]
        public LayerMask highlightLayers;

        [Tooltip("How quickly the hand looks towards it grab target")]
        public float lookAssistSpeed = 1;
        
        [Tooltip("Offsets the width of the grab area -- Turn this down or negative for less weird grabs but requires better aim to grab")]
        public float grabSpreadOffset = 0;

        [Tooltip("Increase for closer finger tip results / Decrease for less physics checks - The number of steps the fingers take when bending to grab something")]
        public int fingerBendSteps = 50;
        
        

#if UNITY_EDITOR
        [Header("Editor"), Space, Space]
        public bool showGizmos = false;
        [Tooltip("Turn this on to enable autograbbing for editor rigging")]
        public bool editorAutoGrab = false;
        bool editorSelected = false;
#endif
        

        bool freezePos = false;
        bool freezeRot = false;

        internal GameObject lookingAtObj = null;
        internal Grabbable holdingObj = null;

        float idealGrip = 1f;
        float currGrip = 1f;

        internal Transform moveTo;

        internal Transform heldMoveTo;
        internal WeightlessFollower heldFollower;

        internal Rigidbody body;
        protected FixedJoint joint1;
        protected FixedJoint joint2;
        protected float triggerPoint;
        protected Transform grabPoint;
        protected Vector3 startGrabPos;
        protected Quaternion startGrabRot;
        protected Vector3 palmOffset;
        protected float startDrag;

        protected Vector3[] handRays;
        protected RaycastHit[] rayHits;
        protected Quaternion rotationOffset = Quaternion.identity;
        protected internal Vector3 grabPositionOffset;
        protected internal Quaternion grabRotationOffset = Quaternion.identity;
        protected Vector3 lastMoveToPos; 
        protected Vector3 preRenderPos;
        protected Vector3 preGrabbableRenderPos;
        protected Quaternion preRenderRot;
        protected HandPoseData preRenderHandPose;


        internal int handLayers;
        protected int triggerCount = 0;

        protected bool grabbing = false;
        protected bool squeezing = false;
        protected bool grabLocked;
        protected bool grabbingFrame = false;
        protected bool preGrabbingFrame = false;
        protected bool prerenderedFrame;

        protected GrabbablePose grabPose;
        protected HandPoseData pose;

        protected Coroutine handAnimateRoutine;
        protected HandPoseArea handPoseArea;
        protected HandPoseData preHandPoseAreaPose;

        protected List<GameObject> collisions = new List<GameObject>();
        protected List<HandTriggerAreaEvents> triggerEventAreas;
        protected List<int> triggerAreasCount;
        

        ///<summary> A list of all acceleration values from the time the throwing motion was detected til now.</summary>
        protected List<VelocityTimePair> m_ThrowVelocityList = new List<VelocityTimePair>();
        protected List<VelocityTimePair> m_ThrowAngleVelocityList = new List<VelocityTimePair>();
        int tryMaxDistanceCount;


        //Adjust grabbable layers for custom setup.

        //The layer is used and applied to all grabbables in if the hands layermask is not set
        public const string grabbableLayerNameDefault = "Grabbable";

        //This helps the auto grab distinguish between what item is being grabbaed and the items around it
        public const string grabbingLayerName = "Grabbing";

        //This helps prevent conflict when releasing
        public const string releasingLayerName = "Releasing";

        //This was added by request just in case you want to add different layers for left/right hand
        public const string rightHandLayerName = "Hand";
        public const string leftHandLayerName = "Hand";
        public const string rightHandHoldingLayerName = "HandHolding";
        public const string leftHandHoldingLayerName = "HandHolding";




        ///Events for all my programmers out there :)
        public event HandGrabEvent OnTriggerGrab;
        public event HandGrabEvent OnBeforeGrabbed;
	    public event HandGrabEvent OnGrabbed;
        
	    public event HandGrabEvent OnBeforeReleased;
        public event HandGrabEvent OnReleased;
        
        public event HandGrabEvent OnForcedRelease;
        
        public event HandGrabEvent OnSqueezed;
        public event HandGrabEvent OnUnsqueezed;

        public event HandGrabEvent OnHighlight;
        public event HandGrabEvent OnStopHighlight;
        
        public event HandGrabEvent OnHeldConnectionBreak;
        
        public event HandCollisionEvent OnHandCollisionStart;
        public event HandCollisionEvent OnHandCollisionStop;
        public event HandCollisionEvent OnHandTriggerStart;
        public event HandCollisionEvent OnHandTriggerStop;
        


        //Additional Advanced Options
        public const bool usingPoseAreas = true;
        public const bool usingHighlight = true;
        public const bool useCustomGrabLayers = false;
        public const bool useCustomHighlightLayers = false;


        public void Start() {
            if(left && Time.fixedDeltaTime > 1/60f)
                Debug.LogError("Auto Hand: Strongly Recommended that Fixed Timestep is reduced to AT LEAST 1/60 for smoothness, (1/90 is best)  --- See [Project Settings/Time]");

            if(!useCustomGrabLayers)
                grabLayers = LayerMask.GetMask(grabbableLayerNameDefault, grabbingLayerName, releasingLayerName);

            if (!useCustomHighlightLayers)
                highlightLayers = LayerMask.GetMask(grabbableLayerNameDefault);
            
            //Sets hand to layer "Hand"
            SetLayerRecursive(transform, LayerMask.NameToLayer(left ? leftHandLayerName : rightHandLayerName));

            //preretrieve layermask
            handLayers = LayerMask.GetMask(leftHandHoldingLayerName, rightHandHoldingLayerName, rightHandLayerName, leftHandLayerName);


            body = GetComponent<Rigidbody>();
            startDrag = body.drag;
            body.useGravity = false;
            body.maxDepenetrationVelocity = 2f;

            moveTo = new GameObject().transform;
            moveTo.transform.parent = transform.parent;
            moveTo.name = "HAND FOLLOW POINT";

            collisions = new List<GameObject>();
            triggerEventAreas = new List<HandTriggerAreaEvents>();
            triggerAreasCount = new List<int>();

            foreach(var cam in FindObjectsOfType<Camera>()) {
                if(!cam.GetComponent<HandStabilizer>())
                    cam.gameObject.AddComponent<HandStabilizer>();
            }

            SetPalmRays();

#if UNITY_EDITOR
            if(Selection.activeGameObject == gameObject){
                Selection.activeGameObject = null;
                Debug.Log("Auto Hand: Selecting the hand can cause positional lag and quality reduction at runtime. Remove this code at any time.");
                editorSelected = true;
            }

            Application.quitting += () => { if(editorSelected) Selection.activeGameObject = gameObject; };
#endif
            if(!followPosition && follow)
                followPosition = follow;
            if(!followRotation && follow)
                followRotation = follow;

            body.WakeUp();
        }

        public virtual void SetPalmRays() {
            //This precalculates the rays so it has to do less math in realtime
            List<Vector3> rays = new List<Vector3>();
            for(int i = 0; i < 50; i++) {
                float ampI = Mathf.Pow(i, 1.3f + grabSpreadOffset) / (Mathf.PI * 0.8f);
                rays.Add(Quaternion.Euler(0, Mathf.Cos(i) * ampI + 90, Mathf.Sin(i) * ampI) * -Vector3.right);
            }
            rayHits = new RaycastHit[50];
            handRays = rays.ToArray();
        }


        private void OnDisable(){
            foreach (var trigger in triggerEventAreas){
                trigger.Exit(this);
            }
            triggerEventAreas.Clear();
            triggerAreasCount.Clear();
            collisions.Clear();
        }


        public void FixedUpdate(){
            if(grabbing || body.isKinematic)
                return;

            UpdateFingers();

            MoveTo();
            TorqueTo();
            
            //Also manages look assist
            UpdateHighlight();

            UpdateThrowing();
        }

        bool prerendered = false;
        //This is used to force the hand to always look like its where it should be even when physics is being weird
        public void OnPreRender() {
            //Hides fixed joint jitterings
            if(holdingObj != null && !grabbing) {
                preRenderPos = transform.position;
                preRenderRot = transform.rotation;
                transform.position = grabPoint.position;
                transform.rotation = grabPoint.rotation;
                prerendered = true;
            }
        }

        //This puts everything where it should be for the physics update
        public void OnPostRender() {
            //Returns position after hiding for camera
            if(holdingObj != null && !grabbing && prerendered) {
                transform.position = preRenderPos;
                transform.rotation = preRenderRot;
            }

            prerendered = false;
        }



        /// <summary>Helps keep track of hand collisions, used to help create extra stability</summary>
        protected void OnCollisionEnter(Collision collision) {
            OnHandCollisionStart?.Invoke(this, collision.collider.gameObject);
            if(!collisions.Contains(collision.collider.gameObject)){
                if(collision.collider.gameObject.CanGetComponent(out HandTouchEvent touchEvent)) {
                    touchEvent.Touch(this);
                }
            }
            collisions.Add(collision.collider.gameObject);
        }

        /// <summary>Helps keep track of hand collisions, used to help create extra stability</summary>
        protected void OnCollisionExit(Collision collision) {
            OnHandCollisionStop?.Invoke(this, collision.collider.gameObject);
            if(collisions.Contains(collision.collider.gameObject)){
                collisions.Remove(collision.collider.gameObject);
                if(!collisions.Contains(collision.collider.gameObject) && collision.collider.gameObject.CanGetComponent(out HandTouchEvent touchEvent)) {
                    touchEvent.Untouch(this);
                }
            }
        }
        
        protected void OnTriggerEnter(Collider other){
            OnHandTriggerStart?.Invoke(this, other.gameObject);
            CheckEnterPoseArea(other);

            HandTriggerAreaEvents area;
            if(other.CanGetComponent(out area)) {
                if(!triggerEventAreas.Contains(area)){
                    area.Enter(this);
                    triggerEventAreas.Add(area);
                    triggerAreasCount.Add(1);
                }
                else {
                    ++triggerAreasCount[triggerEventAreas.IndexOf(area)];
                }

            }
        }
        
        protected void OnTriggerExit(Collider other){
            OnHandTriggerStop?.Invoke(this, other.gameObject);
            CheckExitPoseArea(other);
            StartCoroutine(DelayTriggerExit(other));
        }

        IEnumerator DelayTriggerExit(Collider other) {
            yield return new WaitForFixedUpdate();
            HandTriggerAreaEvents area;

            if(other.CanGetComponent(out area)) {
                if(triggerEventAreas.Contains(area)){
                    var index = triggerEventAreas.IndexOf(area);
                    triggerAreasCount[index]--;
                    if(triggerAreasCount[index] == 0) {
                        triggerEventAreas.RemoveAt(index);
                        triggerAreasCount.RemoveAt(index);
                        area.Exit(this);
                    }
                }
            }
        }
        



        //========================================================
        //================== PHYSICS MOVEMENT  ===================
        

        /// <summary>Moves the hand to the controller position using physics movement</summary>
        internal virtual void MoveTo() {
            //Sets [Move To] Object
            moveTo.position = followPosition.position + grabPositionOffset;
            moveTo.rotation = followRotation.rotation * grabRotationOffset;

            //Adjust the [Move To] based on offsets 
            if (holdingObj != null) {
                if(left){
                    var leftRot = -holdingObj.heldRotationOffset;
                    leftRot.x *= -1;
                    moveTo.localRotation *= Quaternion.Euler(leftRot);
                    var moveLeft = holdingObj.heldPositionOffset;
                    moveLeft.x *= -1;
                    moveTo.position += transform.rotation*moveLeft;
                }
                else{
                    moveTo.position += transform.rotation*holdingObj.heldPositionOffset;
                    moveTo.localRotation *= Quaternion.Euler(holdingObj.heldRotationOffset);
                }
            }

            if (freezePos)
                return;

            //Strongly stabilizes one handed holding
            if(holdingObj != null && grabLocked && holdingObj.HeldCount() == 1)
                body.position = grabPoint.transform.position;

            if(followPositionStrength <= 0)
                return;

            var movePos = moveTo.position;
            var distance = Vector3.Distance(movePos, transform.position);

            var minDist = Time.fixedDeltaTime;
            if(distance <= minDist)
                body.drag = Mathf.Lerp(startDrag, startDrag*2f, 1-(distance/minDist));
            else
                body.drag = startDrag;

            //Returns if out of distance -> if you aren't holding anything
            if(distance > maxFollowDistance) {
                if(holdingObj != null){
                    if (!holdingObj.releaseOnTeleport && tryMaxDistanceCount < 3){
                        SetHandLocation(movePos, transform.rotation);
                        tryMaxDistanceCount += 2;
                    }
                    else if (tryMaxDistanceCount >= 3)
                        ForceReleaseGrab();
                    else
                        ForceReleaseGrab();
                }
                else{
                    body.position = movePos;
                }
            }

            if(tryMaxDistanceCount > 0)
                tryMaxDistanceCount--;

            var velocityClamp = maxVelocity;

            //This helps prevent that hand from forcing its way through walls
            if(distance > 0.1f && collisions.Count > 0)
                velocityClamp = 2f;
            
            //Sets velocity linearly based on distance from hand
            var vel = (movePos - transform.position).normalized * followPositionStrength * distance;
            vel.x = Mathf.Clamp(vel.x, -velocityClamp, velocityClamp);
            vel.y = Mathf.Clamp(vel.y, -velocityClamp, velocityClamp);
            vel.z = Mathf.Clamp(vel.z, -velocityClamp, velocityClamp);
            body.velocity = vel;
        }


        /// <summary>Rotates the hand to the controller rotation using physics movement</summary>
        internal virtual void TorqueTo() {
            if (freezeRot)
                return;

            if (holdingObj != null && grabLocked && holdingObj.HeldCount() == 1)
                body.rotation = grabPoint.transform.rotation;

            var toRot = rotationOffset * moveTo.rotation;
            float angleDist = Quaternion.Angle(body.rotation, toRot);
            Quaternion desiredRotation = Quaternion.Lerp(body.rotation, toRot, Mathf.Clamp(angleDist, 0, 2) / 4f);

            var kp = 90f * followRotationStrength;
            var kd = 60f;
            Vector3 x;
            float xMag;
            Quaternion q = desiredRotation * Quaternion.Inverse(body.rotation);
            q.ToAngleAxis(out xMag, out x);
            x.Normalize();
            x *= Mathf.Deg2Rad;
            Vector3 pidv = kp * x * xMag - kd * body.angularVelocity;
            Quaternion rotInertia2World = body.inertiaTensorRotation * body.rotation;
            pidv = Quaternion.Inverse(rotInertia2World) * pidv;
            pidv.Scale(body.inertiaTensor);
            pidv = rotInertia2World * pidv;
            body.AddTorque(pidv, ForceMode.Force);
        }
        
        
        internal virtual void UpdateThrowing(){
            if (holdingObj == null || grabbing) {
                if(m_ThrowVelocityList.Count > 0) {
                    m_ThrowVelocityList.Clear();
                    m_ThrowAngleVelocityList.Clear();
                }

                return;
            }
            
            // Add current hand velocity to throw velocity list.
            m_ThrowVelocityList.Add(new VelocityTimePair() { time = Time.time, velocity = body.velocity });

            // Remove old entries from m_ThrowVelocityList.
            for (int i = m_ThrowVelocityList.Count - 1; i >= 0; --i){
                if (Time.time - m_ThrowVelocityList[i].time >= throwVelocityExpireTime){
                    // Remove expired entry.
                    m_ThrowVelocityList.RemoveAt(i);
                }
            }
            
            // Add current hand velocity to throw velocity list.
            m_ThrowAngleVelocityList.Add(new VelocityTimePair() { time = Time.time, velocity = holdingObj.body.angularVelocity });

            // Remove old entries from m_ThrowVelocityList.
            for (int i = m_ThrowAngleVelocityList.Count - 1; i >= 0; --i){
                if (Time.time - m_ThrowAngleVelocityList[i].time >= throwVelocityExpireTime){
                    // Remove expired entry.
                    m_ThrowAngleVelocityList.RemoveAt(i);
                }
            }
        }




        //================================================================

        //================== CORE INTERACTION FUNCTIONS ===================
        
            
        /// <summary>Function for controller trigger fully pressed -> Grabs whatever is directly in front of and closest to the hands palm</summary>
        public virtual void Grab() {
            OnTriggerGrab?.Invoke(this, null);
            try {
                foreach(var triggerArea in triggerEventAreas) {
                    triggerArea.Grab(this);
                }
            }
            catch { }

            if(!grabbing && holdingObj == null) {
                RaycastHit closestHit;
                if(HandClosestHit(out closestHit, out Grabbable grabbable, reachDistance, ~handLayers) != Vector3.zero){
                    if(grabbable != null && gameObject.activeInHierarchy)
                        StartCoroutine(GrabObject(closestHit, grabbable));
                }
            }
            else if(holdingObj != null) {
                GrabLock grabLock;
                if(holdingObj.CanGetComponent(out grabLock)){
                    grabLock.OnGrabPressed?.Invoke();
                }
            }
        }
            
        public virtual void Grab(RaycastHit hit, Grabbable grab) {
            bool objectFree = grab.body.isKinematic != true && grab.body.constraints == RigidbodyConstraints.None;
            if(!grabbing && holdingObj == null && CanGrab(grab) && objectFree) {

                StartCoroutine(GrabObject(hit, grab, true));
            }
        }

        Coroutine tryGrab;
        public virtual void Grab(Grabbable grab){
            if (tryGrab != null)
                StopCoroutine(tryGrab);
            tryGrab = StartCoroutine(TryGrab());

            IEnumerator TryGrab(){
                for (int i = 0; i < 5; i++){
                    var distance = Vector3.Distance(palmTransform.position, grab.transform.position);
                    RaycastHit closestHit;
                    var mask = LayerMask.GetMask(Hand.grabbableLayerNameDefault, Hand.grabbingLayerName);
                    if(HandClosestHit(out closestHit, out _, distance, mask) != Vector3.zero){
                        StartCoroutine(GrabObject(closestHit, grab));
                        break;
                    }
                    else { 
                        RaycastHit[] hits = Physics.RaycastAll(palmTransform.position, grab.transform.position - palmTransform.position, distance * 2, mask);
                        if (hits.Length > 0){
                            foreach (var hit in hits){
                                if (hit.transform.gameObject == grab.gameObject){
                                    StartCoroutine(GrabObject(hit, grab));
                                    break;
                                }
                            }
                        }
                    }

                    yield return new WaitForSeconds(0.1f);
                }

                tryGrab = null;
            }
        }

        /// <summary>Takes a hit from a grabbable object and moves the hand towards that point, then calculates ideal hand shape</summary>
        protected virtual IEnumerator GrabObject(RaycastHit hit, Grabbable grab, bool distanceGrab = false) {
            
            //Checks if the grabbable script is enabled
            if(!CanGrab(grab))
                yield break;

            while(grab.beingGrabbed) {
                yield return new WaitForEndOfFrame();
            }
            
            //Hand Swap - One Handed Items
            if(grab.singleHandOnly && grab.HeldCount() > 0){
                grab.ForceHandsRelease();
                yield return new WaitForFixedUpdate();
                yield return new WaitForEndOfFrame();
            }

            grab.Unhighlight(this);

            CancelPose();
            ClearPoseArea();
            
            holdingObj = grab;
            startGrabPos = transform.position;
            startGrabRot = transform.rotation;
            
            holdingObj.OnBeforeGrab(this);
            OnBeforeGrabbed?.Invoke(this, holdingObj);
            var startHoldingObj = holdingObj;


            //Set layers for grabbing
            holdingObj.SetLayerRecursive(holdingObj.transform, holdingObj.gameObject.layer, LayerMask.NameToLayer(grabbingLayerName));
            
            //SETS GRAB POINT
            grabPoint = new GameObject().transform;
            grabPoint.name = "Grab Point (Generated)";
            grabPoint.position = hit.point;
            grabPoint.parent = hit.transform;

            grabbing = true;
            palmOffset = transform.position - palmTransform.position;
            moveTo.position = grabPoint.position + palmOffset;
            moveTo.rotation = grabPoint.rotation;
            freezeRot = true;

            

            var startGrabDist = Vector3.Distance(palmTransform.position, grabPoint.position)/reachDistance;
            

            var palmOriginParent = palmTransform.parent;
            var originParent = transform.parent;

            palmTransform.parent = null;
            transform.parent = palmTransform;
            palmTransform.LookAt(grabPoint.position, palmTransform.up);

            transform.parent = originParent;
            palmTransform.parent = palmOriginParent;

            HandPoseData startGrabPose;

            //If it's a predetermined Pose
            GrabbablePoseCombiner poseCombiner;
            GrabbablePose tempPose;
            if(holdingObj.CanGetComponent(out poseCombiner) && poseCombiner.CanSetPose(this))
            {
                grabPose = poseCombiner.GetClosestPose(this, holdingObj);
                startGrabPose = new HandPoseData(this, holdingObj);
            }
            else if (holdingObj.CanGetComponent(out tempPose) && tempPose.CanSetPose(this))
            {
                grabPose = tempPose;
                startGrabPose = new HandPoseData(this, holdingObj);
            }
            //Allign Position
            else{
                startGrabPose = new HandPoseData(this, grabPoint);
                AutoAlign();
            }




            
            //Aligns the hand using the closest hit point
            void AutoAlign() {
                foreach (var finger in fingers)
                    finger.ResetBend();

                palmOffset = transform.position - palmTransform.position;

                if ((holdingObj.instantGrab || distanceGrab) && holdingObj.HeldCount() == 0){
                    holdingObj.body.velocity = Vector3.zero;
                    holdingObj.body.angularVelocity = Vector3.zero;
                    holdingObj.transform.position += (transform.position - palmOffset) - grabPoint.position;
                    holdingObj.body.position += (transform.position - palmOffset) - grabPoint.position;
                    Physics.SyncTransforms();
                    body.WakeUp();
                    holdingObj.body.WakeUp();
                }
                else{
                    body.transform.position = grabPoint.position + palmOffset;
                    body.position = grabPoint.position + palmOffset;
                }


                //Finger Bend
                foreach(var finger in fingers)
                    finger.BendFingerUntilHit(fingerBendSteps, LayerMask.GetMask(grabbingLayerName));

            }


            //Smooth Grabbing
            if (grabTime > 0 && !(holdingObj.instantGrab || distanceGrab)){
                HandPoseData postGrabPose;
                Transform grabTarget;

                if(grabPose == null){
                    grabTarget = grabPoint;
                    postGrabPose = new HandPoseData(this, grabPoint);
                }
                else {
                    grabTarget = holdingObj.transform;
                    postGrabPose = grabPose.GetHandPoseData(this);
                }
                
                Vector3 endGrabPos = transform.position;
                //Lerp between start and end pose over time related to distance
                for(float i = 0; i < grabTime*(startGrabDist/reachDistance); i+=Time.fixedDeltaTime){
                    if(holdingObj != null){
                        var point = i/ grabTime * (startGrabDist / reachDistance);
                        HandPoseData.LerpPose(startGrabPose, postGrabPose, grabCurve.Evaluate(point)).SetPose(this, grabTarget);
                        yield return new WaitForFixedUpdate();
                    }
                }
                if (holdingObj != null)
                    postGrabPose.SetPose(this, grabTarget);
            }
            else if(grabPose != null)
                grabPose.GetHandPoseData(this).SetPose(this, holdingObj.transform);
                
            //Create Connection
            if(holdingObj == null){ 
                BreakGrabConnection();
                startHoldingObj.beingGrabbed = false;
                freezePos = false;
                freezeRot = false;
                grabbing = false;
                yield break;
            }

            if (holdingObj.maintainGrabOffset) {
                grabPositionOffset = transform.position - followPosition.transform.position;
                grabRotationOffset = Quaternion.Inverse(followRotation.transform.rotation) * transform.rotation;
            }

            //Connect Joints
            joint1 = gameObject.AddComponent<FixedJoint>();
            joint1.connectedBody = holdingObj.body;
                
            joint1.connectedMassScale = 1;
            joint1.massScale = 1;
            joint1.enableCollision = false;
            joint1.enablePreprocessing = false;
            joint1.breakForce = holdingObj.jointBreakForce;
            joint1.breakTorque = holdingObj.jointBreakForce;
                

            joint2 = holdingObj.body.gameObject.AddComponent<FixedJoint>();
            joint2.connectedBody = body;
                
            joint2.connectedMassScale = 1;
            joint2.massScale = 1;
            joint2.enableCollision = false;
            joint2.enablePreprocessing = false;
            joint2.breakForce = holdingObj.jointBreakForce;
            joint2.breakTorque = holdingObj.jointBreakForce;
                
            grabPoint.transform.position = transform.position;
            grabPoint.transform.rotation = transform.rotation;
            OnGrabbed?.Invoke(this, holdingObj);
            holdingObj.OnGrab(this);

             
            

            if(holdingObj.ignoreWeight) {
                if (heldMoveTo == null){
                    heldMoveTo = new GameObject().transform;
                    heldMoveTo.name = "HELD FOLLOW POINT";
                }

                moveTo.position = transform.position;
                moveTo.rotation = transform.rotation;
                heldMoveTo.position = holdingObj.transform.position;
                heldMoveTo.rotation = holdingObj.transform.rotation;
                heldMoveTo.parent = moveTo;
                moveTo.position = followPosition.position + grabPositionOffset;
                moveTo.rotation = followRotation.rotation * grabRotationOffset;

                heldMoveTo.parent = followPosition;

                if (!holdingObj.CanGetComponent(out heldFollower)){
                    heldFollower = holdingObj.gameObject.AddComponent<WeightlessFollower>();
                }

                heldFollower.Set(this, holdingObj);
            }

            moveTo.position = followPosition.position + grabPositionOffset;
            moveTo.rotation = followRotation.rotation * grabRotationOffset;

            grabLocked = true;
            bool objectFree = holdingObj.body.isKinematic != true && holdingObj.body.constraints == RigidbodyConstraints.None && !holdingObj.releaseOnTeleport && holdingObj.parentOnGrab;

            //Returns hand based on grab return time
            if (objectFree && grabReturnTime > 0 && holdingObj.HeldCount() == 1 && !(holdingObj.instantGrab || distanceGrab)) {
                for(float i = 0; i < grabReturnTime*startGrabDist; i += Time.fixedDeltaTime){
                    yield return new WaitForFixedUpdate();
                    if (holdingObj != null)
                        body.position = Vector3.MoveTowards(body.position, moveTo.position, grabTime * (startGrabDist / reachDistance));
                    else
                        break;

                }
            }
            

            if(holdingObj == null){ 
                BreakGrabConnection();
                startHoldingObj.beingGrabbed = false;
                freezePos = false;
                freezeRot = false;
                grabbing = false;
                yield break;
            }


            //Reset Values
            freezePos = false;
            freezeRot = false;
            grabbing = false;
        }
        

        /// <summary>Function for controller trigger unpressed</summary>
        public virtual void Release() {
            foreach(var triggerArea in triggerEventAreas) {
                triggerArea.Release(this);
            }

            //Do the holding object calls and sets
            if(holdingObj != null) {
                if(holdingObj.CanGetComponent<GrabLock>(out _))
                    return;

                lookingAtObj = null;
                
                if(holdingObj.ignoreWeight)
                    Destroy(GetComponent<PhysicsFollower>());

                OnBeforeReleased?.Invoke(this, holdingObj);

                if(squeezing)
                    holdingObj?.OnUnsqueeze(this);

                holdingObj?.OnRelease(this, holdingObj.gameObject.layer != LayerMask.NameToLayer(grabbingLayerName));


                OnReleased?.Invoke(this, holdingObj);
                BreakGrabConnection();
            }
            else if(grabLocked || holdingObj == null) {
                BreakGrabConnection();
            }
        }
        

        /// <summary>Function for controller trigger unpressed</summary>
        public virtual void ReleaseGrabLock() {
            //Do the holding object calls and sets
            if(holdingObj != null) {
                OnBeforeReleased?.Invoke(this, holdingObj);

                if(squeezing)
                    holdingObj?.OnUnsqueeze(this);

                holdingObj?.OnRelease(this, true);

                OnReleased?.Invoke(this, holdingObj);
                BreakGrabConnection();
            }
            else if(grabLocked || holdingObj == null) {
                BreakGrabConnection();
            }
        }

        
        /// <summary>This will force release the hand without throwing or calling OnRelease\n like losing grip on something instead of throwing</summary>
        public virtual void ForceReleaseGrab() {
            //Do the holding object calls and sets
            if(holdingObj != null) {
                if(squeezing)
                    holdingObj.OnUnsqueeze(this);
                OnForcedRelease?.Invoke(this, holdingObj);
                holdingObj.body.WakeUp();
                holdingObj.OnForceReleaseEvent?.Invoke(this, holdingObj);
                var releaseObject = holdingObj;
                BreakGrabConnection();
                //Call this last do it doesn't loop
                releaseObject.ForceHandRelease(this);
            }
        }

        
        /// <summary>Event for controller grip</summary>
        public virtual void Squeeze() {
            OnSqueezed?.Invoke(this, holdingObj);
            holdingObj?.OnSqueeze(this);

            foreach(var triggerArea in triggerEventAreas) {
                triggerArea.Squeeze(this);
            }
            squeezing = true;
        }

        /// <summary>Event for controller ungrip</summary>
        public virtual void Unsqueeze() {
            squeezing = false;
            OnUnsqueezed?.Invoke(this, holdingObj);
            holdingObj?.OnUnsqueeze(this);

            foreach(var triggerArea in triggerEventAreas) {
                triggerArea.Unsqueeze(this);
            }
        }

        /// <summary>Returns true if squeezing has been triggered</summary>
        public bool IsSqueezing() {
            return squeezing;
        }
        

        /// <summary>This is used to simulate and trigger pull-apart effect</summary>
        protected virtual void OnJointBreak(float breakForce) {
            if(joint1 != null)
                Destroy(joint1);
            if(joint2 != null)
                Destroy(joint2);
            holdingObj?.OnHandJointBreak(this);
            ForceReleaseGrab();
        }
        

        /// <summary>Breaks the grab event</summary>
        public virtual void BreakGrabConnection(bool callEvent = true){
            if(grabbing && holdingObj != null){
                holdingObj.body.velocity = Vector3.zero;
                holdingObj.body.angularVelocity = Vector3.zero;
                holdingObj.SetOriginalParent();
            }
            grabLocked = false;
            grabPose = null;
            grabPositionOffset = Vector3.zero;
            grabRotationOffset = Quaternion.identity;

            if(heldMoveTo != null)
                heldFollower?.RemoveFollow(heldMoveTo);

            //Destroy Junk
            if(grabPoint != null)
                Destroy(grabPoint.gameObject);
            if(joint1 != null)
                Destroy(joint1);
            if(joint2 != null)
                Destroy(joint2);

            if (callEvent)
                OnHeldConnectionBreak?.Invoke(this, holdingObj);
            holdingObj = null;
        }
        

        /// <summary>Creates the grab connection</summary>
        public virtual void CreateGrabConnection(Grabbable grab, Vector3 handPos, Quaternion handRot, Vector3 grabPos, Quaternion grabRot, bool executeGrabEvents = false) {
            if(grabPoint == null){
                grabPoint = new GameObject().transform;
                grabPoint.parent = grab.transform;
                grabPoint.name = "GRAB POINT";
            }

            if(executeGrabEvents) {
                OnBeforeGrabbed?.Invoke(this, grab);
                grab.OnBeforeGrab(this);
            }

            transform.position = handPos;
            body.position = handPos;
            transform.rotation = handRot;
            body.rotation = handRot;
            grab.transform.position = grabPos;
            grab.body.position = grabPos;
            grab.transform.rotation = grabRot;
            grab.body.rotation = grabRot;

            if (holdingObj.maintainGrabOffset){
                grabPositionOffset = transform.position - followPosition.transform.position;
                grabRotationOffset = Quaternion.Inverse(followRotation.transform.rotation) * transform.rotation;
            }

            //Connect Joints
            joint1 = gameObject.AddComponent<FixedJoint>();
            joint1.connectedBody = grab.body;
            joint1.breakForce = float.PositiveInfinity;
            joint1.breakTorque = float.PositiveInfinity;
                
            joint1.connectedMassScale = 1;
            joint1.massScale = 1;
            joint1.enableCollision = false;
            joint1.enablePreprocessing = false;
                
            joint2 = grab.body.gameObject.AddComponent<FixedJoint>();
            joint2.connectedBody = body;
            joint2.breakForce = float.PositiveInfinity;
            joint2.breakTorque = float.PositiveInfinity;
                
            joint2.connectedMassScale = 1;
            joint2.massScale = 1;
            joint2.enableCollision = false;
            joint2.enablePreprocessing = false;

            grabPoint.transform.position = handPos;
            grabPoint.transform.rotation = handRot;

            holdingObj = grab;

            //If it's a predetermined Pose
            GrabbablePoseCombiner poseCombiner = null;
            GrabbablePose tempPose = null;
            if(holdingObj.CanGetComponent(out poseCombiner)){
                if(poseCombiner.CanSetPose(this)) {
                    grabPose = poseCombiner.GetClosestPose(this, holdingObj);
                    grabPose.GetHandPoseData(this).SetPose(this, holdingObj.transform);
                }
            }
            else if (holdingObj.CanGetComponent(out tempPose)){
                if(tempPose.CanSetPose(this)){
                    grabPose = tempPose;
                    grabPose.GetHandPoseData(this).SetPose(this, holdingObj.transform);
                }
            }
            
            if(executeGrabEvents) {
                OnGrabbed?.Invoke(this, holdingObj);
                holdingObj.OnGrab(this);
            }
           
        }
        


        


        //=============================================================
        //=============== HIGHLIGHT AND LOOK ASSIST ===================

        
        /// <summary>Manages the highlighting for grabbables</summary>
        protected virtual void UpdateHighlight() {
            if(usingHighlight && highlightLayers != 0 && holdingObj == null){
                Grabbable lookingAtGrab;
                RaycastHit hit;
                var dir = HandClosestHit(out hit, out var grabbable, reachDistance, ~handLayers);
                //Zero means it didn't hit
                if(dir != Vector3.zero){
                    //Changes look target
                    if(hit.collider.transform.gameObject != lookingAtObj){
                        //Unhighlights current target if found
                        if(lookingAtObj != null && HasGrabbable(lookingAtObj, out lookingAtGrab)){
                            OnStopHighlight?.Invoke(this, lookingAtGrab);
                            lookingAtGrab.Unhighlight(this);
                        }

                        //Highlights new target if found
                        lookingAtObj = hit.collider.transform.gameObject;
                        if(HasGrabbable(lookingAtObj, out lookingAtGrab)){
                            OnHighlight?.Invoke(this, lookingAtGrab);
                            lookingAtGrab.Highlight(this);
                        }
                    }

                    rotationOffset = Quaternion.RotateTowards(rotationOffset, Quaternion.FromToRotation(palmTransform.forward, hit.point - transform.position), 50f * Time.deltaTime * lookAssistSpeed * grabbable.lookAssistMultiplyer);
                }
                //If it was looking at something but now it's not there anymore
                else if(lookingAtObj != null){
                    //Just in case the object your hand is looking at is destroyed
                    if(HasGrabbable(lookingAtObj, out lookingAtGrab)){
                        OnStopHighlight?.Invoke(this, lookingAtGrab);
                        lookingAtGrab.Unhighlight(this);
                    }

                    rotationOffset = Quaternion.identity;
                    lookingAtObj = null;
                }
                else
                    rotationOffset = Quaternion.identity;
            }
            else {
                UpdateLookAssist();
            }
        }


        /// <summary>Rotates the hand towards the object it's aiming to pick up</summary>
        protected virtual void UpdateLookAssist(){
            if(holdingObj == null && lookAssistSpeed > 0){
                RaycastHit hit;
                var controllerRot = left ? followRotation.localRotation*Quaternion.Euler(0, 90, 0) : followRotation.localRotation*Quaternion.Euler(0, -90, 0);
                var dir = HandClosestHit(followPosition, controllerRot, out hit, out var grabbable, reachDistance, ~handLayers);

                //Zero means it didn't hit
                if(dir != Vector3.zero){
                    //Hiding look assist, will probably be returned in a different form with less jitters
                    rotationOffset = Quaternion.RotateTowards(rotationOffset, Quaternion.FromToRotation(palmTransform.forward, hit.point - transform.position), 50f * Time.deltaTime * lookAssistSpeed * grabbable.lookAssistMultiplyer);
                }
                //If you're seeing nothing reset offset
                else{
                    rotationOffset = Quaternion.identity;
                }
            }
            //If you're holding something reset offset
            else{
                rotationOffset = Quaternion.identity;
            }
        }


        

        //=================================================================

        //========================= GET AND LOAD POSES ======================
        // How to save and load a pose -> GetHeldPose() -> Save the HandPoseData -> SetHandPose(poseData)


        /// <summary>Sets the hand pose and connects the grabbable</summary>
        public virtual void SetHeldPose(HandPoseData pose, Grabbable grabbable) {
            holdingObj = grabbable;
            OnBeforeGrabbed?.Invoke(this, holdingObj);

            holdingObj.transform.position = transform.position;

            //Set Pose
            pose.SetPose(this, grabbable.transform);

            //Connect Joints
            joint1 = gameObject.AddComponent<FixedJoint>();
            joint1.connectedBody = holdingObj.body;
            joint1.breakForce = holdingObj.jointBreakForce;
            joint1.breakTorque = holdingObj.jointBreakForce;
                
            joint1.connectedMassScale = 1;
            joint1.massScale = 1;
            joint1.enableCollision = false;
            joint1.enablePreprocessing = false;
                
            joint2 = holdingObj.body.gameObject.AddComponent<FixedJoint>();
            joint2.connectedBody = body;
            joint2.breakForce = holdingObj.jointBreakForce;
            joint2.breakTorque = holdingObj.jointBreakForce;
                
            joint2.connectedMassScale = 1;
            joint2.massScale = 1;
            joint2.enableCollision = false;
            joint2.enablePreprocessing = false;
                
            grabPoint = new GameObject().transform;
            grabPoint.name = "Grab Point (Generated)";
            grabPoint.parent = holdingObj.transform;
            grabPoint.transform.position = transform.position;
            grabPoint.transform.rotation = transform.rotation;
                
            OnGrabbed?.Invoke(this, holdingObj);
            holdingObj.OnGrab(this);

            SetHandLocation(moveTo.position, moveTo.rotation);

            grabLocked = true;
            
        }
        
        /// <summary>Sets the hand pose</summary>
        public void SetHandPose(HandPoseData pose) {
            pose.SetPose(this, null);
        }

        /// <summary>(IF SAVING A HELD POSE USE GetHeldPose()) Returns the current hand pose, ignoring what is being held</summary>
        public HandPoseData GetHandPose(){
            return new HandPoseData(this);
        }
        
        /// <summary>Returns the hand pose relative to what it's holding</summary>
        public HandPoseData GetHeldPose() {
            if(holdingObj)
                return new HandPoseData(this, holdingObj);
            return new HandPoseData(this);
        }
        
        /// <summary>Takes a new pose and an amount of time and poses the hand</summary>
        public virtual void UpdatePose(HandPoseData pose, float time){
            if (handAnimateRoutine != null)
                StopCoroutine(handAnimateRoutine);
            handAnimateRoutine = StartCoroutine(LerpHandPose(GetHandPose(), pose, time));
        }

        /// <summary>Ensures any pose being made is canceled</summary>
        protected void CancelPose() {
            if (handAnimateRoutine != null)
                StopCoroutine(handAnimateRoutine);
            handAnimateRoutine = null;
        }

        /// <summary>Not exactly lerped, uses non-linear sqrt function because it looked better -- planning animation curves options soon</summary>
        protected virtual IEnumerator LerpHandPose(HandPoseData fromPose, HandPoseData toPose, float totalTime){
            float timePassed = 0;
            while(timePassed < totalTime){
                SetHandPose(HandPoseData.LerpPose(fromPose, toPose, Mathf.Pow(timePassed/totalTime, 0.5f)));
                yield return new WaitForEndOfFrame();
                timePassed += Time.deltaTime;
            }
            SetHandPose(HandPoseData.LerpPose(fromPose, toPose, 1));
            handAnimateRoutine = null;
        }
       
        
        public bool UsingPoseArea() {
            return handPoseArea != null;
        }

        
        /// <summary>Checks and manages if any of the hands colliders enter a pose area</summary>
        protected virtual void CheckEnterPoseArea(Collider other) {
            if(holdingObj || !usingPoseAreas || !other.gameObject.activeInHierarchy)
                return;

            HandPoseArea tempPose;
            if(other.CanGetComponent(out tempPose)){
                for (int i = 0; i < tempPose.poseAreas.Length; i++) {
                    if(tempPose.poseIndex == poseIndex) {
                        if(handPoseArea == null)
                            preHandPoseAreaPose = GetHandPose();

                        if (tempPose.HasPose(left) && (handPoseArea == null || handPoseArea != tempPose)){
                            preHandPoseAreaPose = GetHandPose();
                            triggerCount = 0;
                            handPoseArea = tempPose;
                            if(holdingObj == null)
                                UpdatePose(handPoseArea.GetHandPoseData(left), handPoseArea.transitionTime);
                        }

                        if (tempPose.Equals(handPoseArea))
                            triggerCount++;

                        break;
                    }
                    
                }
            }
        }
        
        /// <summary>Checks if manages any of the hands colliders exit a pose area</summary>
        protected virtual void CheckExitPoseArea(Collider other) {
            if(!usingPoseAreas || !other.gameObject.activeInHierarchy)
                return;

            if(handPoseArea != null && handPoseArea.gameObject.Equals(other.gameObject)){
                triggerCount--;
                if (triggerCount == 0 && holdingObj == null){
                    if (holdingObj == null)
                        UpdatePose(preHandPoseAreaPose, handPoseArea.transitionTime);
                    handPoseArea = null;
                }
                else if(triggerCount == 0 && holdingObj != null)
                    handPoseArea = null;
            }
        }

        protected void ClearPoseArea() {
            triggerCount = 0;
            handPoseArea = null;
        }
        

        /// <summary>Sets the hands grip 0 is open 1 is closed</summary>
        public void SetGrip(float grip) {
            triggerPoint = grip;
        }


        /// <summary>Determines how the hand should look/move based on its flags</summary>
        protected virtual void UpdateFingers(){
            if(!grabbing && !squeezing && holdingObj == null){
                idealGrip = triggerPoint;
            }

            //Responsable for movement finger sway
            if(!holdingObj && !disableIK && !grabPose && handPoseArea == null && handAnimateRoutine == null) {
                float vel = -palmTransform.InverseTransformDirection(body.velocity).z;
                float grip = idealGrip + gripOffset + swayStrength * (vel / 8f);

                bool less = (currGrip < grip) ? true : false;
                currGrip += ((currGrip < grip) ? Time.deltaTime : -Time.deltaTime) * (Mathf.Abs(currGrip - grip) * 25);
                if(less && currGrip > grip)
                    currGrip = grip;

                else if(!less && currGrip < grip)
                    currGrip = grip;

                foreach(var finger in fingers){
                    finger.UpdateFinger(currGrip);
                }
            }
        }


        

        //=================================================================
        //========================= HELPER FUNCTIONS ======================

        
        /// <summary>Returns the current held object - null if empty</summary>
        public Grabbable GetHeldGrabbable() {
            return holdingObj;
        }

        ///<summary>Moves the hand and whatever it might be holding (if teleport allowed) to given pos/rot</summary>
        internal void SetHandLocation(Vector3 pos, Quaternion rot) {
            if(holdingObj != null && !holdingObj.parentOnGrab)
                return;

            if (holdingObj != null && !holdingObj.releaseOnTeleport){
                var deltaPos = pos - body.position;
                var deltaRot = rot * Quaternion.Inverse(body.rotation);

                body.position += deltaPos;
                body.rotation = rot;
                
                grabPositionOffset = deltaRot*grabPositionOffset;

                holdingObj.body.position += deltaPos;
                foreach (var jointed in holdingObj.jointedBodies){
                    if(!(jointed.CanGetComponent(out Grabbable grab) && grab.HeldCount() > 0)){
                        jointed.position += deltaPos;
                    }
                }
            }
            else{
                transform.position = pos;
                transform.rotation = rot;
                body.position = pos;
                body.rotation = rot;
            }
        }

        /// <summary>Default, uses palm forward, finds the closest raycast from a cone of rays -> Returns average direction of all hits</summary>
        public virtual Vector3 HandClosestHit(out RaycastHit closestHit, out Grabbable grab, float dist, int layerMask, Grabbable grabTarget = null) {
            return HandClosestHit(palmTransform, Quaternion.identity, out closestHit, out grab, dist, layerMask, grabTarget);
        }

        /// <summary>Finds the closest raycast from a cone of rays -> Returns average direction of all hits</summary>
        public virtual Vector3 HandClosestHit(Transform hand, Quaternion offset, out RaycastHit closestHit, out Grabbable grabbable, float dist, int layerMask, Grabbable grabTarget = null) {
            Grabbable grab;
            List<RaycastHit> hits = new List<RaycastHit>();
            List<Grabbable> grabs = new List<Grabbable>();
            for (int i = 0; i < handRays.Length; i++){
                if (Physics.SphereCast(palmTransform.position, 0.035f, offset * hand.rotation * handRays[i], out rayHits[i], dist, layerMask)
                    && (grabTarget == null || grabTarget.gameObject == rayHits[i].transform.gameObject)
                        && HasGrabbable(rayHits[i].transform.gameObject, out grab)){
                    grabs.Add(grab);
                    hits.Add(rayHits[i]);
                }
            }
            if(hits.Count > 0) {
                closestHit = hits[0];
                grabbable = grabs[0];
                var closestHitIndex = 0;
                //The minmax stuff helps the hand priorites the middle points over the outer points
                var minMax = new Vector2(1f, 1.05f);
                Vector3 dir = Vector3.zero;
                for(int i = 0; i < hits.Count; i++) {
                    var closestMulti = Mathf.Lerp(minMax.x, minMax.y, ((float)closestHitIndex)/hits.Count);
                    var multi = Mathf.Lerp(minMax.x, minMax.y, ((float)i)/hits.Count);
                    multi *= grabs[i].grabDistancePriority;
                    if(hits[i].distance*multi < closestHit.distance*closestMulti){
                        closestHit = hits[i];
                        grabbable = grabs[i];
                        closestHitIndex = i;
                    }
                    dir += hits[i].point - hand.position;
                }

                return dir/hits.Count;
            }

            closestHit = new RaycastHit();
            grabbable = null;
            return Vector3.zero;
        }
        
        /// <summary>Returns the hands velocity times its strength</summary>
        internal virtual Vector3 ThrowVelocity(){
            if (grabbing)
                return Vector3.zero;

            // Calculate the average hand velocity over the course of the throw.
            Vector3 averageVelocity = Vector3.zero;
            if (m_ThrowVelocityList.Count > 0){
                foreach (VelocityTimePair pair in m_ThrowVelocityList){
                    averageVelocity += pair.velocity;
                }
                averageVelocity /= m_ThrowVelocityList.Count;
            }
            else { averageVelocity = body.velocity; }

            return averageVelocity * throwPower;
        }

        /// <summary>Returns the hands velocity times its strength</summary>
        internal virtual Vector3 ThrowAngularVelocity(){
            if (grabbing)
                return Vector3.zero;

            // Calculate the average hand velocity over the course of the throw.
            Vector3 averageVelocity = Vector3.zero;
            if (m_ThrowAngleVelocityList.Count > 0){
                foreach (VelocityTimePair pair in m_ThrowAngleVelocityList)
                {
                    averageVelocity += pair.velocity;
                }
                averageVelocity /= m_ThrowAngleVelocityList.Count;
            }

            return averageVelocity;
        }

        /// <summary>Whether or not this hand can grab the grabbbale based on hand and grabbable settings</summary>
        public bool CanGrab(Grabbable grab) {
            var cantHandSwap = (grab.IsHeld() && grab.singleHandOnly && !grab.allowHeldSwapping);
            return !(cantHandSwap || !grab.enabled || !grab.isGrabbable || grab.handType == HandType.none || (grab.handType == HandType.left && !left) || (grab.handType == HandType.right && left));
        }
        
        /// <summary>Returns true if there is a grabbable or link, out null if there is none</summary>
        protected bool HasGrabbable(GameObject obj, out Grabbable grabbable) {
            if(obj == null){
                grabbable = null;
                return false;
            }

            if(obj.CanGetComponent(out grabbable)){
                return true;
            }

            GrabbableChild grabChild;
            if(obj.CanGetComponent(out grabChild)){
                grabbable = grabChild.grabParent;
                return true;
            }

            grabbable = null;
            return false;
        }
        
        
        public static void SetLayerRecursive(Transform obj, int fromLayer, int toLayer) {
            if(obj.gameObject.layer == fromLayer) {
                obj.gameObject.layer = toLayer;
            }
            for(int i = 0; i < obj.childCount; i++) {
                SetLayerRecursive(obj.GetChild(i), toLayer, fromLayer);
            }
        }

        public static void SetLayerRecursive(Transform obj, int newLayer) {
            obj.gameObject.layer = newLayer;
            for(int i = 0; i < obj.childCount; i++) {
                SetLayerRecursive(obj.GetChild(i), newLayer);
            }
        }

        public int CollisionCount() {
            return collisions.Count;
        }
        
        /// <summary>Returns true during the grabbing frames</summary>
        public bool IsGrabbing() {
            return grabbing;
        }
        


        //=================================================================

        //========================= EDITOR FUNCTIONS ======================


        [ContextMenu("Relaxed Hand")]
        public void RelaxHand() {
            foreach(var finger in fingers)
                finger.SetFingerBend(gripOffset);
        }

        [ContextMenu("Opened Hand")]
        public void OpenHand() {
            foreach(var finger in fingers)
                finger.SetFingerBend(0);
        }
        
        [ContextMenu("Closed Hand")]
        public void CloseHand() {
            foreach(var finger in fingers)
                finger.SetFingerBend(1);
        }

        [ContextMenu("Bend Fingers Until Hit")]
        public void ProceduralFingerBend() {
            ProceduralFingerBend(~LayerMask.GetMask(leftHandHoldingLayerName, rightHandHoldingLayerName, rightHandLayerName, leftHandLayerName));
        }
        
        /// <summary>Bends each finger until they hit</summary>
        public void ProceduralFingerBend(int layermask){
            foreach(var finger in fingers){
                finger.BendFingerUntilHit(fingerBendSteps, layermask);
            }
        }

        /// <summary>Bends each finger until they hit</summary>
        public void ProceduralFingerBend(RaycastHit hit){
            foreach(var finger in fingers){
                finger.BendFingerUntilHit(fingerBendSteps, hit);
            }
        }
        
        public static int GetHandsLayerMask() {
            return LayerMask.GetMask(leftHandHoldingLayerName, rightHandHoldingLayerName, rightHandLayerName, leftHandLayerName);
        }


#if UNITY_EDITOR
        float lastGrabSpreadOffset = 0;
        private void OnDrawGizmos() {
            if(palmTransform == null)
                return;


            if(showGizmos) {
                Gizmos.DrawLine(transform.position, (transform.position+transform.rotation*palmOffset));
                if(handRays == null || handRays.Length == 0 || lastGrabSpreadOffset != grabSpreadOffset) {
                    Vector3 handDir = -Vector3.right;
                    for(int i = 0; i < 50; i++) {
                        float ampI = Mathf.Pow(i, 1.3f + grabSpreadOffset) / (Mathf.PI * 0.8f);
                        Gizmos.DrawRay(palmTransform.position, palmTransform.rotation * Quaternion.Euler(0, Mathf.Cos(i) * ampI + 90, Mathf.Sin(i) * ampI) * handDir * reachDistance);
                    }
                }
                else {
                    foreach(var ray in handRays) {
                        Gizmos.DrawRay(palmTransform.position, palmTransform.rotation * ray * reachDistance);
                    }
                }
            }
        }
        

        private void OnDrawGizmosSelected() {
            if(palmTransform == null)
                return;
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(palmTransform.position, palmTransform.forward*reachDistance);
            
        }
#endif
    }
}
