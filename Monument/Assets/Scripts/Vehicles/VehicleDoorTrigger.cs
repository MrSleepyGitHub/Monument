using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class VehicleDoorTrigger : MonoBehaviour, IInteractable
{
	[Header("Vehicle Link")]
	[Tooltip("Root vehicle component this door provides access to.")]
	[SerializeField] private Vehicle vehicle;

	[Header("Seat Configuration")]
	[Tooltip("Target seat index (0 = Driver, 1+ = Passengers/Gunners).")]
	[SerializeField] private int targetSeatIndex = 0;

	[Tooltip("If the target seat is already occupied, attempt to board another available seat.")]
	[SerializeField] private bool allowFallbackToAnySeat = true;

	public Vehicle Vehicle => vehicle;
	public int TargetSeatIndex => targetSeatIndex;

	private void Reset()
	{
		Collider col = GetComponent<Collider>();
		if (col != null) col.isTrigger = true;

		if (vehicle == null) vehicle = GetComponentInParent<Vehicle>();

		int interactableLayer = LayerMask.NameToLayer("Interactable");
		if (interactableLayer != -1) gameObject.layer = interactableLayer;
	}

	private void Awake()
	{
		if (vehicle == null) vehicle = GetComponentInParent<Vehicle>();

		Collider col = GetComponent<Collider>();
		if (col != null && !col.isTrigger) col.isTrigger = true;
	}

	public void Interact(GameObject interactor)
	{
		if (vehicle == null || interactor == null) return;

		humanoidMotor motor = interactor.GetComponent<humanoidMotor>();
		if (motor == null) motor = interactor.GetComponentInParent<humanoidMotor>();
		if (motor == null) return;

		if (!vehicle.IsSeatOccupied(targetSeatIndex))
		{
			vehicle.Board(motor, targetSeatIndex);
		}
		else if (allowFallbackToAnySeat)
		{
			vehicle.BoardAnyAvailableSeat(motor);
		}
	}
}