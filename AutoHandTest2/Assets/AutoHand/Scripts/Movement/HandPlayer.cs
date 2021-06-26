using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Autohand.Demo;
using System;
using NaughtyAttributes;

namespace Autohand{
    [RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(CapsuleCollider)), DefaultExecutionOrder(-1)]
    public class HandPlayer : MonoBehaviour{

        [Header("Body Parts")]
        [Tooltip("The tracked headCamera object")]
        public Camera headCamera;
        [Tooltip("The object that represents the forward direction movement, usually should be set as the camera or a tracked controller")]
        public Transform forwardFollow;
        [Tooltip("This should be a GameObject that contains all the tracked objects (head/controllers)")]
        public Transform trackingContainer;
        public Hand handRight;
        public Hand handLeft;


        [Space, Header("Body Settings")]
        public float heightOffset = 0;
        [Tooltip("Maximum distance that the head is allowed to be from the body before being stopped")]
        public float maxHeadDistance = 0.5f;
        [Tooltip("Whether or not the capsule height should be adjusted to match the headCamera height")]
        public bool autoAdjustColliderHeight = true;
        [ShowIf("autoAdjustColliderHeight")]
        [Tooltip("Minimum and maximum auto adjusted height, to adjust height without auto adjustment change capsule collider height instead")]
        public Vector2 minMaxHeight = new Vector2(0.7f, 2f);


        [Space, Header("Movement")]
        [Tooltip("Movement speed when isGrounded")]
        public Vector2 groundedSpeed = new Vector2(2f, 2f);
        [Tooltip("A higher isGrounded drag will reduce potential of jittering when isGrounded")]
        public float groundedDrag = 1f;


        [Space, Header("Falling Movement")]
        [Tooltip("Lower drag when falling option so you have options to not fall like a feather")]
        public Vector2 fallingSpeed = new Vector2(2f, 2f);
        [Tooltip("Movement speed when falling")]
        public float fallingDrag = 1f;


        [Space, Header("Turning")]
        [Tooltip("Whether or not to use snap turning or smooth turning"), Min(0)]
        public bool snapTurning = true;
        [Tooltip("turn speed when not using snap turning - if snap turning, represents angle per snap")]
        public float turnSpeed = 15f;
        
        
        [Space, Header("Grounding")]
        public bool useGrounding = true;
        [Tooltip("Maximum height that the body can step up onto"), Min(0)]
        public float maxStepHeight = 0.1f;
        [Tooltip("Maximum angle the player can walk on"), Min(0)]
        public float maxStepAngle = 30f;
        [Tooltip("The layers that count as ground")]
        public LayerMask groundLayerMask;

        
        [Space, Header("Climbing")]
        [Tooltip("Whether or not the player can use Climbable objects")]
        public bool allowClimbing = true;
        [Tooltip("Whether or not the player move while climbing")]
        public bool allowClimbingMovement = true;
        [Tooltip("How quickly the player can climb")]
        public Vector3 climbingStrength = new Vector3(0.5f, 1f, 0.5f);



        [Space, Space, Header("EXPERIMENTAL")]

        [Space, Header("Flying - BETA")]
        public bool flying = false;
        [Tooltip("Movement speed when flying")]
        public Vector2 flyingSpeed = new Vector2(1f, 1f);

        [Space, Header("Platforms - BETA")]
        [Tooltip("Platforms will move the player with them. A platform is an object with the PlayerPlatform component on it")]
        public bool allowPlatforms = true;

        [Space, Header("Pushing - BETA")]
        [Tooltip("Whether or not the player can use Climbable objects")]
        public bool allowBodyPushing = true;
        [Tooltip("How quickly the player can climb")]
        public Vector3 pushingStrength = new Vector3(1f, 1f, 1f);

        float movementDeadzone = 0.2f;
        float turnDeadzone = 0.6f;
        float turnResetzone = 0.3f;
        float groundedOffset = 0.01f;
        [Tooltip("Movement speed based on headCamera forward direction")]
        float headFollowSpeed = 2f;

        HeadPhysicsFollower headPhysicsFollower;
        Rigidbody body;
        CapsuleCollider bodyCapsule;

        Transform moveTo;
        Vector3 moveAxis;
        Vector3 climbAxis;
        Vector3 adjustedOffset;
        float headHeightOffset;
        float turningAxis;
        float deltaY;
        bool isGrounded = false;
        bool axisReset = true;
        float playerHeight = 0;
        float lastHeightOffset;

        Hand lastRightHand;
        Hand lastLeftHand;
        
        Dictionary<Hand, Climbable> climbing;
        Dictionary<Pushable, Hand> pushRight;
        Dictionary<Pushable, int> pushRightCount;
        Dictionary<Pushable, Hand> pushLeft;
        Dictionary<Pushable, int> pushLeftCount;
        List<GameObject> collisions = new List<GameObject>();
        private Vector3 pushAxis;
        RaycastHit groundHit;

        List<PlayerPlatform> platforms = new List<PlayerPlatform>();
        Dictionary<PlayerPlatform, int> platformsCount = new Dictionary<PlayerPlatform, int>();
        Dictionary<PlayerPlatform, Vector3> platformPositions = new Dictionary<PlayerPlatform, Vector3>();
        Dictionary<PlayerPlatform, Quaternion> platformRotations = new Dictionary<PlayerPlatform, Quaternion>();

        public void Start(){
            gameObject.layer = LayerMask.NameToLayer("HandPlayer");

            bodyCapsule = GetComponent<CapsuleCollider>();

            body = GetComponent<Rigidbody>();

            if(body.collisionDetectionMode == CollisionDetectionMode.Discrete)
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            
            if(forwardFollow == null)
                forwardFollow = headCamera.transform;

            body.freezeRotation = true;
            
            climbing = new Dictionary<Hand, Climbable>();
            pushRight = new Dictionary<Pushable, Hand>();
            pushRightCount = new Dictionary<Pushable, int>();
            pushLeft = new Dictionary<Pushable, Hand>();
            pushLeftCount = new Dictionary<Pushable, int>();
            collisions = new List<GameObject>();


            moveTo = new GameObject().transform;
            moveTo.transform.rotation = transform.rotation;
            moveTo.name = "PLAYER FOLLOW POINT";
            

            deltaY = transform.position.y;

            CreateHeadFollower();
        }

        void OnEnable(){
            EnableHand(handRight);
            EnableHand(handLeft);
        }

        void OnDisable(){
            DisableHand(handRight);
            DisableHand(handLeft);
        }



        void CreateHeadFollower() {
            var headFollower = new GameObject().transform;
            headFollower.name = "Head Follower";
            headFollower.parent = transform.parent;

            var col = headFollower.gameObject.AddComponent<SphereCollider>();
            col.material = bodyCapsule.material;
            col.radius = bodyCapsule.radius;
            col.material = bodyCapsule.material;

            var headBody = headFollower.gameObject.AddComponent<Rigidbody>();
            headBody.drag = 10;
            headBody.angularDrag = 5;
            headBody.freezeRotation = false;
            headBody.mass = body.mass/3f;
            headBody.position = new Vector3(transform.position.x, transform.position.y+1, transform.position.z);

            headPhysicsFollower = headFollower.gameObject.AddComponent<HeadPhysicsFollower>();
            headPhysicsFollower.headCamera = headCamera;
            headPhysicsFollower.followBody = transform;
            headPhysicsFollower.trackingContainer = trackingContainer;
            headPhysicsFollower.maxBodyDistance = maxHeadDistance;
        }

        

        void CheckHands() {
            if (lastLeftHand != handLeft) {
                EnableHand(handLeft);
                lastLeftHand = handLeft;
            }

            if (lastRightHand != handRight) {
                EnableHand(handRight);
                lastRightHand = handRight;
            }
        }

        void EnableHand(Hand hand) { 
            if(allowClimbing){
                hand.OnGrabbed += StartClimb;
                hand.OnHeldConnectionBreak += EndClimb;
            }
            
            if(allowBodyPushing){
                hand.OnGrabbed += StartGrabPush;
                hand.OnHeldConnectionBreak += EndGrabPush;
                hand.OnHandTriggerStart += StartPush;
                hand.OnHandTriggerStop += StopPush;
            }
        }

        void DisableHand(Hand hand) { 
            if(allowClimbing){
                hand.OnGrabbed -= StartClimb;
                hand.OnHeldConnectionBreak -= EndClimb;
                if(climbing.ContainsKey(hand))
                    climbing.Remove(hand);
            }
            
            if(allowBodyPushing)
            {
                hand.OnGrabbed -= StartGrabPush;
                hand.OnHeldConnectionBreak -= EndGrabPush;
                hand.OnHandTriggerStart -= StartPush;
                hand.OnHandTriggerStop -= StopPush;
                if (hand.left) {
                    pushLeft.Clear();
                    pushLeftCount.Clear();
                }
                else { 
                    pushRight.Clear();
                    pushRightCount.Clear();
                    
                }
            }
        }


        public void Move(Vector2 axis) {
            if(Mathf.Abs(axis.x) > movementDeadzone)
                this.moveAxis.x = axis.x;
            else
                this.moveAxis.x = 0;
            if(Mathf.Abs(axis.y) > movementDeadzone)
                this.moveAxis.z = axis.y;
            else
                this.moveAxis.z = 0;
        }

        /// <summary>The 0-1 value that represents the turn speed or axis</summary>
        public void Turn(float turnAxis) {
            turningAxis = turnAxis;
        }


        void Update() {
            if(!headPhysicsFollower.Started())
                return;


            //This will allow for smoother movement when not colliding with things
            if (!UsePhysicsMovement()){
                Ground();
                FrameUpdateMove();
            }

        }


        void FixedUpdate(){
            if(!headPhysicsFollower.Started())
                return;

            CheckHands();

            //This will allow for correct collision detection when colliding with things
            if(UsePhysicsMovement()){
                Ground();
                PhysicsUpdateMove();
            }

            UpdateTurn();
            MoveTo();

            UpdatePlayerHeight();
            CheckPlatforms();
        }



        void FrameUpdateMove()
        {
            Vector3 moveSpeed = Vector3.zero;

            if (!flying) {

                var forwardAxis = Quaternion.AngleAxis(forwardFollow.eulerAngles.y, Vector3.up);
                if(isGrounded) {
                    moveSpeed = forwardAxis*new Vector3(groundedSpeed.x*moveAxis.x, 0, groundedSpeed.y*moveAxis.z) * Time.deltaTime;
                }
                else {
                    moveSpeed = forwardAxis*new Vector3(fallingSpeed.x*moveAxis.x, 0, fallingSpeed.y*moveAxis.z) * Time.deltaTime;
                    if (isGrounded && moveSpeed.y < 0)
                        moveSpeed.y = 0;
                }
            }
            else {
                moveSpeed = forwardFollow.rotation*new Vector3(flyingSpeed.x*moveAxis.x, 0, flyingSpeed.y*moveAxis.z) * Time.deltaTime;
            }

            //Adjusts height to headCamera to match transform height movements
            moveSpeed += new Vector3(0, transform.position.y-deltaY, 0);

            
            var flatPosition = headCamera.transform.position;
            flatPosition.y = 0;
            var flattransformPosition = transform.position+moveSpeed;
            flattransformPosition.y = 0;
            if(Vector3.Distance(flatPosition, flattransformPosition) >= maxHeadDistance) {
                var idealPos = (flattransformPosition - flatPosition) - (flattransformPosition - flatPosition).normalized*maxHeadDistance;
                moveSpeed += idealPos;
            }
            
            
            if(IsClimbing() && allowClimbing){
                var climbOffset = climbAxis*Time.deltaTime * 1f/(2f*Time.deltaTime);
                if(!flying)
                    climbOffset.y = 0;
                headPhysicsFollower.transform.position += climbOffset;
                trackingContainer.position += climbOffset;

                if(allowClimbingMovement){
                    headPhysicsFollower.transform.position += moveSpeed;
                    trackingContainer.position += moveSpeed;
                    if (flying)
                        body.position += new Vector3(0, moveSpeed.y, 0);
                }
            }
            else if(moveSpeed != Vector3.zero){
                headPhysicsFollower.transform.position += moveSpeed;
                trackingContainer.position += moveSpeed;
                if(flying)
                    body.position += new Vector3(0, moveSpeed.y, 0);
            }


            //Keeps the head down when colliding something above it and manages bouncing back up when not
            if(Vector3.Distance(headCamera.transform.position, headPhysicsFollower.transform.position) > headPhysicsFollower.headCollider.radius/2f) {
                var idealPos = headPhysicsFollower.transform.position+(headCamera.transform.position - headPhysicsFollower.transform.position).normalized*headPhysicsFollower.headCollider.radius/2f;
                var offsetPos = headCamera.transform.position - idealPos;
                trackingContainer.position -= offsetPos;
                adjustedOffset += offsetPos;
            }
            else if(headPhysicsFollower.CollisionCount() == 0){
                var moveAdjustedOffset = Vector3.MoveTowards(adjustedOffset, Vector3.zero, Time.deltaTime);
                var moveAdjustedOffsetY = moveAdjustedOffset;
                moveAdjustedOffsetY.x = moveAdjustedOffsetY.z = 0;
                headPhysicsFollower.transform.position += moveAdjustedOffsetY;
                trackingContainer.position += moveAdjustedOffsetY;
                adjustedOffset -= moveAdjustedOffset;
            }

            deltaY = body.position.y;
        }



        void PhysicsUpdateMove() {
            Vector3 moveSpeed = Vector3.zero;


            if (!flying) {

                var forwardAxis = Quaternion.AngleAxis(forwardFollow.eulerAngles.y, Vector3.up);
                if (isGrounded) {
                    moveSpeed = forwardAxis*new Vector3(groundedSpeed.x*moveAxis.x, 0, groundedSpeed.y*moveAxis.z) * Time.fixedDeltaTime;
                }
                else {
                    moveSpeed = forwardAxis*new Vector3(fallingSpeed.x*moveAxis.x, 0, fallingSpeed.y*moveAxis.z) * Time.fixedDeltaTime;
                }
            }
            else {
                moveSpeed = forwardFollow.rotation*new Vector3(flyingSpeed.x*moveAxis.x, 0, flyingSpeed.y*moveAxis.z) * Time.fixedDeltaTime;
                if (isGrounded && moveSpeed.y < 0)
                    moveSpeed.y = 0;
            }

            //Adjusts height to headCamera to match body height movements
            moveSpeed += new Vector3(0, body.position.y-deltaY, 0);

            var flatPosition = headCamera.transform.position;
            flatPosition.y = 0;
            var flatBodyPosition = body.position+moveSpeed;
            flatBodyPosition.y = 0;
            if(Vector3.Distance(flatPosition, flatBodyPosition) >= maxHeadDistance) {
                var idealPos = (flatBodyPosition - flatPosition) - (flatBodyPosition - flatPosition).normalized*maxHeadDistance;
                moveSpeed += idealPos;
            }
            
            
            if(IsClimbing() && allowClimbing){
                var climbOffset = climbAxis * Time.fixedDeltaTime * 1f / (2f * Time.fixedDeltaTime);
                if (!flying)
                    climbOffset.y = 0;
                headPhysicsFollower.body.position += climbOffset;
                trackingContainer.position += climbOffset;

                if(allowClimbingMovement){
                    headPhysicsFollower.body.position += moveSpeed;
                    trackingContainer.position += moveSpeed;
                }
            }
            else if(IsPushing() && allowBodyPushing) {
                var pushOffset = pushAxis * Time.fixedDeltaTime * 1f / (2f * Time.fixedDeltaTime);
                if (!flying) {
                    pushOffset.y = 0;
                }

                headPhysicsFollower.body.position += pushOffset;
                trackingContainer.position += pushOffset;
                
                headPhysicsFollower.body.position += moveSpeed;
                trackingContainer.position += moveSpeed;
                if (flying)
                    body.position += new Vector3(0, moveSpeed.y, 0);


            }
            else if(moveSpeed != Vector3.zero){
                headPhysicsFollower.body.position += moveSpeed;
                trackingContainer.position += moveSpeed;
                if (flying)
                    body.position += new Vector3(0, moveSpeed.y, 0);
            }


            //Keeps the head down when colliding something above it and manages bouncing back up when not
            if(Vector3.Distance(headCamera.transform.position, headPhysicsFollower.transform.position) > headPhysicsFollower.headCollider.radius/2f) {
                var idealPos = headPhysicsFollower.transform.position+(headCamera.transform.position - headPhysicsFollower.transform.position).normalized*headPhysicsFollower.headCollider.radius/2f;
                var offsetPos = headCamera.transform.position - idealPos;
                trackingContainer.position -= offsetPos;
                adjustedOffset += offsetPos;
            }
            
            if(headPhysicsFollower.CollisionCount() == 0){
                var moveAdjustedOffset = Vector3.MoveTowards(adjustedOffset, Vector3.zero, Time.fixedDeltaTime);
                var moveAdjustedOffsetY = moveAdjustedOffset;
                moveAdjustedOffsetY.x = moveAdjustedOffsetY.z = 0;
                headPhysicsFollower.body.position += moveAdjustedOffsetY;
                trackingContainer.position += moveAdjustedOffsetY;
                adjustedOffset -= moveAdjustedOffset;
            }

            deltaY = body.position.y;
        }




        void UpdateTurn(){
            //Snap turning
            if(snapTurning){
                if (turningAxis > turnDeadzone && axisReset){
                    trackingContainer.RotateAround(headCamera.transform.position, Vector3.up, turnSpeed);
                    axisReset = false;
                    handRight.SetHandLocation(handRight.moveTo.position, handRight.moveTo.rotation);
                    handLeft.SetHandLocation(handLeft.moveTo.position, handLeft.moveTo.rotation);
                }
                else if (turningAxis < -turnDeadzone && axisReset){
                    trackingContainer.RotateAround(headCamera.transform.position, Vector3.up, -turnSpeed);
                    axisReset = false;
                    handRight.SetHandLocation(handRight.moveTo.position, handRight.moveTo.rotation);
                    handLeft.SetHandLocation(handLeft.moveTo.position, handLeft.moveTo.rotation);
                }

                if (Mathf.Abs(turningAxis) < turnResetzone)
                    axisReset = true;
            }
            //Smooth turning
            else {
                if(Mathf.Abs(turningAxis) < turnDeadzone)
                    turningAxis = 0;

                trackingContainer.transform.rotation *= Quaternion.Euler(0, Time.fixedDeltaTime*turnSpeed*turningAxis, 0);
            }
            
            
        }

        public void SetPosition(Vector3 position){
            Vector3 deltaPos = position - transform.position;
            transform.position += deltaPos;
            body.position = transform.position;
            headPhysicsFollower.transform.position += deltaPos;
            headPhysicsFollower.body.position = headPhysicsFollower.transform.position;

            deltaPos.y = 0;
            trackingContainer.position += deltaPos;
        }


        void Ground(){
            isGrounded = false;
            if(!flying && !IsClimbing() && pushAxis.y <= 0){
                RaycastHit stepHit;
                var stepPos = transform.position;
                float highestPoint = -1;
                float newHeightPoint = transform.position.y;

                stepPos.y += maxStepHeight;
                if(Physics.Raycast(stepPos, Vector3.down, out stepHit, maxStepHeight + groundedOffset, groundLayerMask)) {
                    isGrounded = true;
                    var stepAngle = Vector3.Angle(stepHit.normal, Vector3.up);
                    if(stepAngle < maxStepAngle && stepHit.point.y - transform.position.y > highestPoint) {
                        groundHit = stepHit;
                        highestPoint = stepHit.point.y - transform.position.y;
                        newHeightPoint = stepHit.point.y;
                    }
                }
            
                for(int i = 0; i < 8; i++) {
                    stepPos = transform.position;
                    stepPos.x += Mathf.Cos(i*Mathf.PI/4f)*(bodyCapsule.radius+0.05f);
                    stepPos.z += Mathf.Sin(i*Mathf.PI/4f)*(bodyCapsule.radius+0.05f);
                    stepPos.y += maxStepHeight;
                    if(Physics.Raycast(stepPos, Vector3.down, out stepHit, maxStepHeight + groundedOffset, groundLayerMask)) {
                        isGrounded = true;
                        var stepAngle = Vector3.Angle(stepHit.normal, Vector3.up);
                        if(stepAngle < maxStepAngle && stepHit.point.y - transform.position.y > highestPoint) {
                            groundHit = stepHit;
                            highestPoint = stepHit.point.y - transform.position.y;
                            newHeightPoint = stepHit.point.y;
                        }
                    }
                }
            
                for(int i = 0; i < 8; i++) {
                    stepPos = transform.position;
                    stepPos.x += Mathf.Cos(i*Mathf.PI/4f)*(bodyCapsule.radius+0.05f)/2f;
                    stepPos.z += Mathf.Sin(i*Mathf.PI/4f)*(bodyCapsule.radius+0.05f)/2f;
                    stepPos.y += maxStepHeight;
                    if(Physics.Raycast(stepPos, Vector3.down, out stepHit, maxStepHeight + groundedOffset, groundLayerMask)) {
                        isGrounded = true;
                        var stepAngle = Vector3.Angle(stepHit.normal, Vector3.up);
                        if(stepAngle < maxStepAngle && stepHit.point.y - transform.position.y > highestPoint) {
                            groundHit = stepHit;
                            highestPoint = stepHit.point.y - transform.position.y;
                            newHeightPoint = stepHit.point.y;
                        }
                    }
                }

                if(useGrounding && isGrounded) {
                    var newHeight = transform.position;
                    newHeight.y = newHeightPoint;
                    transform.position = newHeight;
                }
            }

            if (isGrounded) {
                body.constraints = RigidbodyConstraints.FreezePositionY;
                body.freezeRotation = true;
                body.useGravity = false;
                body.drag = groundedDrag;
                bodyCapsule.enabled = true;
            }
            else if (flying){
                body.constraints = RigidbodyConstraints.None;
                body.freezeRotation = true;
                body.drag = fallingDrag;
                body.useGravity = false;
                bodyCapsule.enabled = false;
            }
            else {
                body.constraints = RigidbodyConstraints.None;
                body.freezeRotation = true;
                body.drag = fallingDrag;
                body.useGravity = true;
                bodyCapsule.enabled = true;
            }
            
        }
        
        public bool IsGrounded(){
            return isGrounded;
        }


        void UpdatePlayerHeight(){
            if (flying)
                return;

            if (heightOffset != lastHeightOffset) {
                trackingContainer.Translate(new Vector3(0, heightOffset - lastHeightOffset, 0));
                lastHeightOffset = heightOffset;
            }

            if(autoAdjustColliderHeight){
                playerHeight = Mathf.Clamp(headCamera.transform.position.y - transform.position.y, minMaxHeight.x, minMaxHeight.y);
                bodyCapsule.height = playerHeight;
                var centerHeight = playerHeight/2f > bodyCapsule.radius ? playerHeight/2f : bodyCapsule.radius; 
                bodyCapsule.center = new Vector3(0, centerHeight, 0);
            }
        }
        
        void StartPush(Hand hand, GameObject other) {
            if(!allowBodyPushing || IsClimbing())
                return;

            if(other.CanGetComponent(out Pushable push) && push.enabled) {
                if(hand.left) {
                    if(!pushLeft.ContainsKey(push)){
                        pushLeft.Add(push, hand);
                        pushLeftCount.Add(push, 1);
                    }
                    else {
                        pushLeftCount[push]++;
                    }
                }

                if(!hand.left && !pushRight.ContainsKey(push)) {
                    if(!pushRight.ContainsKey(push)){
                        pushRight.Add(push, hand);
                        pushRightCount.Add(push, 1);
                    }
                    else {
                        pushRightCount[push]++;
                    }
                }
            }
        }
        
        void StopPush(Hand hand, GameObject other) {
            if(!allowBodyPushing)
                return;
            
            if(other.CanGetComponent(out Pushable push)) {
                if(hand.left && pushLeft.ContainsKey(push)) {
                    var count = --pushLeftCount[push];
                    if(count == 0){
                        pushLeft.Remove(push);
                        pushLeftCount.Remove(push);
                    }
                }
                if(!hand.left && pushRight.ContainsKey(push)) {
                    var count = --pushRightCount[push];
                    if(count == 0){
                        pushRight.Remove(push);
                        pushRightCount.Remove(push);
                    }
                }
            }
        }

        
        void ApplyPushingForce() {
            pushAxis = Vector3.zero;
            var rightHandCast = Physics.RaycastAll(handRight.transform.position, Vector3.down, 0.1f, ~handRight.handLayers);
            var leftHandCast = Physics.RaycastAll(handLeft.transform.position, Vector3.down, 0.1f, ~handLeft.handLayers);
            List<GameObject> hitObjects = new List<GameObject>();
            foreach (var hit in rightHandCast){
                hitObjects.Add(hit.transform.gameObject);
            }
            foreach (var hit in leftHandCast){
                hitObjects.Add(hit.transform.gameObject);
            }

            foreach (var push in pushRight) {
                if (push.Key.enabled && !push.Value.IsGrabbing()){
                    Vector3 offset = Vector3.zero;
                    var distance = Vector3.Distance(push.Value.body.position, push.Value.moveTo.position);
                    if (distance > 0)
                        offset = Vector3.Scale((push.Value.body.position - push.Value.moveTo.position), push.Key.strengthScale);

                    offset = Vector3.Scale(offset, pushingStrength);
                    if (!hitObjects.Contains(push.Key.transform.gameObject))
                        offset.y = 0;
                    pushAxis += offset;
                }
            }

            foreach(var push in pushLeft) {
                if (push.Key.enabled && !push.Value.IsGrabbing()){
                    Vector3 offset = Vector3.zero;
                    var distance = Vector3.Distance(push.Value.body.position, push.Value.moveTo.position);
                    if(distance > 0)
                        offset = Vector3.Scale((push.Value.body.position-push.Value.moveTo.position), push.Key.strengthScale);

                    offset = Vector3.Scale(offset, pushingStrength);
                    if (!hitObjects.Contains(push.Key.transform.gameObject))
                        offset.y = 0;
                    pushAxis += offset;
                }
            }

        }
        void StartGrabPush(Hand hand, Grabbable grab) {
            if(!allowBodyPushing || IsClimbing())
                return;

            if(grab.CanGetComponent(out Pushable push) && push.enabled) {
                if(hand.left) {
                    if(!pushLeft.ContainsKey(push)){
                        pushLeft.Add(push, hand);
                        pushLeftCount.Add(push, 1);
                    }
                }

                if(!hand.left && !pushRight.ContainsKey(push)) {
                    if(!pushRight.ContainsKey(push)){
                        pushRight.Add(push, hand);
                        pushRightCount.Add(push, 1);
                    }
                }
            }
        }
        
        void EndGrabPush(Hand hand, Grabbable grab) {
            if(grab != null && grab.CanGetComponent(out Pushable push)) {
                if (hand.left && pushLeft.ContainsKey(push)) {
                    pushLeft.Remove(push);
                    pushLeftCount.Remove(push);
                }
                else if (!hand.left && pushRight.ContainsKey(push)) {
                    pushRight.Remove(push);
                    pushRightCount.Remove(push);
                }
                    
            }
        }
        
        public bool IsPushing() {
            bool isPushing = false;
            foreach (var push in pushRight){
                if (push.Key.enabled)
                    isPushing = true;
            }
            foreach (var push in pushLeft){
                if (push.Key.enabled)
                    isPushing = true;
            }

            return isPushing;
        }



        void StartClimb(Hand hand, Grabbable grab) {
            if(!allowClimbing)
                return;

            if(!climbing.ContainsKey(hand) && grab.CanGetComponent(out Climbable climbbable) && climbbable.enabled) {
                if(climbing.Count == 0) {
                    pushRight.Clear();
                    pushRightCount.Clear();

                    pushLeft.Clear();
                    pushLeftCount.Clear();
                }
                
                climbing.Add(hand, climbbable);
            }
        }
        
        void EndClimb(Hand hand, Grabbable grab) {
            if(!allowClimbing)
                return;

            if(climbing.ContainsKey(hand)) {
                var climb = climbing[hand];
                climbing.Remove(hand);
            }
        }

        void ApplyClimbingForce(){
            climbAxis = Vector3.zero;
            if(climbing.Count > 0){
                foreach(var hand in climbing) {
                    if (hand.Value.enabled) {
                        var offset = Vector3.Scale(hand.Key.body.position-hand.Key.moveTo.position, hand.Value.axis);
                        offset = Vector3.Scale(offset, climbingStrength);
                        climbAxis += offset;
                    }
                }
            }
        }

        public bool IsClimbing() {
            bool isClimbing = false;
            foreach (var climb in climbing){
                if (climb.Value.enabled)
                    isClimbing = true;
            }

            return isClimbing;
        }
        
        void CheckPlatforms(){
            if (!allowPlatforms)
                return;

            foreach (var platform in platforms){
                var deltaPos = platform.transform.position - platformPositions[platform];
                trackingContainer.position += deltaPos;
                body.position += deltaPos;
                platformPositions[platform] = platform.transform.position;

                var deltaRot = (Quaternion.Inverse(platformRotations[platform]) * platform.transform.rotation).eulerAngles;
                trackingContainer.RotateAround(platform.transform.position, Vector3.up, deltaRot.y);
                trackingContainer.RotateAround(platform.transform.position, Vector3.right, deltaRot.x);
                trackingContainer.RotateAround(platform.transform.position, Vector3.forward, deltaRot.z);

                platformRotations[platform] = platform.transform.rotation;
            }
        }
        
        private void OnTriggerEnter(Collider other){
            
            if (!allowPlatforms)
                return;

            if (other.CanGetComponent(out PlayerPlatform platform)){
                if (!platforms.Contains(platform)) {
                    platforms.Add(platform);
                    platformPositions.Add(platform, platform.transform.position);
                    platformRotations.Add(platform, platform.transform.rotation);
                    platformsCount.Add(platform, 1);
                }
                else {
                    platformsCount[platform]++;
                }
            }
        }

        private void OnTriggerExit(Collider other){
            if (!allowPlatforms)
                return;

            if (other.CanGetComponent(out PlayerPlatform platform)){
                if (platforms.Contains(platform)) {
                    if(platformsCount[platform]-1 == 0) {
                        platforms.Remove(platform);
                        platformPositions.Remove(platform);
                        platformRotations.Remove(platform);
                        platformsCount.Remove(platform);
                    }
                    else {
                        platformsCount[platform]--;
                    }
                }
            }
        }


        private void OnCollisionEnter(Collision collision)
        {
            if (!collisions.Contains(collision.gameObject)) {
                collisions.Add(collision.gameObject);
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if(collisions.Contains(collision.gameObject)) {
                collisions.Remove(collision.gameObject);
            }
        }



        /// <summary>Moves the player to the input position using physics movement</summary>
        void MoveTo() {
            moveTo.position = Vector3.zero;


            var headBodyDifference = headPhysicsFollower.body.position - body.position;
            if (!flying)
                headBodyDifference.y = 0;
            else
                headBodyDifference.y -= bodyCapsule.height+headPhysicsFollower.headCollider.radius;

            moveTo.position +=  headBodyDifference*headFollowSpeed*10f;

            if(allowClimbing && IsClimbing()){
                ApplyClimbingForce();
                moveTo.position += climbAxis;
                isGrounded = false;
            }

            else if(allowBodyPushing && IsPushing()) {
                ApplyPushingForce();
                moveTo.position += pushAxis;
                isGrounded = false;
            }
            

            
            //Sets velocity linearly based on distance from hand
            var vel = moveTo.position;

            Vector3 scaleSpeed = new Vector3(groundedSpeed.x, 1, groundedSpeed.y);
            if(!isGrounded)
                scaleSpeed = new Vector3(fallingSpeed.x, 1, fallingSpeed.y);
            
            vel.x *= scaleSpeed.x;
            vel.z *= scaleSpeed.z;

            if(allowBodyPushing && IsPushing()) {
                if((moveTo.position.x > 0 && (moveTo.position.x + pushAxis.x) < 0) || (moveTo.position.x < 0 && (moveTo.position.x + pushAxis.x) > 0))
                    moveTo.position = Vector3.Scale(moveTo.position, new Vector3(0, 1, 1));

                if((moveTo.position.z > 0 && (moveTo.position.z + pushAxis.z) < 0) || (moveTo.position.z < 0 && (moveTo.position.z + pushAxis.z) > 0))
                    moveTo.position = Vector3.Scale(moveTo.position, new Vector3(1, 1, 0));
            }

            if(!flying && !IsClimbing() && !isGrounded && pushAxis.y <= 0){
                vel.y = body.velocity.y;
            }

            body.velocity = vel;

        }


        public bool UsePhysicsMovement(){
            if (headPhysicsFollower == null)
                return true;

            var flatPosition = headCamera.transform.position;
            flatPosition.y = 0;
            var flattransformPosition = transform.position;
            flattransformPosition.y = 0;
            return platformsCount.Count > 0 || climbing.Count > 0 || pushRight.Count > 0 || pushLeft.Count > 0 || handRight.CollisionCount() > 0 || handLeft.CollisionCount() > 0 || headPhysicsFollower.CollisionCount() > 0 || Vector3.Distance(flatPosition, flattransformPosition) >= maxHeadDistance*0.8f;
        }

        




        public static LayerMask GetPhysicsLayerMask(int currentLayer) {
            int finalMask = 0;
            for(int i = 0; i < 32; i++){
                if(!Physics.GetIgnoreLayerCollision(currentLayer, i))
                    finalMask = finalMask | (1 << i);
            }
            return finalMask;
        }





        private void OnDrawGizmos() {
            if(bodyCapsule == null)
                bodyCapsule = GetComponent<CapsuleCollider>();
            
            if(isGrounded)
                Gizmos.color = Color.green;
            else
                Gizmos.color = Color.red;

            var offsetPos = transform.position;
            var offSetEndPos = offsetPos;
            offSetEndPos.y += maxStepHeight;
            Gizmos.DrawLine(offsetPos, offSetEndPos);
            
            for(int i = 0; i < 8; i++) {
                offsetPos = transform.position;
                offsetPos.x += Mathf.Cos(i*Mathf.PI/4f)*(bodyCapsule.radius+0.05f);
                offsetPos.z += Mathf.Sin(i*Mathf.PI/4f)*(bodyCapsule.radius+0.05f);
                offSetEndPos = offsetPos;
                offSetEndPos.y += maxStepHeight;
                Gizmos.DrawLine(offsetPos, offSetEndPos);
            }

            for(int i = 0; i < 8; i++) {
                offsetPos = transform.position;
                offsetPos.x += Mathf.Cos(i*Mathf.PI/4f)*(bodyCapsule.radius+0.05f)/2f;
                offsetPos.z += Mathf.Sin(i*Mathf.PI/4f)*(bodyCapsule.radius+0.05f)/2f;
                offSetEndPos = offsetPos;
                offSetEndPos.y += maxStepHeight;
                Gizmos.DrawLine(offsetPos, offSetEndPos);
            }
            
            if(headCamera == null)
                return;

            Gizmos.color = Color.blue;
            var containerAxis = Quaternion.AngleAxis(forwardFollow.transform.localEulerAngles.y, Vector3.up);
            var forward = Quaternion.AngleAxis(forwardFollow.transform.localEulerAngles.y, Vector3.up);
            Gizmos.DrawRay(transform.position, containerAxis*forward*Vector3.forward);
            
            Gizmos.color = Color.red;
            var right = Quaternion.AngleAxis(forwardFollow.transform.localEulerAngles.y, Vector3.up);
            Gizmos.DrawRay(transform.position, containerAxis*right*Vector3.right);
        }
    }
}
