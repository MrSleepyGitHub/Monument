using UnityEngine;

[System.Serializable]
public class VehicleSeat
{
	public enum SeatType { Driver, Passenger, Gunner }

	[Header("Seat Configuration")]
	public string seatName = "Driver Seat";
	public SeatType seatType = SeatType.Driver;
	public Transform seatTransform;
	public Transform[] exitPoints;

	[Header("Cameras & Views")]
	public GameObject seatCameraRig;
	public Transform cockpitViewPoint;

	[Header("Occupant State")]
	public humanoidMotor currentOccupant;
	public bool isOccupied => currentOccupant != null;

	public void Mount(humanoidMotor motor)
	{
		currentOccupant = motor;

		// Decouple pawn physics while seated
		Rigidbody pawnRb = motor.GetComponent<Rigidbody>();
		Collider pawnCol = motor.GetComponent<Collider>();

		if (pawnRb != null)
		{
			pawnRb.linearVelocity = Vector3.zero;
			pawnRb.angularVelocity = Vector3.zero;
			pawnRb.isKinematic = true;
			pawnRb.detectCollisions = false;
		}

		if (pawnCol != null)
		{
			pawnCol.enabled = false;
		}

		// Align and parent physical motor to vehicle seat
		motor.transform.SetParent(seatTransform);
		motor.transform.localPosition = Vector3.zero;
		motor.transform.localRotation = Quaternion.identity;

		// Signal animator for vehicle sitting pose
		Animator animator = motor.GetComponentInChildren<Animator>();
		if (animator != null)
		{
			animator.SetBool("InVehicle", true);
		}

		if (seatCameraRig != null)
		{
			seatCameraRig.SetActive(true);
		}
	}

	public void Dismount(Vector3 exitPosition, Quaternion exitRotation)
	{
		if (currentOccupant == null) return;

		humanoidMotor motor = currentOccupant;
		currentOccupant = null;

		// Unparent back to world root
		motor.transform.SetParent(null);
		motor.transform.position = exitPosition;
		motor.transform.rotation = exitRotation;

		// Restore dynamic locomotion physics
		Rigidbody pawnRb = motor.GetComponent<Rigidbody>();
		Collider pawnCol = motor.GetComponent<Collider>();

		if (pawnRb != null)
		{
			pawnRb.isKinematic = false;
			pawnRb.detectCollisions = true;
			pawnRb.linearVelocity = Vector3.zero;
		}

		if (pawnCol != null)
		{
			pawnCol.enabled = true;
		}

		Animator animator = motor.GetComponentInChildren<Animator>();
		if (animator != null)
		{
			animator.SetBool("InVehicle", false);
		}

		if (seatCameraRig != null)
		{
			seatCameraRig.SetActive(false);
		}
	}
}