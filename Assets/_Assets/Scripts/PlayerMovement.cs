using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Transactions;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEditor.IMGUI.Controls.CapsuleBoundsHandle;

[RequireComponent(typeof(PlayerInput))]


public class PlayerMovement : MonoBehaviour
{

    public InputActionReference moveInput;
    public InputActionReference jumpInput;
    public InputActionReference glideInput;


    bool isMovementPressed;

    public Animator playerAnimator;

    int verticalAnimatorInt;


    Vector2 playerInput;
    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10, maxGlideSpeed = 12f, maxSwimSpeed = 5f;

    float currentMaxSpeed;

    [SerializeField, Range(0f, 100f)]
    float maxAcceleration = 10f, maxAirAcceleration = 1f, maxSwimAcceleration = 5f;

    [SerializeField]
    Vector3 velocity, desiredVelocity, connectionVelocity;

    Rigidbody body, connectedBody, previousConnectedBody;

    public Transform playerModel;

    bool desiredJump;

    bool isJumping;

    bool glidingButtonHeld;
    public bool isGliding;

    [SerializeField, Range(0f, 10f)]
    float jumpHeight = 2f;

    [SerializeField, Range(-10f, 0f)]
    float glideFallSpeed = -3f;

    Vector3 contactNormal, steepNormal;

    public int groundContactCount, steepContactCount;

    bool OnGround => groundContactCount > 0;
    bool OnSteep => steepContactCount > 0;

    [SerializeField, Range(0, 5)]
    int maxAirJumps = 0;

    public float jumpButtonGracePeriod;
    float? lastGroundedTime;
    float? jumpButtonPressedTime;

    int jumpPhase;

    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f, maxStairsAngle = 50f;

    float minGroundDotProduct, minStairsDotProduct;


    int stepsSinceLastGrounded, stepsSinceLastJump;

    [SerializeField, Range(0f, 100f)]
    float maxSnapSpeed = 100f;

    Vector3 upAxis;

    [SerializeField, Min(0f)]
    float probeDistance = 1f;

    bool InWater => submergence > 0f;

    float submergence;

    [SerializeField]
	float submergenceOffset = 0.5f;

	[SerializeField, Min(0.1f)]
	float submergenceRange = 1f;

    [SerializeField, Range(0f, 10f)]
	float waterDrag = 1f;

    [SerializeField, Min(0f)]
	float buoyancy = 1f;

	[SerializeField, Range(0.01f, 1f)]
	float swimThreshold = 0.5f;

    bool Swimming => submergence >= swimThreshold;


    [SerializeField]
    LayerMask probeMask = -1, stairsMask = -1, waterMask = 0;

    [SerializeField]
    Transform playerInputSpace = default;

    float currentRotationFactorPerFrame, rotationFactorPerFrame = 10f, glideRotationFactorPerFrame = 2f;

    Vector3 connectionWorldPosition, connectionLocalPosition;

    void OnValidate() {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
    }

    void OnEnable() {
        jumpInput.action.started += JumpPressed;
        glideInput.action.started += GlideHeld;
        glideInput.action.canceled += GlideRelease;


    }
    void OnDisable() {
        jumpInput.action.started -= JumpPressed;
        glideInput.action.started -= GlideHeld;
        glideInput.action.canceled -= GlideRelease;



    }
    void JumpPressed(InputAction.CallbackContext obj){
        jumpButtonPressedTime = Time.time;

    }

    void GlideHeld(InputAction.CallbackContext obj){
        glidingButtonHeld = obj.ReadValueAsButton();

    }

    void GlideRelease(InputAction.CallbackContext obj){
        glidingButtonHeld = false;
    }
    void Awake()
    {
        body = GetComponent<Rigidbody>();
        verticalAnimatorInt = Animator.StringToHash("Vertical");
        body.useGravity = false;

        currentMaxSpeed = maxSpeed;

        OnValidate();
    }

    // Update is called once per frame
    void Update()
    {
        playerInput = moveInput.action.ReadValue<Vector2>();

        isMovementPressed = playerInput.x != 0 || playerInput.y != 0;

        playerInput = Vector2.ClampMagnitude(playerInput, 1f);

        if(playerInputSpace){
            Vector3 forward = playerInputSpace.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = playerInputSpace.right;
            right.y = 0f;
            right.Normalize();
            desiredVelocity = (forward * playerInput.y + right * playerInput.x) * currentMaxSpeed;
        }
        else{
            desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * currentMaxSpeed;
        }

        HandleAnimation();
        HandleRotation();
    }

