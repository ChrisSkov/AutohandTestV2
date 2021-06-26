using CustomAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Autohand{
    [RequireComponent(typeof(Grabbable))]
    public class DistanceGrabbable : MonoBehaviour{
        
        
        [Header("Pull")]
        public bool targetable = true;
        public bool instantPull = false;

        [Header("Velocity Shoot")]
        [Tooltip("Use this to adjust the angle of the arch that the gameobject follows while shooting towards your hand."), ConditionalShow("instantPull")]
        public float archMultiplier = .6f;

        [Tooltip("The collision layers that stop the gravitation on collision"), ConditionalShow("instantPull")]
        public LayerMask stopLayers;


    
        [Header("Gravitation")]
        [Tooltip("This enables gravitation which makes the gameobject curve towards the position of your hand"), ConditionalShow("instantPull")]
        public bool gravitate = true;

        [Tooltip("Gravitation activates after you move your hand a certain distance from where you initially flicked. This is that distance.")]
        [ConditionalShow("instantPull"), ConditionalHide("gravitate")]
        public float requiredPullDistance = 0.2f;

        [Tooltip("Slow down or speed up gravitation to your liking.")]
        [ConditionalShow("instantPull"), ConditionalHide("gravitate")]
        public float gravitationVelocity = 1f;



        [Header("Rotation")]
        [Tooltip("This enables rotation which makes the gameobject orient to the rotation of you hand as it moves through the air. All below rotation variables have no use when this is false.")]
        [ConditionalShow("instantPull")]
        public bool rotate = true;

        [Tooltip("Speed that the object orients to the rotation of your hand.")]
        [ConditionalShow("instantPull"), ConditionalHide("rotate")]
        public float rotationSpeed = 5;

        [Tooltip("The percent of the initial distance from your hand that the object will stop rotating at.")]
        [ConditionalShow("instantPull"), ConditionalHide("rotate")]
        public float percentDistanceBeforeRotation = .12f;
        
        [Header("Highlight Options")]
        [Space, Tooltip("Whether or not to ignore all highlights include default highlights on HandPointGrab")]
        public bool ignoreHighlights = false;
        [Tooltip("Highlight targeted material to use - defaults to HandPointGrab materials if none")]
        public Material targetedMaterial;
        [Tooltip("Highlight selected material to use - defaults to HandPointGrab materials if none")]
        public Material selectedMaterial;
    
        [Header("Events")]
        public UnityEvent OnPull;
        [Space,Space]
        [Tooltip("Called when the object has been targeted/aimed at by the pointer")]
        public UnityEvent StartTargeting;
        public UnityEvent StopTargeting;
        [Space,Space]
        [Tooltip("Called when the object has been selected before being pulled or flicked")]
        public UnityEvent StartSelecting;
        public UnityEvent StopSelecting;

        public HandGrabEvent OnPullCanceled;

        internal Grabbable grabbable;
    

        private Transform Target;
        private Vector3 calculatedNecessaryVelocity;
        private bool startRotation;
        private float startDistance;
        private Vector3 initialPosition;
        private bool gravitationEnabled;
        private bool gravitationMethodBegun;
        private bool pullStarted;
        private Rigidbody body;

        private void Start() {
            grabbable = GetComponent<Grabbable>();
            body = grabbable.body;
        }
    
        void FixedUpdate(){
            if(!instantPull){
                InitialVelocityPushToHand();
                if(rotate)
                    FollowHandRotation();
                if (gravitate)
                    GravitateTowardsHand();
            }
        }


        private void FollowHandRotation(){
            if (startRotation){
                if (Vector3.Distance(transform.position, Target.position) > startDistance * percentDistanceBeforeRotation) //if the distance from the hand is still within 12% of the initial distance upon flicking, then you rotate the object
                    transform.rotation = Quaternion.Slerp(transform.rotation, Target.rotation, rotationSpeed * Time.deltaTime); //rotationSpeed is how quickly the object will orient itself to the rotation of the hand
                else
                    startRotation = false;
            }
        }


        private void GravitateTowardsHand(){
            if (Target != null){
                if (gravitationEnabled){

                    if (!gravitationMethodBegun){
                        initialPosition = Target.position;
                        gravitationMethodBegun = true;
                    }
                    
                    if (Vector3.Distance(initialPosition, Target.position) > requiredPullDistance){
                        float speed = body.velocity.magnitude;
                        Vector3 newVelocity = transform.position - Target.position;
                        body.velocity = (-newVelocity.normalized * gravitationVelocity ) * speed;
                    }
                }
                else{
                    gravitationMethodBegun = false;
                }
            }
        }


        private void InitialVelocityPushToHand(){
            if (Target != null)
                calculatedNecessaryVelocity = CalculateTrajectoryVelocity(transform.position, Target.transform.position, archMultiplier);

            //This way I can ensure that the initial shot with velocity is only shot once
            if (pullStarted){
                OnPull?.Invoke();
                startDistance = Vector3.Distance(transform.position, Target.position);
                body.velocity = calculatedNecessaryVelocity;
                gravitationEnabled = true;
                startRotation = true;
                pullStarted = false;
            }
        }

        private void OnCollisionEnter(Collision collision){
            pullStarted = false;
            startRotation = false;
            gravitationEnabled = false;

            if(stopLayers == (stopLayers | (1 << collision.gameObject.layer))){
                OnPullCanceled?.Invoke(null, grabbable);
            }
        }


        Vector3 CalculateTrajectoryVelocity(Vector3 origin, Vector3 target, float t){
            float vx = (target.x - origin.x) / t;
            float vz = (target.z - origin.z) / t;
            float vy = ((target.y - origin.y) - 0.5f * Physics.gravity.y * t * t) / t;
            return new Vector3(vx, vy, vz);
        }

        public void SetTarget(Transform theObject) { Target = theObject; pullStarted = true;  }
    }
}
