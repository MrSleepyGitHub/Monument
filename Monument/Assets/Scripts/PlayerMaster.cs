using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PlayerMaster : MonoBehaviour
{
	public static PlayerMaster Instance { get; private set; }

	[Header("Scene Configuration")]
	[Tooltip("Build index of the Main Menu scene. No humanoids will spawn in this scene.")]
	[SerializeField] private int mainMenuSceneIndex = 0;

	[Header("Player & Controller References")]
	public playerController playerController;
	public KeybindFramework keybindFramework;
	public humanoidMotor humanoidPrefab;
	public Weapon startingWeaponPrefab;

	[Header("Spectator")]
	[Tooltip("Prefab containing the SpectatorMotor and spectator Camera.")]
	[SerializeField] private SpectatorMotor spectatorPrefab;

	[Header("Shared UI Panel References")]
	[Tooltip("The root Options / Settings panel under PlayerMaster.")]
	public GameObject optionsMenuRoot;

	[Header("Settings & Team")]
	public float MouseSensitivity = 10f;
	public int playerTeamId = 1;
	public bool IsPaused { get; private set; } = false;

	[Header("Respawn Status (Read-Only)")]
	[SerializeField] private float respawnTimer = 0f;
	[SerializeField] private bool isWaitingToRespawn = false;

	public float RemainingRespawnTime => Mathf.Max(0f, respawnTimer);
	public bool IsWaitingToRespawn => isWaitingToRespawn;
	public bool CanRespawnNow => isWaitingToRespawn && respawnTimer <= 0f && HasSuitableSpawnPoint();

	public event Action<float> OnRespawnTimerUpdated;
	public event Action<bool> OnRespawnAvailabilityChanged;
	public event Action OnPlayerSpawned;

	private humanoidMotor _currentSpawnedHumanoid;
	private SpectatorMotor _activeSpectator;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
		transform.SetParent(null);
		DontDestroyOnLoad(gameObject);

		if (keybindFramework == null)
		{
			keybindFramework = GetComponentInChildren<KeybindFramework>();
		}

		if (playerController == null)
		{
			playerController = GetComponentInChildren<playerController>();
		}

		if (SceneManager.GetActiveScene().buildIndex == mainMenuSceneIndex)
		{
			CleanupPlayerAndHumanoid();
			SetPauseState(false);
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}
	}

	private void OnEnable()
	{
		SceneManager.sceneLoaded += HandleSceneLoaded;
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= HandleSceneLoaded;
	}

	private void Update()
	{
		if (!isWaitingToRespawn) return;

		if (respawnTimer > 0f)
		{
			respawnTimer = Mathf.Max(0f, respawnTimer - Time.deltaTime);
			OnRespawnTimerUpdated?.Invoke(respawnTimer);

			if (respawnTimer <= 0f)
			{
				OnRespawnAvailabilityChanged?.Invoke(HasSuitableSpawnPoint());

				if (GameMode.Instance != null && GameMode.Instance.autoRespawn)
				{
					RequestRespawn();
				}
			}
		}
	}

	private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		ResolveSceneAudioListeners(scene.buildIndex);
		ResolveSceneEventSystems(scene.buildIndex);

		if (scene.buildIndex == mainMenuSceneIndex)
		{
			CleanupPlayerAndHumanoid();
			SetPauseState(false);
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			return;
		}

		CleanupPlayerAndHumanoid();

		if (playerController == null)
		{
			playerController = FindFirstObjectByType<playerController>();
		}

		StartCoroutine(DelayedInitialSpawn());
	}

	private IEnumerator DelayedInitialSpawn()
	{
		yield return null;
		RequestRespawn();
	}

	private void ResolveSceneAudioListeners(int activeSceneIndex)
	{
		AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		bool assignedActive = false;

		for (int i = 0; i < listeners.Length; i++)
		{
			AudioListener l = listeners[i];
			if (l == null) continue;

			if (activeSceneIndex == mainMenuSceneIndex)
			{
				if (!assignedActive && l.gameObject.scene.buildIndex == mainMenuSceneIndex && !l.transform.IsChildOf(transform) && l.gameObject != gameObject)
				{
					l.enabled = true;
					assignedActive = true;
				}
				else
				{
					l.enabled = false;
				}
			}
			else
			{
				if (l.transform.IsChildOf(transform)) continue;
			}
		}
	}

	private void ResolveSceneEventSystems(int activeSceneIndex)
	{
		EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		EventSystem primarySystem = null;

		for (int i = 0; i < systems.Length; i++)
		{
			EventSystem es = systems[i];
			if (es == null) continue;

			if (es.gameObject == gameObject || es.transform.IsChildOf(transform))
			{
				if (activeSceneIndex == mainMenuSceneIndex)
				{
					es.enabled = false;
				}
				continue;
			}

			if (es.gameObject.scene.buildIndex == activeSceneIndex)
			{
				if (primarySystem == null) primarySystem = es;
				else Destroy(es.gameObject);
			}
			else
			{
				Destroy(es.gameObject);
			}
		}

		if (primarySystem == null)
		{
			for (int i = 0; i < systems.Length; i++)
			{
				if (systems[i] != null && (systems[i].gameObject == gameObject || systems[i].transform.IsChildOf(transform)))
				{
					primarySystem = systems[i];
					break;
				}
			}
		}

		if (primarySystem != null)
		{
			primarySystem.enabled = false;
			primarySystem.enabled = true;
			primarySystem.SetSelectedGameObject(null);
		}
	}

	public void CleanupPlayerAndHumanoid()
	{
		isWaitingToRespawn = false;
		respawnTimer = 0f;

		if (_activeSpectator != null)
		{
			Destroy(_activeSpectator.gameObject);
			_activeSpectator = null;
		}

		if (playerController != null)
		{
			playerController.ResetControllerAndMotor();
			playerController.DisableMasterCamera();
		}

		if (_currentSpawnedHumanoid != null)
		{
			Destroy(_currentSpawnedHumanoid.gameObject);
			_currentSpawnedHumanoid = null;
		}
	}

	public void HandlePlayerDeath()
	{
		// 1. Save dead player's camera position & rotation
		Vector3 camPos = Vector3.up * 2f;
		Quaternion camRot = Quaternion.identity;

		if (_currentSpawnedHumanoid != null)
		{
			if (_currentSpawnedHumanoid.playerCamera != null)
			{
				camPos = _currentSpawnedHumanoid.playerCamera.transform.position;
				camRot = _currentSpawnedHumanoid.playerCamera.transform.rotation;
				_currentSpawnedHumanoid.playerCamera.gameObject.SetActive(false);
			}
			else
			{
				camPos = _currentSpawnedHumanoid.transform.position + Vector3.up * 1.8f;
				camRot = _currentSpawnedHumanoid.transform.rotation;
			}

			_currentSpawnedHumanoid.DisableMotorOnDeath();
		}

		// 2. Unpossess infantry controller
		if (playerController != null)
		{
			playerController.ResetControllerAndMotor();
		}

		// 3. Spawn Spectator Motor at the death camera position
		if (spectatorPrefab != null)
		{
			if (_activeSpectator != null) Destroy(_activeSpectator.gameObject);
			_activeSpectator = Instantiate(spectatorPrefab, camPos, camRot);
			_activeSpectator.Initialize(camPos, camRot);
		}
		else
		{
			Debug.LogWarning("[PlayerMaster] No spectatorPrefab assigned on PlayerMaster!", this);
		}

		float delay = (GameMode.Instance != null) ? GameMode.Instance.respawnDelay : 3.0f;
		respawnTimer = delay;
		isWaitingToRespawn = true;

		OnRespawnTimerUpdated?.Invoke(respawnTimer);
		OnRespawnAvailabilityChanged?.Invoke(false);
	}

	public bool HasSuitableSpawnPoint()
	{
		return GetBestAvailableSpawn() != null;
	}

	private SpawnPoint GetBestAvailableSpawn()
	{
		SpawnPoint sp = SpawnPoint.GetAvailableSpawnPoint(SpawnPoint.SpawnRole.PlayerOnly, playerTeamId);
		if (sp != null) return sp;

		sp = SpawnPoint.GetAvailableSpawnPoint(SpawnPoint.SpawnRole.Any, playerTeamId);
		return sp ?? SpawnPoint.GetAvailableSpawnPoint(SpawnPoint.SpawnRole.Any, 0);
	}

	public bool RequestRespawn()
	{
		if (SceneManager.GetActiveScene().buildIndex == mainMenuSceneIndex)
		{
			return false;
		}

		if (humanoidPrefab == null)
		{
			Debug.LogError("[PlayerMaster] No humanoidPrefab assigned on PlayerMaster.");
			return false;
		}

		SpawnPoint spawn = GetBestAvailableSpawn();
		Vector3 spawnPos = Vector3.up * 1.5f;
		Quaternion spawnRot = Quaternion.identity;

		if (spawn != null)
		{
			spawnPos = spawn.transform.position;
			spawnRot = spawn.transform.rotation;
		}
		else
		{
			GameObject fallbackObj = GameObject.FindWithTag("Respawn");
			if (fallbackObj != null)
			{
				spawnPos = fallbackObj.transform.position;
				spawnRot = fallbackObj.transform.rotation;
			}
		}

		// 1. Destroy active spectator
		if (_activeSpectator != null)
		{
			Destroy(_activeSpectator.gameObject);
			_activeSpectator = null;
		}

		// 2. Clear old humanoid if alive (corpses are already disabled)
		if (_currentSpawnedHumanoid != null)
		{
			if (!_currentSpawnedHumanoid.isDead)
			{
				Destroy(_currentSpawnedHumanoid.gameObject);
			}
			_currentSpawnedHumanoid = null;
		}

		// 3. Reset player controller
		if (playerController != null)
		{
			playerController.ResetControllerAndMotor();
		}

		// 4. Instantiate new humanoid
		_currentSpawnedHumanoid = Instantiate(humanoidPrefab, spawnPos, spawnRot);
		_currentSpawnedHumanoid.teamId = playerTeamId;

		if (spawn != null)
		{
			spawn.Claim(_currentSpawnedHumanoid.gameObject);
		}

		_currentSpawnedHumanoid.OnDeath.AddListener(HandlePlayerDeath);

		// 5. Possess first so isLocallyControlled is set to true
		if (playerController != null)
		{
			playerController.Possess(_currentSpawnedHumanoid);
		}

		// 6. Equip starting weapon second so it instantiates the FP model into fpWeaponHolder
		if (startingWeaponPrefab != null)
		{
			_currentSpawnedHumanoid.EquipWeapon(startingWeaponPrefab);
		}

		isWaitingToRespawn = false;
		respawnTimer = 0f;

		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;

		OnPlayerSpawned?.Invoke();
		OnRespawnAvailabilityChanged?.Invoke(false);

		return true;
	}

	public void OpenOptionsMenu()
	{
		if (optionsMenuRoot != null && MenuManager.Instance != null)
		{
			MenuManager.Instance.OpenMenu(optionsMenuRoot);
		}
		else
		{
			Debug.LogWarning("[PlayerMaster] Options menu root is not assigned or MenuManager is missing!", this);
		}
	}

	public void SetPauseState(bool paused)
	{
		IsPaused = paused;
		Time.timeScale = paused ? 0f : 1f;
		Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
		Cursor.visible = paused;
	}

	public void ResetForMenuTransition()
	{
		SetPauseState(false);
		CleanupPlayerAndHumanoid();

		AudioListener[] myListeners = GetComponentsInChildren<AudioListener>(true);
		for (int i = 0; i < myListeners.Length; i++) myListeners[i].enabled = false;

		EventSystem[] myEventSystems = GetComponentsInChildren<EventSystem>(true);
		for (int i = 0; i < myEventSystems.Length; i++) myEventSystems[i].enabled = false;

		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
	}

	public void UpdateMouseSensitivity(float newSens)
	{
		MouseSensitivity = newSens;
	}
}