    void HandleAnimation(){

        float moveAmount = Mathf.Clamp01(Mathf.Abs(playerInput.x) + Mathf.Abs(playerInput.y));

        float snappedVertical;

        if(moveAmount > 0 && moveAmount <0.55f){
            snappedVertical = 0.5f;
        }
        else if(moveAmount > 0.55f){
            snappedVertical = 1;
        }
        else if (moveAmount < 0 && moveAmount  > -0.55f){
            snappedVertical = -0.5f;
        }
        else if(moveAmount  < -0.55f){
            snappedVertical = -1;
        }
        else{
            snappedVertical = 0;
        }

        playerAnimator.SetFloat(verticalAnimatorInt, snappedVertical, 0.1f, Time.deltaTime);


    }

    void HandleRotation(){

        Vector3 positionToLookAt;
        positionToLookAt.x = desiredVelocity.x;
        positionToLookAt.y = 0.0f;
        positionToLookAt.z = desiredVelocity.z;
        Quaternion currentRotation = playerModel.rotation;

        if(isMovementPressed){
            Quaternion targetRotation = Quaternion.LookRotation(positionToLookAt);
            playerModel.rotation = Quaternion.Slerp(currentRotation, targetRotation, currentRotationFactorPerFrame * Time.deltaTime);
        }

    }

    void FixedUpdate() {

        upAxis = -Physics.gravity.normalized;

        UpdateState();

		if (InWater) {
			velocity *= 1f - waterDrag * submergence * Time.deltaTime;
        }
        AdjustVelocity();

        if (OnGround){
            lastGroundedTime = Time.time;
            playerAnimator.SetBool("IsGrounded", true);
            isJumping = false;
            playerAnimator.SetBool("IsGliding", false);
            isGliding = false;
            playerAnimator.SetBool("IsFalling", false);
        }
        else{

            playerAnimator.SetBool("IsGrounded", false);

            if(glidingButtonHeld && !InWater){
                Glide();
            }
            else{
                isGliding = false;
                playerAnimator.SetBool("IsGliding", false);
                playerAnimator.SetBool("IsFalling", true);

            }



            if((isJumping && velocity.y < 1) || velocity.y < -5){
                if(!isGliding){
                    playerAnimator.SetBool("IsFalling", true);
                }
            }


        }

        CheckGlideParameters();
     
        if (Time.time - jumpButtonPressedTime <= jumpButtonGracePeriod){
            Jump();
        }


        if (InWater) {
			velocity +=
				Physics.gravity * ((1f - buoyancy * submergence) * Time.deltaTime);
		}
        else if (OnGround && velocity.sqrMagnitude < 0.01f) {
			velocity +=	contactNormal *	(Vector3.Dot(Physics.gravity, contactNormal) * Time.deltaTime);
		}
		else {
			velocity += Physics.gravity * Time.deltaTime;
		}

        body.linearVelocity = velocity;

        ClearState();
    }

    void Glide(){

        isGliding = true;
        playerAnimator.SetBool("IsGliding", true);
        playerAnimator.SetBool("IsFalling", false);

        if (velocity.y < glideFallSpeed){
            velocity.y = glideFallSpeed;
        }

    }

    void CheckGlideParameters(){

        if(isGliding){
            currentMaxSpeed = maxGlideSpeed;
            currentRotationFactorPerFrame = glideRotationFactorPerFrame;
        }
        else{
            currentMaxSpeed = maxSpeed;
            currentRotationFactorPerFrame = rotationFactorPerFrame;

        }
    }

    void ClearState(){
        groundContactCount = steepContactCount = 0;
        contactNormal = steepNormal = connectionVelocity = Vector3.zero;
        previousConnectedBody = connectedBody;
        connectedBody = null;

        if(connectedBody){
            if (connectedBody.isKinematic || connectedBody.mass>body.mass){
                UpdateConnectionState();
            }
        }

        submergence = 0f;
    }

    void UpdateConnectionState(){
        if(connectedBody == previousConnectedBody){
            Vector3 connectionMovement = connectedBody.transform.TransformPoint(connectionLocalPosition) - connectionWorldPosition;
            connectionVelocity = connectionMovement / Time.deltaTime;
        }
        connectionWorldPosition = body.position;
        connectionLocalPosition = connectedBody.transform.InverseTransformPoint(connectionWorldPosition);

    }

    void UpdateState(){
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = body.linearVelocity;
        if(CheckSwimming() || OnGround || SnapToGround() || CheckSteepContacts()) { 
            stepsSinceLastGrounded = 0;
            if (stepsSinceLastJump > 1){
                jumpPhase = 0;
            }
            if(groundContactCount > 1){
                contactNormal.Normalize();
            }

        }
        else{
            contactNormal = Vector3.up;
        }
    }

