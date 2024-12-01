using UnityEngine;

public class AccelerationZone : MonoBehaviour {

	[SerializeField, Min(0f)]
	float acceleration = 50f, speed = 50f;

	void OnTriggerEnter (Collider other) {
		Rigidbody body = other.attachedRigidbody;
		if (body) {
			Accelerate(body);
		}
	}

	void OnTriggerStay (Collider other) {
		Rigidbody body = other.attachedRigidbody;
		if (body) {
			Accelerate(body);
		}
	}

	void Accelerate(Rigidbody body) {
		Vector3 velocity = transform.InverseTransformDirection(body.linearVelocity);
		if (velocity.y >= speed) {
			//return;
		}

		if (acceleration > 0f) {
			velocity.y = Mathf.MoveTowards(
				velocity.y, speed, acceleration * Time.deltaTime
			);
		}
		else {
			velocity.y = speed;
		}

		if (body.TryGetComponent(out PlayerMovement player)) {
            if(player.isGliding){
		        body.linearVelocity = transform.TransformDirection(velocity);
            }
			player.PreventSnapToGround();
		}
        else{
		    body.linearVelocity = transform.TransformDirection(velocity);
        }
	}
}