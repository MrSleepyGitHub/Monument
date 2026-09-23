using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public abstract class Vehicle : MonoBehaviour
{
	public enum SeatRole { Driver, Passenger, Gunner }

	[Serializable]
	public class VehicleSeat
	{
		public string seatName = "Driver Seat";
		public SeatRole role = SeatRole.Driver;
		public Transform seatTransform;
		public Transform primaryExitPoint;
		public Transform[] fallbackExitPoints;
		public Transform cockpitCameraPoint;

		[Header("Seat Permissions")]
		public bool allowDriverWeapons = false;
		public bool disallowWeapons = false;

		[Header("Seat Camera State")]
		public bool isFirstPerson = true;
		public float cameraYaw = 0f;
		public float cameraPitch = 0f;

		[Header("Cockpit Look Clamping")]
		public float minCockpitPitch = -70f;
		public float maxCockpitPitch = 70f;
		public float minCockpitYaw = -85f;
		public float maxCockpitYaw = 85f;

		[HideInInspector] public humanoidMotor currentOccupant;
		[HideInInspector] public playerController possessingPlayer;

		public Camera occupantCamera { get; private set; }
		private Transform originalCamParent;
		private Vector3 originalCamLocalPos;
		private Quaternion originalCamLocalRot;
		private int originalCullingMask;
		private float originalNearClip;

		private readonly List<Collider> disabledColliders = new List<Collider>();
		private RigidbodyInterpolation cachedInterpolation = RigidbodyInterpolation.None;

		public bool isOccupied => currentOccupant != null;

		public void Mount(humanoidMotor motor, playerController player, Collider[] vehicleColliders)
		{
			currentOccupant = motor;
			possessingPlayer = player;
			isFirstPerson = true;
			cameraYaw = 0f;
			cameraPitch = 0f;

			// 1. Suspend pawn dynamic locomotion
			Rigidbody pawnRb = motor.GetComponent<Rigidbody>();
			if (pawnRb != null)
			{
				cachedInterpolation = pawnRb.interpolation;
				pawnRb.interpolation = RigidbodyInterpolation.None;

				if (!pawnRb.isKinematic)
				{
					pawnRb.linearVelocity = Vector3.zero;
					pawnRb.angularVelocity = Vector3.zero;
					pawnRb.isKinematic = true;
				}
				pawnRb.detectCollisions = false;
			}

			// 2. Disable all pawn colliders and suppress self-collision with vehicle hull
			disabledColliders.Clear();
			Collider[] pawnColliders = motor.GetComponentsInChildren<Collider>(true);
			for (int i = 0; i < pawnColliders.Length; i++)
			{
				Collider pCol = pawnColliders[i];
				if (pCol == null) continue;

				if (vehicleColliders != null)
				{
					for (int j = 0; j < vehicleColliders.Length; j++)
					{
						if (vehicleColliders[j] != null)
						{
							Physics.IgnoreCollision(pCol, vehicleColliders[j], true);
						}
					}
				}

				if (pCol.enabled)
				{
					disabledColliders.Add(pCol);
					pCol.enabled = false;
				}
			}

			// 3. Parent pawn to seat anchor
			Transform targetAnchor = seatTransform != null ? seatTransform : motor.transform;
			motor.transform.SetParent(targetAnchor);
			motor.transform.localPosition = Vector3.zero;
			motor.transform.localRotation = Quaternion.identity;

			motor.inVehicle = true;
			motor.ResetLookRotation();

			Animator anim = motor.GetComponentInChildren<Animator>();
			if (anim != null) SetAnimatorBoolIfExists(anim, "InVehicle", true);

			// 4. Reparent pawn body camera to cockpit anchor
			occupantCamera = motor.playerCamera;
			if (occupantCamera != null)
			{
				originalCamParent = occupantCamera.transform.parent;
				originalCamLocalPos = occupantCamera.transform.localPosition;
				originalCamLocalRot = occupantCamera.transform.localRotation;
				originalCullingMask = occupantCamera.cullingMask;
				originalNearClip = occupantCamera.nearClipPlane;

				int tpLayer = LayerMask.NameToLayer("LocalPlayer_TP");
				if (tpLayer != -1) occupantCamera.cullingMask |= (1 << tpLayer);

				occupantCamera.nearClipPlane = 0.05f;

				Transform camAnchor = cockpitCameraPoint != null ? cockpitCameraPoint : seatTransform;
				if (camAnchor != null)
				{
					occupantCamera.transform.SetParent(camAnchor);
					occupantCamera.transform.localPosition = Vector3.zero;
					occupantCamera.transform.localRotation = Quaternion.identity;
				}

				occupantCamera.gameObject.SetActive(true);
				motor.SetCameraActive(true);
			}

			if (possessingPlayer != null)
			{
				possessingPlayer.DisableForVehicle(seatTransform != null ? seatTransform.GetComponentInParent<Vehicle>() : null);
			}
		}

		public void Dismount(Vector3 exitPosition, Quaternion exitRotation, Collider[] vehicleColliders, Vector3 inheritedVelocity)
		{
			if (currentOccupant == null) return;

			humanoidMotor motor = currentOccupant;
			playerController player = possessingPlayer;

			// 1. Restore pawn camera
			if (occupantCamera != null)
			{
				occupantCamera.transform.SetParent(originalCamParent);
				occupantCamera.transform.localPosition = originalCamLocalPos;
				occupantCamera.transform.localRotation = originalCamLocalRot;
				occupantCamera.cullingMask = originalCullingMask;
				occupantCamera.nearClipPlane = originalNearClip;
				occupantCamera = null;
			}

			currentOccupant = null;
			possessingPlayer = null;

			// 2. Unparent pawn
			motor.transform.SetParent(null);
			motor.transform.position = exitPosition;
			motor.transform.rotation = exitRotation;

			// 3. Re-enable colliders and collision detection
			for (int i = 0; i < disabledColliders.Count; i++)
			{
				if (disabledColliders[i] != null)
				{
					disabledColliders[i].enabled = true;
					if (vehicleColliders != null)
					{
						for (int j = 0; j < vehicleColliders.Length; j++)
						{
							if (vehicleColliders[j] != null)
							{
								Physics.IgnoreCollision(disabledColliders[i], vehicleColliders[j], false);
							}
						}
					}
				}
			}
			disabledColliders.Clear();

			if (motor.playerCollider != null) motor.playerCollider.enabled = true;

			// 4. Restore dynamic physics and transfer vehicle momentum
			Rigidbody pawnRb = motor.GetComponent<Rigidbody>();
			if (pawnRb != null)
			{
				pawnRb.isKinematic = false;
				pawnRb.detectCollisions = true;
				pawnRb.interpolation = cachedInterpolation;
				pawnRb.linearVelocity = inheritedVelocity;
				pawnRb.angularVelocity = Vector3.zero;
			}

			motor.inVehicle = false;
			motor.SetCameraActive(player != null);
			motor.ResetLookRotation();

			Animator anim = motor.GetComponentInChildren<Animator>();
			if (anim != null) SetAnimatorBoolIfExists(anim, "InVehicle", false);

			if (player != null) player.EnableFromVehicle();
		}

		private static void SetAnimatorBoolIfExists(Animator anim, string paramName, bool value)
		{
			if (anim == null) return;
			for (int i = 0; i < anim.parameterCount; i++)
			{
				AnimatorControllerParameter param = anim.parameters[i];
				if (param.type == AnimatorControllerParameterType.Bool && param.name == paramName)
				{
					anim.SetBool(paramName, value);
					return;
				}
			}
		}
	}

	[Header("Vehicle State")]
	public bool isOccupied = false;

	[Header("Base Specifications")]
	public float vehicleWeight = 1500f;

	[Header("Multi-Seat Management")]
	[SerializeField] protected VehicleSeat[] seats;

	[Header("Legacy Interaction Anchors (Fallback)")]
	public Transform playerSeat;
	public Transform playerSeatExit;

	[Header("Exit Clearance Failsafe")]
	public LayerMask exitObstacleMask = ~0;
	public float exitClearanceRadius = 0.45f;
	public float exitClearanceHeight = 1.8f;

	[Header("Dedicated Camera Rig (3rd Person Driver Boom)")]
	public Camera vehicleCamera;
	public Transform cameraGimbal;
	public Transform cameraBoom;
	public Transform cockpitCameraPoint;
	public bool isFirstPerson = true;

	[Header("Camera Zoom (3rd Person)")]
	public float minZoom = 2f;
	public float maxZoom = 15f;
	public float zoomSpeed = 1f;
	private float currentZoom = 6f;

	[Header("Camera Sensitivity")]
	public float mouseLookSens = 1.5f;
	public float joystickLookSens = 150f;
	public float snapBackSpeed = 10f;

	public float defaultThirdPersonPitch = 15f;
	public float defaultThirdPersonYaw = 0f;

	private float thirdPersonOrbitYaw = 0f;
	private float thirdPersonOrbitPitch = 0f;

	public Rigidbody rb { get; protected set; }
	public playerController currentPlayer { get; protected set; }
	protected Collider[] vehicleColliders;

	public VehicleSeat[] Seats => seats;

	protected virtual void Awake()
	{
		rb = GetComponent<Rigidbody>();
		if (rb != null)
		{
			rb.mass = vehicleWeight;
			rb.maxDepenetrationVelocity = 4.0f; // Mitigates explosive physics launches
		}

		vehicleColliders = GetComponentsInChildren<Collider>(true);
		InitializeSeats();

		if (vehicleCamera != null)
		{
			int tpLayer = LayerMask.NameToLayer("LocalPlayer_TP");
			if (tpLayer != -1) vehicleCamera.cullingMask |= (1 << tpLayer);
			vehicleCamera.gameObject.SetActive(false);
		}
	}

	private void InitializeSeats()
	{
		if (seats == null || seats.Length == 0)
		{
			seats = new VehicleSeat[1];
			seats[0] = new VehicleSeat
			{
				seatName = "Driver Seat",
				role = SeatRole.Driver,
				seatTransform = playerSeat != null ? playerSeat : transform,
				primaryExitPoint = playerSeatExit,
				cockpitCameraPoint = cockpitCameraPoint,
				isFirstPerson = true
			};
		}
		else
		{
			if (playerSeat == null && seats[0].seatTransform != null) playerSeat = seats[0].seatTransform;
			if (playerSeatExit == null && seats[0].primaryExitPoint != null) playerSeatExit = seats[0].primaryExitPoint;
			if (cockpitCameraPoint == null && seats[0].cockpitCameraPoint != null) cockpitCameraPoint = seats[0].cockpitCameraPoint;
		}
	}

	public int GetSeatIndex(humanoidMotor motor)
	{
		if (motor == null || seats == null) return -1;
		for (int i = 0; i < seats.Length; i++)
		{
			if (seats[i].currentOccupant == motor) return i;
		}
		return -1;
	}

	public bool IsSeatOccupied(int seatIndex)
	{
		if (seats == null || seatIndex < 0 || seatIndex >= seats.Length) return false;
		return seats[seatIndex].isOccupied;
	}

	public bool IsDriverSeatOccupied() => IsSeatOccupied(0);

	public bool CanSeatUseWeapons(int seatIndex)
	{
		if (seats == null || seatIndex < 0 || seatIndex >= seats.Length) return false;
		VehicleSeat seat = seats[seatIndex];

		if (seatIndex == 0 && seat.role == SeatRole.Driver) return seat.allowDriverWeapons;
		return !seat.disallowWeapons;
	}

	public void IgnoreCollisionWithVehicle(Collider col, bool ignore = true)
	{
		if (col == null || vehicleColliders == null) return;
		for (int i = 0; i < vehicleColliders.Length; i++)
		{
			if (vehicleColliders[i] != null) Physics.IgnoreCollision(col, vehicleColliders[i], ignore);
		}
	}

	public virtual bool Board(humanoidMotor motor, int seatIndex = 0)
	{
		if (motor == null || seatIndex < 0 || seatIndex >= seats.Length) return false;
		if (seats[seatIndex].isOccupied) return false;

		playerController pc = ResolvePlayerController(motor);
		seats[seatIndex].Mount(motor, pc, vehicleColliders);

		if (seatIndex == 0)
		{
			isOccupied = true;
			currentPlayer = pc;
			isFirstPerson = true;
		}

		UpdateSeatCameraMode(seatIndex);
		OnSeatStateChanged(seatIndex, motor, true);
		return true;
	}

	public virtual bool BoardAnyAvailableSeat(humanoidMotor motor)
	{
		for (int i = 0; i < seats.Length; i++)
		{
			if (!seats[i].isOccupied) return Board(motor, i);
		}
		return false;
	}

	public virtual void EnterVehicle(playerController player)
	{
		if (player == null || player.CurrentMotor == null) return;
		Board(player.CurrentMotor, 0);
	}

	public virtual void ExitOccupant(humanoidMotor motor)
	{
		int seatIndex = GetSeatIndex(motor);
		if (seatIndex != -1) ExitVehicle(seatIndex);
	}

	public virtual void ExitVehicle(int seatIndex)
	{
		if (seats == null || seatIndex < 0 || seatIndex >= seats.Length) return;
		if (!seats[seatIndex].isOccupied) return;

		humanoidMotor occupant = seats[seatIndex].currentOccupant;
		Vector3 targetExitPosition = DetermineSafeExitPosition(seatIndex);

		Vector3 exitForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
		Quaternion targetExitRotation = (exitForward != Vector3.zero)
			? Quaternion.LookRotation(exitForward, Vector3.up)
			: Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

		Vector3 inheritedVelocity = Vector3.zero;
		if (rb != null)
		{
			inheritedVelocity = rb.GetPointVelocity(targetExitPosition);
			if (inheritedVelocity.y < 0f) inheritedVelocity.y = 0f;
		}

		seats[seatIndex].Dismount(targetExitPosition, targetExitRotation, vehicleColliders, inheritedVelocity);

		if (seatIndex == 0)
		{
			isOccupied = false;
			currentPlayer = null;
			if (vehicleCamera != null) vehicleCamera.gameObject.SetActive(false);
		}

		OnSeatStateChanged(seatIndex, occupant, false);
	}

	public virtual void ExitVehicle()
	{
		if (currentPlayer != null && currentPlayer.CurrentMotor != null)
			ExitOccupant(currentPlayer.CurrentMotor);
		else
			ExitVehicle(0);
	}

	public virtual void ExitAll()
	{
		for (int i = 0; i < seats.Length; i++)
		{
			if (seats[i].isOccupied) ExitVehicle(i);
		}
	}

	protected virtual void OnSeatStateChanged(int seatIndex, humanoidMotor motor, bool boarded) { }

	private playerController ResolvePlayerController(humanoidMotor motor)
	{
		if (PlayerMaster.Instance != null && PlayerMaster.Instance.playerController != null)
		{
			if (PlayerMaster.Instance.playerController.CurrentMotor == motor)
				return PlayerMaster.Instance.playerController;
		}

		playerController localController = motor.GetComponentInParent<playerController>();
		if (localController != null) return localController;

		return motor.GetComponent<playerController>();
	}

	public void ToggleCamera(humanoidMotor motor)
	{
		int seatIndex = GetSeatIndex(motor);
		ToggleCamera(seatIndex != -1 ? seatIndex : 0);
	}

	public void ToggleCamera(int seatIndex)
	{
		if (seats == null || seatIndex < 0 || seatIndex >= seats.Length) return;
		VehicleSeat seat = seats[seatIndex];

		// Only driver seat (seat 0) can enter third-person exterior chase view
		if (seatIndex != 0 || seat.role != SeatRole.Driver) return;

		seat.isFirstPerson = !seat.isFirstPerson;
		isFirstPerson = seat.isFirstPerson;

		seat.cameraPitch = 0f;
		seat.cameraYaw = 0f;
		thirdPersonOrbitPitch = 0f;
		thirdPersonOrbitYaw = 0f;

		UpdateSeatCameraMode(seatIndex);
	}

	public void ToggleCamera()
	{
		ToggleCamera(currentPlayer != null ? currentPlayer.CurrentMotor : null);
	}

	private void UpdateSeatCameraMode(int seatIndex)
	{
		if (seats == null || seatIndex < 0 || seatIndex >= seats.Length) return;
		VehicleSeat seat = seats[seatIndex];
		if (!seat.isOccupied) return;

		if (seatIndex != 0)
		{
			seat.isFirstPerson = true;
			if (seat.occupantCamera != null) seat.occupantCamera.gameObject.SetActive(true);
			return;
		}

		if (seat.isFirstPerson)
		{
			if (vehicleCamera != null) vehicleCamera.gameObject.SetActive(false);
			if (seat.occupantCamera != null) seat.occupantCamera.gameObject.SetActive(true);
		}
		else
		{
			if (seat.occupantCamera != null) seat.occupantCamera.gameObject.SetActive(false);
			if (vehicleCamera != null)
			{
				vehicleCamera.gameObject.SetActive(true);
				if (cameraBoom != null && vehicleCamera.transform.parent != cameraBoom)
				{
					vehicleCamera.transform.SetParent(cameraBoom);
					vehicleCamera.transform.localPosition = new Vector3(0f, 0f, -currentZoom);
					vehicleCamera.transform.localRotation = Quaternion.identity;
				}
			}
		}
	}

	public void SetCameraInputs(humanoidMotor motor, float scroll, bool freeLook, Vector2 mouseLook, Vector2 joystickLook)
	{
		int seatIndex = GetSeatIndex(motor);
		if (seatIndex != -1) ProcessSeatCameraInput(seatIndex, scroll, freeLook, mouseLook, joystickLook);
		else if (currentPlayer != null && currentPlayer.CurrentMotor == motor) ProcessSeatCameraInput(0, scroll, freeLook, mouseLook, joystickLook);
	}

	public void SetCameraInputs(float scroll, bool freeLook, Vector2 mouseLook, Vector2 joystickLook)
	{
		SetCameraInputs(currentPlayer != null ? currentPlayer.CurrentMotor : null, scroll, freeLook, mouseLook, joystickLook);
	}

	private void ProcessSeatCameraInput(int seatIndex, float scroll, bool freeLook, Vector2 mouseLook, Vector2 joystickLook)
	{
		if (seats == null || seatIndex < 0 || seatIndex >= seats.Length) return;
		VehicleSeat seat = seats[seatIndex];
		if (!seat.isOccupied) return;

		float sens = PlayerMaster.Instance != null ? PlayerMaster.Instance.MouseSensitivity : 10f;
		bool isFirstPersonMode = (seatIndex == 0) ? seat.isFirstPerson : true;

		if (isFirstPersonMode)
		{
			float yawDelta = (mouseLook.x * mouseLookSens * sens * 0.02f) + (joystickLook.x * joystickLookSens * Time.deltaTime);
			float pitchDelta = -(mouseLook.y * mouseLookSens * sens * 0.02f) - (joystickLook.y * joystickLookSens * Time.deltaTime);

			seat.cameraYaw = Mathf.Clamp(seat.cameraYaw + yawDelta, seat.minCockpitYaw, seat.maxCockpitYaw);
			seat.cameraPitch = Mathf.Clamp(seat.cameraPitch + pitchDelta, seat.minCockpitPitch, seat.maxCockpitPitch);

			if (seat.occupantCamera != null)
			{
				seat.occupantCamera.transform.localRotation = Quaternion.Euler(seat.cameraPitch, seat.cameraYaw, 0f);
			}
		}
		else if (seatIndex == 0)
		{
			if (Mathf.Abs(scroll) > 0.01f)
			{
				currentZoom = Mathf.Clamp(currentZoom - Mathf.Sign(scroll) * zoomSpeed, minZoom, maxZoom);
			}

			if (freeLook)
			{
				thirdPersonOrbitYaw += (mouseLook.x * mouseLookSens * sens * 0.02f) + (joystickLook.x * joystickLookSens * Time.deltaTime);
				thirdPersonOrbitPitch += -(mouseLook.y * mouseLookSens * sens * 0.02f) - (joystickLook.y * joystickLookSens * Time.deltaTime);
				thirdPersonOrbitPitch = Mathf.Clamp(thirdPersonOrbitPitch, -75f, 75f);
			}
			else
			{
				thirdPersonOrbitYaw = Mathf.Lerp(thirdPersonOrbitYaw, 0f, Time.deltaTime * snapBackSpeed);
				thirdPersonOrbitPitch = Mathf.Lerp(thirdPersonOrbitPitch, 0f, Time.deltaTime * snapBackSpeed);
			}

			if (cameraBoom != null)
			{
				cameraBoom.localRotation = Quaternion.Euler(defaultThirdPersonPitch + thirdPersonOrbitPitch, defaultThirdPersonYaw + thirdPersonOrbitYaw, 0f);
				if (vehicleCamera != null) vehicleCamera.transform.localPosition = new Vector3(0f, 0f, -currentZoom);
			}
		}
	}

	protected virtual Vector3 DetermineSafeExitPosition(int seatIndex)
	{
		float radius = exitClearanceRadius;
		float height = exitClearanceHeight;
		LayerMask mask = exitObstacleMask;

		humanoidMotor occupant = seats[seatIndex].currentOccupant;
		if (occupant != null && occupant.playerCollider != null)
		{
			radius = occupant.playerCollider.radius * 0.95f;
			height = occupant.standingHeight;
			mask = occupant.groundMask;
		}

		VehicleSeat seat = seats[seatIndex];

		if (seat.primaryExitPoint != null && IsPointUnobstructed(seat.primaryExitPoint.position, radius, height, mask))
			return seat.primaryExitPoint.position;

		if (seat.fallbackExitPoints != null)
		{
			for (int i = 0; i < seat.fallbackExitPoints.Length; i++)
			{
				Transform fallback = seat.fallbackExitPoints[i];
				if (fallback != null && IsPointUnobstructed(fallback.position, radius, height, mask))
					return fallback.position;
			}
		}

		Vector3 leftFlank = transform.position - transform.right * 2.2f + Vector3.up * 0.2f;
		if (IsPointUnobstructed(leftFlank, radius, height, mask)) return leftFlank;

		Vector3 rightFlank = transform.position + transform.right * 2.2f + Vector3.up * 0.2f;
		if (IsPointUnobstructed(rightFlank, radius, height, mask)) return rightFlank;

		Vector3 topOfVehicle = transform.position + Vector3.up * (height * 0.9f);
		if (IsPointUnobstructed(topOfVehicle, radius, height, mask)) return topOfVehicle;

		return transform.position + Vector3.up * 1.5f;
	}

	private bool IsPointUnobstructed(Vector3 origin, float radius, float height, LayerMask mask)
	{
		Vector3 p0 = origin + Vector3.up * radius;
		Vector3 p1 = origin + Vector3.up * (height - radius);

		Collider[] hits = Physics.OverlapCapsule(p0, p1, radius, mask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < hits.Length; i++)
		{
			bool isOwnCollider = false;
			if (vehicleColliders != null)
			{
				for (int j = 0; j < vehicleColliders.Length; j++)
				{
					if (hits[i] == vehicleColliders[j])
					{
						isOwnCollider = true;
						break;
					}
				}
			}
			if (!isOwnCollider) return false;
		}

		return true;
	}
}