    bool SnapToGround(){
        if(stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2 ){
            return false;
        }
        float speed = velocity.magnitude;
        if (speed > maxSnapSpeed){
            return false;
        }
        if(!Physics.Raycast(body.position, Vector3.down, out RaycastHit hit, probeDistance, probeMask, QueryTriggerInteraction.Ignore)){
            return false;
        }
        if(hit.normal.y < GetMinDot(hit.collider.gameObject.layer)){
            return false;
        }
        groundContactCount = 1;
        contactNormal = hit.normal;
        float dot = Vector3.Dot(velocity, hit.normal);
        if(dot > 0f){
            velocity = (velocity - hit.normal * dot).normalized * speed;          
        }
        connectedBody = hit.rigidbody;
        return true;
    }

    void Jump(){

        Vector3 jumpDirection;
        if(Time.time - lastGroundedTime <= jumpButtonGracePeriod){
            jumpDirection = contactNormal;
        }
        else if (OnSteep){
            jumpDirection = steepNormal;
            jumpPhase = 0;
        }
        else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps){
            if(jumpPhase == 0){
                jumpPhase = 1;
            }
            jumpDirection = contactNormal;
        }
        else{
            return;
        }

        if (OnGround || jumpPhase < maxAirJumps){

            stepsSinceLastJump = 0;
            jumpPhase += 1;
            float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
            jumpDirection = (jumpDirection + Vector3.up).normalized;
            float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
            if (alignedSpeed > 0f){
                jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
            }

            // Just have vertical jump
            //velocity += Vector3.up * jumpSpeed;

            velocity += jumpDirection * jumpSpeed;
        }
 
        playerAnimator.SetTrigger("IsJumping");

        isJumping = true;

        jumpButtonPressedTime = null;
        lastGroundedTime = null;
    }

    void OnCollisionEnter(Collision collision) {
        EvaluateCollision(collision);
    }
    void OnCollisionStay(Collision collision) {
        EvaluateCollision(collision);
    }

    void EvaluateCollision(Collision collision){
        float minDot = GetMinDot(collision.gameObject.layer);
        for (int i = 0; i < collision.contactCount; i++){
            Vector3 normal = collision.GetContact(i).normal;
            if (normal.y >= minDot){
                groundContactCount += 1;
                contactNormal += normal;
                connectedBody = collision.rigidbody;
            }
            else if (normal.y > -0.01f){
                steepContactCount += 1;
                steepNormal += normal;
                if(groundContactCount == 0){
                    connectedBody = collision.rigidbody;
                }
            }
        }
    }

    Vector3 ProjectOnContactPlane(Vector3 vector){
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }

    void AdjustVelocity(){

        float acceleration, speed;

        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        Vector3 relativeVelocity = velocity - connectionVelocity;

        float currentX = Vector3.Dot(relativeVelocity, xAxis);
        float currentZ = Vector3.Dot(relativeVelocity, zAxis);



        if (InWater){ 
			float swimFactor = Mathf.Min(1f, submergence / swimThreshold);
			acceleration = Mathf.LerpUnclamped(
				OnGround ? maxAcceleration : maxAirAcceleration,
				maxSwimAcceleration, swimFactor
			);
			speed = Mathf.LerpUnclamped(maxSpeed, maxSwimSpeed, swimFactor);
			speed = maxSwimSpeed;
        
        }
        else{ 
        	acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
            speed = maxSpeed;
        }

		float maxSpeedChange = acceleration * Time.deltaTime;

		float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
		float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }

    float GetMinDot (int layer){
        return (stairsMask & (1 << layer)) == 0 ? minGroundDotProduct : minStairsDotProduct;
    }

    bool CheckSteepContacts(){
        if(steepContactCount > 1){
            steepNormal.Normalize();
            if (steepNormal.y >= minGroundDotProduct){
                groundContactCount = 1;
                contactNormal = steepNormal;
                return true;
            }
        }
        return false;
    }

    public void PreventSnapToGround(){
        stepsSinceLastJump = -1;
    }

	void OnTriggerEnter (Collider other) {
		if ((waterMask & (1 << other.gameObject.layer)) != 0) {
			EvaluateSubmergence();
		}
	}

	void OnTriggerStay (Collider other) {
		if ((waterMask & (1 << other.gameObject.layer)) != 0) {
			EvaluateSubmergence();
		}
	}

	void EvaluateSubmergence () {
		if (Physics.Raycast(
			body.position + upAxis * submergenceOffset,
			-upAxis, out RaycastHit hit, submergenceRange + 1f,
			waterMask, QueryTriggerInteraction.Collide
		)) {
			submergence = 1f - hit.distance / submergenceRange;
		}
        else {
			submergence = 1f;
		}
	}

    bool CheckSwimming () {
		if (Swimming) {
			groundContactCount = 0;
			contactNormal = upAxis;
			return true;
		}
		return false;
	}
}
