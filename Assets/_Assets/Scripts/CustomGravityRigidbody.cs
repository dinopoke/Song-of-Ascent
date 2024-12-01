using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CustomGravityRigidbody : MonoBehaviour {

	Rigidbody body;
	float floatDelay;

	[SerializeField]
	bool floatToSleep = false;

	[SerializeField]
	float submergenceOffset = 0.5f;

	[SerializeField, Min(0.1f)]
	float submergenceRange = 1f;

	[SerializeField, Min(0f)]
	float buoyancy = 1.5f;

	[SerializeField, Range(0f, 10f)]
	float waterDrag = 2f;

	[SerializeField]
	LayerMask waterMask = 0;

	float submergence;

	[SerializeField]
	Vector3 buoyancyOffset = Vector3.zero;

	Vector3 gravity;
	void Awake () {
		body = GetComponent<Rigidbody>();
		body.useGravity = false;
	}

	void FixedUpdate () {
		if (floatToSleep) {
			if (body.IsSleeping()) {
				floatDelay = 0f;
				return;
			}
			if (body.linearVelocity.sqrMagnitude < 0.0001f) {
				floatDelay += Time.deltaTime;
				if (floatDelay >= 1f) {
					return;
				}
			}
		}
		else {
			floatDelay = 0f;
		}

		gravity = Physics.gravity;
		if (submergence > 0f) {
			float drag = Mathf.Max(0f, 1f - waterDrag * submergence * Time.deltaTime);
			body.linearVelocity *= drag;
			body.angularVelocity *= drag;
			body.AddForceAtPosition(
				gravity * -(buoyancy * submergence),
				transform.TransformPoint(buoyancyOffset),
				ForceMode.Acceleration
			);
			submergence = 0f;
		}
		body.AddForce(gravity, ForceMode.Acceleration);
	}
	void OnTriggerEnter (Collider other) {
		if ((waterMask & (1 << other.gameObject.layer)) != 0) {
			EvaluateSubmergence();
		}
	}

	void OnTriggerStay (Collider other) {
		if (!body.IsSleeping() && (waterMask & (1 << other.gameObject.layer)) != 0) {
			EvaluateSubmergence();
		}
	}
	
	void EvaluateSubmergence () {
		Vector3 upAxis = -gravity.normalized;
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
}