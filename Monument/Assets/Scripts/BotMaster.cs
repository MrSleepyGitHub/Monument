using UnityEngine;

public class BotMaster : MonoBehaviour
{
	[Header("Controller Reference")]
	public BotController botController;

	[Header("Spawning Setup")]
	public GameObject humanoidPrefab;
	public Weapon startingWeapon;
	public bool spawnOnStart = true;
	public int teamId = 2;

	private void Awake()
	{
		if (botController == null)
		{
			botController = GetComponentInChildren<BotController>();
			if (botController == null)
			{
				botController = gameObject.AddComponent<BotController>();
			}
		}
		botController.teamId = teamId;
	}

	private void Start()
	{
		if (spawnOnStart)
		{
			SpawnBot();
		}
	}

	[ContextMenu("Spawn Bot Now")]
	public humanoidMotor SpawnBot()
	{
		if (botController != null && botController.IsControllingAliveEntity())
		{
			Debug.Log("[BotMaster] Bot is already controlling an active entity. Spawn aborted.", this);
			return botController.CurrentMotor;
		}

		if (humanoidPrefab == null)
		{
			Debug.LogError("[BotMaster] Humanoid Prefab is unassigned!", this);
			return null;
		}

		SpawnPoint chosenPoint = SpawnPoint.GetAvailableSpawnPoint(SpawnPoint.SpawnRole.BotOnly, teamId);
		if (chosenPoint == null)
		{
			chosenPoint = SpawnPoint.GetAvailableSpawnPoint(SpawnPoint.SpawnRole.Any, teamId);
		}

		Vector3 spawnPos = chosenPoint != null ? chosenPoint.transform.position : transform.position;
		Quaternion spawnRot = chosenPoint != null ? chosenPoint.transform.rotation : transform.rotation;

		GameObject spawnedObj = Instantiate(humanoidPrefab, spawnPos, spawnRot);
		spawnedObj.name = $"[Bot_{teamId}] {humanoidPrefab.name}";
		spawnedObj.tag = "Untagged";

		if (chosenPoint != null)
		{
			chosenPoint.Claim(spawnedObj);
		}

		humanoidMotor motor = spawnedObj.GetComponent<humanoidMotor>();
		if (motor == null)
		{
			Debug.LogError("[BotMaster] Spawned prefab is missing a humanoidMotor component!", spawnedObj);
			return null;
		}

		motor.teamId = teamId;

		botController.Possess(motor);

		if (startingWeapon != null)
		{
			motor.EquipWeapon(startingWeapon);
		}

		return motor;
	}
}