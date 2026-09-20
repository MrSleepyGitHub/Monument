using UnityEngine;

public class PlayerMaster : MonoBehaviour
{
	public static PlayerMaster Instance { get; private set; }

	[Header("Sub-System References")]
	public playerController controller;
	public KeybindFramework keybindFramework;

	[Header("Spawning Configuration")]
	public GameObject humanoidPrefab;
	public Weapon startingWeapon;
	public int teamId = 1;
	public bool spawnOnStart = true;

	[Header("Player Settings Data")]
	[SerializeField] private float mouseSensitivity = 10f;
	public float MouseSensitivity => mouseSensitivity;
	public bool IsPaused { get; private set; } = false;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;

		if (controller == null) controller = GetComponentInChildren<playerController>();
		if (keybindFramework == null) keybindFramework = GetComponentInChildren<KeybindFramework>();
	}

	private void Start()
	{
		if (spawnOnStart)
		{
			SpawnPlayer();
		}
	}

	[ContextMenu("Spawn Player Now")]
	public humanoidMotor SpawnPlayer()
	{
		if (controller != null && controller.IsControllingAliveEntity())
		{
			Debug.Log("[PlayerMaster] Player is already controlling an active entity. Spawn aborted.", this);
			return controller.CurrentMotor;
		}

		if (humanoidPrefab == null)
		{
			Debug.LogError("[PlayerMaster] Humanoid Prefab slot is empty! Assign your Humanoid prefab in the Inspector.", this);
			return null;
		}

		SpawnPoint chosenPoint = SpawnPoint.GetAvailableSpawnPoint(SpawnPoint.SpawnRole.PlayerOnly, teamId);
		if (chosenPoint == null)
		{
			chosenPoint = SpawnPoint.GetAvailableSpawnPoint(SpawnPoint.SpawnRole.Any, teamId);
		}

		Vector3 spawnPos = chosenPoint != null ? chosenPoint.transform.position : transform.position;
		Quaternion spawnRot = chosenPoint != null ? chosenPoint.transform.rotation : transform.rotation;

		GameObject spawned = Instantiate(humanoidPrefab, spawnPos, spawnRot);
		spawned.name = humanoidPrefab.name;
		spawned.tag = "LocalPlayer";

		if (chosenPoint != null)
		{
			chosenPoint.Claim(spawned);
		}

		humanoidMotor motor = spawned.GetComponent<humanoidMotor>();
		if (motor == null)
		{
			Debug.LogError("[PlayerMaster] Spawned object is missing a 'humanoidMotor' component on its root!", spawned);
			return null;
		}

		motor.teamId = teamId;

		if (controller != null)
		{
			controller.Possess(motor);
		}

		if (startingWeapon != null)
		{
			motor.EquipWeapon(startingWeapon);
		}

		return motor;
	}

	public void SetPauseState(bool pause)
	{
		IsPaused = pause;

		if (controller != null)
		{
			controller.enabled = !pause;
		}

		Cursor.lockState = pause ? CursorLockMode.None : CursorLockMode.Locked;
		Cursor.visible = pause;
		Time.timeScale = pause ? 0f : 1f;
	}

	public void UpdateMouseSensitivity(float newSens)
	{
		mouseSensitivity = newSens;
	}

	public void ResetForMenuTransition()
	{
		IsPaused = false;

		if (controller != null)
		{
			controller.enabled = true;
		}

		Time.timeScale = 1f;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
	}
}