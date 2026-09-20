using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public abstract class Vehicle : MonoBehaviour
{
	[Header("Vehicle State")]
	public bool isOccupied = false;

	[Header("Base Specifications")]
	public float vehicleWeight = 1500f;

	[Header("Interaction & Seating")]
	public Transform playerSeat;
	public Transform playerSeatExit;

	[Header("Exit Clearance Failsafe")]
	public LayerMask exitObstacleMask = ~0;
	public float exitClearanceRadius = 0.45f;
	public float exitClearanceHeight = 1.8f;

	[Header("Dedicated Camera Rig")]
	public Camera vehicleCamera;
	public Transform cameraGimbal;
	public Transform cameraBoom;
	public Transform cockpitCameraPoint;
	public bool isFirstPerson = false;

	[Header("Camera Zoom (3rd Person)")]
	public float minZoom = 2f;
	public float maxZoom = 15f;
	public float zoomSpeed = 1f;
	private float currentZoom = 6f;

	[Header("Camera Free Look")]
	public float mouseLookSens = 0.1f;
	public float joystickLookSens = 150f;
	public float snapBackSpeed = 10f;

	public float defaultThirdPersonPitch = 15f;
	public float defaultThirdPersonYaw = 0f;

	private float cameraYaw = 0f;
	private float cameraPitch = 0f;

	private float camScroll;
	private bool camFreeLook;
	private Vector2 camMouseLook;
	private Vector2 camJoystickLook;

	public Rigidbody rb { get; protected set; }
	public playerController currentPlayer { get; protected set; }
	private Collider[] vehicleColliders;

	protected virtual void Awake()
	{
		rb = GetComponent<Rigidbody>();
		if (rb != null) rb.mass = vehicleWeight;

		vehicleColliders = GetComponentsInChildren<Collider>();

		if (vehicleCamera != null)
		{
			vehicleCamera.gameObject.SetActive(isOccupied);
		}
	}

	public void SetCameraInputs(float scroll, bool freeLook, Vector2 mouseLook, Vector2 joystickLook)
	{
		camScroll = scroll;
		camFreeLook = freeLook;
		camMouseLook = mouseLook;
		camJoystickLook = joystickLook;
	}

	public void ToggleCamera()
	{
		isFirstPerson = !isFirstPerson;
		cameraPitch = 0f;
		cameraYaw = 0f;
		UpdateCameraRigPlacement();
	}

	public virtual void EnterVehicle(playerController player)
	{
		if (isOccupied || player == null || player.CurrentMotor == null) return;

		isOccupied = true;
		currentPlayer = player;

		humanoidMotor motor = player.CurrentMotor;

		// Mount the humanoid motor entity to the seat
		motor.transform.SetParent(playerSeat);
		motor.transform.localPosition = Vector3.zero;
		motor.transform.localRotation = Quaternion.identity;

		motor.ResetLookRotation();
		currentPlayer.DisableForVehicle(this);

		if (vehicleCamera != null)
		{
			vehicleCamera.gameObject.SetActive(true);
		}

		currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
		cameraPitch = 0f;
		cameraYaw = 0f;

		UpdateCameraRigPlacement();
	}

	public virtual void ExitVehicle()
	{
		if (!isOccupied || currentPlayer == null) return;

		isOccupied = false;

		humanoidMotor motor = currentPlayer.CurrentMotor;
		Vector3 targetExitPosition = DetermineSafeExitPosition();

		if (motor != null)
		{
			motor.transform.SetParent(null);
			motor.transform.position = targetExitPosition;

			Vector3 exitForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
			if (exitForward != Vector3.zero)
			{
				motor.transform.rotation = Quaternion.LookRotation(exitForward, Vector3.up);
			}
			else
			{
				motor.transform.rotation = Quaternion.Euler(0f, playerSeatExit != null ? playerSeatExit.eulerAngles.y : transform.eulerAngles.y, 0f);
			}

			motor.ResetLookRotation();
		}

		if (vehicleCamera != null)
		{
			vehicleCamera.gameObject.SetActive(false);
		}

		currentPlayer.EnableFromVehicle();
		currentPlayer = null;
	}

	protected virtual void LateUpdate()
	{
		if (!isOccupied || vehicleCamera == null) return;
		ApplyCameraPhysics();
	}

	private void ApplyCameraPhysics()
	{
		float scroll = camScroll;
		if (scroll == 0f && Mouse.current != null)
		{
			scroll = Mouse.current.scroll.ReadValue().y;
		}

		if (!isFirstPerson && Mathf.Abs(scroll) > 0.01f)
		{
			currentZoom -= Mathf.Sign(scroll) * zoomSpeed;
			currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
		}

		if (camFreeLook)
		{
			float yawOffset = (camMouseLook.x * mouseLookSens) + (camJoystickLook.x * joystickLookSens * Time.deltaTime);
			float pitchOffset = -(camMouseLook.y * mouseLookSens) - (camJoystickLook.y * joystickLookSens * Time.deltaTime);

			cameraYaw += yawOffset;
			cameraPitch += pitchOffset;

			float maxUp = -85f - (isFirstPerson ? 0f : defaultThirdPersonPitch);
			float maxDown = 85f - (isFirstPerson ? 0f : defaultThirdPersonPitch);
			cameraPitch = Mathf.Clamp(cameraPitch, maxUp, maxDown);
		}
		else
		{
			cameraYaw = Mathf.Lerp(cameraYaw, 0f, Time.deltaTime * snapBackSpeed);
			cameraPitch = Mathf.Lerp(cameraPitch, 0f, Time.deltaTime * snapBackSpeed);
		}

		if (isFirstPerson)
		{
			if (cockpitCameraPoint != null)
			{
				if (vehicleCamera.transform.parent != cockpitCameraPoint)
				{
					vehicleCamera.transform.SetParent(cockpitCameraPoint);
					vehicleCamera.transform.localPosition = Vector3.zero;
				}
				vehicleCamera.transform.localRotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
			}
		}
		else
		{
			if (cameraBoom != null)
			{
				cameraBoom.localRotation = Quaternion.Euler(defaultThirdPersonPitch + cameraPitch, defaultThirdPersonYaw + cameraYaw, 0f);

				if (vehicleCamera.transform.parent != cameraBoom)
				{
					vehicleCamera.transform.SetParent(cameraBoom);
					vehicleCamera.transform.localRotation = Quaternion.identity;
				}

				vehicleCamera.transform.localPosition = new Vector3(0f, 0f, -currentZoom);
			}
		}
	}

	private void UpdateCameraRigPlacement()
	{
		if (vehicleCamera == null) return;

		if (isFirstPerson && cockpitCameraPoint != null)
		{
			vehicleCamera.transform.SetParent(cockpitCameraPoint);
			vehicleCamera.transform.localPosition = Vector3.zero;
			vehicleCamera.transform.localRotation = Quaternion.identity;
		}
		else if (!isFirstPerson && cameraBoom != null)
		{
			vehicleCamera.transform.SetParent(cameraBoom);
			vehicleCamera.transform.localPosition = new Vector3(0f, 0f, -currentZoom);
			vehicleCamera.transform.localRotation = Quaternion.identity;
		}
	}

	private Vector3 DetermineSafeExitPosition()
	{
		float radius = exitClearanceRadius;
		float height = exitClearanceHeight;
		LayerMask mask = exitObstacleMask;

		if (currentPlayer != null && currentPlayer.CurrentMotor != null && currentPlayer.CurrentMotor.playerCollider != null)
		{
			radius = currentPlayer.CurrentMotor.playerCollider.radius * 0.95f;
			height = currentPlayer.CurrentMotor.standingHeight;
			mask = currentPlayer.CurrentMotor.groundMask;
		}

		Vector3 primaryExit = playerSeatExit != null ? playerSeatExit.position : transform.position + transform.right * 2f;
		if (IsPointUnobstructed(primaryExit, radius, height, mask)) return primaryExit;

		Vector3 localSeatExit = playerSeatExit != null ? transform.InverseTransformPoint(playerSeatExit.position) : new Vector3(2f, 0f, 0f);
		Vector3 mirroredExit = transform.TransformPoint(new Vector3(-localSeatExit.x, localSeatExit.y, localSeatExit.z));
		if (IsPointUnobstructed(mirroredExit, radius, height, mask)) return mirroredExit;

		Vector3 topOfVehicle = transform.position + Vector3.up * (height * 0.8f);
		if (IsPointUnobstructed(topOfVehicle, radius, height, mask)) return topOfVehicle;

		return transform.position + Vector3.up * 1.5f;
	}

	private bool IsPointUnobstructed(Vector3 origin, float radius, float height, LayerMask mask)
	{
		Vector3 p0 = origin + Vector3.up * radius;
		Vector3 p1 = origin + Vector3.up * (height - radius);

		Collider[] hits = Physics.OverlapCapsule(p0, p1, radius, mask, QueryTriggerInteraction.Ignore);
		foreach (Collider hit in hits)
		{
			bool isOwnCollider = false;
			if (vehicleColliders != null)
			{
				for (int i = 0; i < vehicleColliders.Length; i++)
				{
					if (hit == vehicleColliders[i])
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