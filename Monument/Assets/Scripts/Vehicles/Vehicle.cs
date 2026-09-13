using System.Collections;
using UnityEngine;

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
	public float cameraTransitionSpeed = 3f;
	protected bool isTransitioning = false;

	[Header("Camera System")]
	public Camera vehicleCamera;
	public Transform cockpitCameraPoint;
	public Transform thirdPersonCameraPoint;
	public bool isFirstPerson = false;

	[Header("Camera Zoom (3rd Person)")]
	public float minZoom = 2f;
	public float maxZoom = 15f;
	public float zoomSpeed = 0.05f;
	private float currentZoom = 8f;

	[Header("Camera Free Look")]
	public float mouseLookSens = 0.1f;
	public float joystickLookSens = 150f;
	public float snapBackSpeed = 10f;

	public float defaultThirdPersonPitch = 15f;
	public float defaultThirdPersonYaw = 0f;

	private float cameraYaw = 0f;
	private float cameraPitch = 0f;

	// Cached camera inputs sent by the controller
	private float camScroll;
	private bool camFreeLook;
	private Vector2 camMouseLook;
	private Vector2 camJoystickLook;

	public Rigidbody rb { get; protected set; }
	public playerController currentPlayer { get; protected set; }

	protected virtual void Awake()
	{
		rb = GetComponent<Rigidbody>();
		if (rb != null) rb.mass = vehicleWeight;

		cameraPitch = defaultThirdPersonPitch;
		cameraYaw = defaultThirdPersonYaw;

		if (vehicleCamera != null) vehicleCamera.gameObject.SetActive(isOccupied);
	}

	// NEW: API endpoint for the vehicle controller
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
	}

	public virtual void EnterVehicle(playerController player)
	{
		if (isOccupied || isTransitioning) return;

		isOccupied = true;
		currentPlayer = player;

		currentPlayer.transform.SetParent(playerSeat);
		currentPlayer.transform.localPosition = Vector3.zero;
		currentPlayer.transform.localRotation = Quaternion.identity;

		if (currentPlayer.motor != null) currentPlayer.motor.ResetLookRotation();

		// Pass this vehicle to the player's controller
		currentPlayer.DisableForVehicle(this);

		if (vehicleCamera != null && currentPlayer.playerCamera != null)
		{
			vehicleCamera.transform.position = currentPlayer.playerCamera.transform.position;
			vehicleCamera.transform.rotation = currentPlayer.playerCamera.transform.rotation;

			currentPlayer.playerCamera.gameObject.SetActive(false);
			vehicleCamera.gameObject.SetActive(true);

			Transform targetPoint = isFirstPerson ? cockpitCameraPoint : thirdPersonCameraPoint;
			StartCoroutine(CameraTransition(targetPoint, vehicleCamera, true));
		}
	}

	public virtual void ExitVehicle()
	{
		if (!isOccupied || isTransitioning) return;

		isOccupied = false;

		currentPlayer.transform.SetParent(null);
		currentPlayer.transform.position = playerSeatExit.position;

		Vector3 exitEuler = playerSeatExit.rotation.eulerAngles;
		currentPlayer.transform.rotation = Quaternion.Euler(0f, exitEuler.y, 0f);

		if (currentPlayer.motor != null) currentPlayer.motor.ResetLookRotation();

		if (vehicleCamera != null && currentPlayer.playerCamera != null)
		{
			currentPlayer.playerCamera.transform.position = vehicleCamera.transform.position;
			currentPlayer.playerCamera.transform.rotation = vehicleCamera.transform.rotation;

			vehicleCamera.gameObject.SetActive(false);
			currentPlayer.playerCamera.gameObject.SetActive(true);

			StartCoroutine(CameraTransition(currentPlayer.motor.cameraHolder, currentPlayer.playerCamera, false));
		}
		else
		{
			FinishExit();
		}
	}

	private void FinishExit()
	{
		if (currentPlayer != null)
		{
			currentPlayer.EnableFromVehicle();
			currentPlayer = null;
		}
	}

	private IEnumerator CameraTransition(Transform targetPoint, Camera activeCam, bool isEntering)
	{
		isTransitioning = true;
		Vector3 startPos = activeCam.transform.position;
		Quaternion startRot = activeCam.transform.rotation;

		float t = 0f;
		while (t < 1f)
		{
			t += Time.deltaTime * cameraTransitionSpeed;
			float smooth = t * t * (3f - 2f * t);

			activeCam.transform.position = Vector3.Lerp(startPos, targetPoint.position, smooth);
			activeCam.transform.rotation = Quaternion.Slerp(startRot, targetPoint.rotation, smooth);

			yield return null;
		}

		activeCam.transform.position = targetPoint.position;
		activeCam.transform.rotation = targetPoint.rotation;
		isTransitioning = false;

		if (!isEntering)
		{
			activeCam.transform.localPosition = Vector3.zero;
			activeCam.transform.localRotation = Quaternion.identity;
			FinishExit();
		}
	}

	protected virtual void LateUpdate()
	{
		if (!isOccupied || vehicleCamera == null || isTransitioning) return;
		ApplyCameraPhysics();
	}

	private void ApplyCameraPhysics()
	{
		if (!isFirstPerson && camScroll != 0f)
		{
			currentZoom -= camScroll * zoomSpeed;
			currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
		}

		if (camFreeLook)
		{
			float yawOffset = (camMouseLook.x * mouseLookSens) + (camJoystickLook.x * joystickLookSens * Time.deltaTime);
			float pitchOffset = -(camMouseLook.y * mouseLookSens) - (camJoystickLook.y * joystickLookSens * Time.deltaTime);

			cameraYaw += yawOffset;
			cameraPitch += pitchOffset;

			float maxUp = -85f - defaultThirdPersonPitch;
			float maxDown = 85f - defaultThirdPersonPitch;
			cameraPitch = Mathf.Clamp(cameraPitch, maxUp, maxDown);
		}
		else
		{
			cameraYaw = Mathf.Lerp(cameraYaw, 0f, Time.deltaTime * snapBackSpeed);
			cameraPitch = Mathf.Lerp(cameraPitch, 0f, Time.deltaTime * snapBackSpeed);
		}

		Transform activePoint = isFirstPerson ? cockpitCameraPoint : thirdPersonCameraPoint;

		if (activePoint != null)
		{
			if (isFirstPerson)
			{
				Quaternion lookRotation = activePoint.rotation * Quaternion.Euler(cameraPitch, cameraYaw, 0f);
				vehicleCamera.transform.rotation = lookRotation;
				vehicleCamera.transform.position = activePoint.position;
			}
			else
			{
				Quaternion baseRotation = transform.rotation;
				Quaternion localRotation = Quaternion.Euler(defaultThirdPersonPitch + cameraPitch, defaultThirdPersonYaw + cameraYaw, 0f);

				vehicleCamera.transform.rotation = baseRotation * localRotation;
				vehicleCamera.transform.position = activePoint.position - (vehicleCamera.transform.forward * currentZoom);
			}
		}
	}
}