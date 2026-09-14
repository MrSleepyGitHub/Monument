using UnityEngine;

public class DeathCameraTracker : MonoBehaviour
{
	public Transform target;
	public float lookSmoothSpeed = 5f;

	private void LateUpdate()
	{
		if (target == null) return;

		Vector3 dir = (target.position - transform.position).normalized;
		if (dir != Vector3.zero)
		{
			Quaternion targetRot = Quaternion.LookRotation(dir);
			transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * lookSmoothSpeed);
		}
	}
}