using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SpectatorMotor : MonoBehaviour
{
	[Header("Camera & Audio")]
	[SerializeField] private Camera spectatorCamera;
	[SerializeField] private AudioListener spectatorListener;

	[Header("Flight Controls")]
	[SerializeField] private float baseFlySpeed = 10f;
	[SerializeField] private float sprintMultiplier = 2.5f;
	[SerializeField] private float slowMultiplier = 0.35f;
	[SerializeField] private float acceleration = 12f;

	[Header("Look Controls")]
	[SerializeField] private float mouseSensitivity = 2f;
	[SerializeField] private Vector2 pitchClamp = new Vector2(-89f, 89f);

	private Vector3 _currentVelocity;
	private float _yaw;
	private float _pitch;

	private void Awake()
	{
		if (spectatorCamera == null)
		{
			spectatorCamera = GetComponentInChildren<Camera>(true);
		}
		if (spectatorListener == null)
		{
			spectatorListener = GetComponentInChildren<AudioListener>(true);
		}

		Vector3 currentEuler = transform.eulerAngles;
		_yaw = currentEuler.y;
		_pitch = currentEuler.x > 180f ? currentEuler.x - 360f : currentEuler.x;
	}

	private void OnEnable()
	{
		if (spectatorCamera != null)
		{
			spectatorCamera.gameObject.SetActive(true);
			spectatorCamera.enabled = true;
		}

		if (spectatorListener != null)
		{
			spectatorListener.enabled = true;
		}
	}

	private void Update()
	{
		HandleLookInput();
		HandleMovementInput();
	}

	private void HandleLookInput()
	{
		// Only capture mouse look if cursor is locked or RMB is held
		bool canLook = Cursor.lockState == CursorLockMode.Locked || (Mouse.current != null && Mouse.current.rightButton.isPressed);
		if (!canLook || Mouse.current == null) return;

		Vector2 delta = Mouse.current.delta.ReadValue();
		float sensMultiplier = (PlayerMaster.Instance != null) ? PlayerMaster.Instance.MouseSensitivity * 0.1f : 1f;

		_yaw += delta.x * mouseSensitivity * sensMultiplier * 0.05f;
		_pitch = Mathf.Clamp(_pitch - delta.y * mouseSensitivity * sensMultiplier * 0.05f, pitchClamp.x, pitchClamp.y);

		transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
	}

	private void HandleMovementInput()
	{
		Vector3 inputDir = Vector3.zero;

		if (Keyboard.current != null)
		{
			if (Keyboard.current.wKey.isPressed) inputDir += transform.forward;
			if (Keyboard.current.sKey.isPressed) inputDir -= transform.forward;
			if (Keyboard.current.dKey.isPressed) inputDir += transform.right;
			if (Keyboard.current.aKey.isPressed) inputDir -= transform.right;
			if (Keyboard.current.eKey.isPressed || Keyboard.current.spaceKey.isPressed) inputDir += Vector3.up;
			if (Keyboard.current.qKey.isPressed || Keyboard.current.cKey.isPressed || Keyboard.current.leftCtrlKey.isPressed) inputDir -= Vector3.up;
		}

		if (inputDir.sqrMagnitude > 1f)
		{
			inputDir.Normalize();
		}

		float currentSpeed = baseFlySpeed;
		if (Keyboard.current != null)
		{
			if (Keyboard.current.leftShiftKey.isPressed) currentSpeed *= sprintMultiplier;
			else if (Keyboard.current.leftAltKey.isPressed) currentSpeed *= slowMultiplier;
		}

		Vector3 targetVelocity = inputDir * currentSpeed;
		_currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, Time.deltaTime * acceleration);
		transform.position += _currentVelocity * Time.deltaTime;
	}

	public void Initialize(Vector3 position, Quaternion rotation)
	{
		transform.position = position;
		transform.rotation = rotation;

		Vector3 currentEuler = rotation.eulerAngles;
		_yaw = currentEuler.y;
		_pitch = currentEuler.x > 180f ? currentEuler.x - 360f : currentEuler.x;
		_currentVelocity = Vector3.zero;
	}
}