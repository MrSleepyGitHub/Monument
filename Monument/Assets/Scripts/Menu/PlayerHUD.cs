using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
	[Header("Player Reference")]
	public playerController player;

	[Header("Gameplay HUD Indicators (Hidden when Dead)")]
	public GameObject crosshair;
	public TextMeshProUGUI ammoText;
	public Slider healthSlider;
	public TextMeshProUGUI healthText;

	[Header("Respawn UI Panel (Visible when Dead)")]
	public GameObject respawnPanel;
	public Button respawnButton;
	public TextMeshProUGUI respawnStatusText;

	private void Awake()
	{
		if (respawnButton != null)
		{
			respawnButton.onClick.AddListener(OnRespawnButtonClicked);
		}
	}

	private void Start()
	{
		ResolvePlayer();

		if (PlayerMaster.Instance != null)
		{
			PlayerMaster.Instance.OnRespawnTimerUpdated += HandleRespawnTimerUpdate;
			PlayerMaster.Instance.OnRespawnAvailabilityChanged += HandleRespawnAvailability;
			PlayerMaster.Instance.OnPlayerSpawned += HandlePlayerSpawned;
		}

		if (respawnPanel != null)
		{
			respawnPanel.SetActive(false);
		}
	}

	private void OnDestroy()
	{
		if (PlayerMaster.Instance != null)
		{
			PlayerMaster.Instance.OnRespawnTimerUpdated -= HandleRespawnTimerUpdate;
			PlayerMaster.Instance.OnRespawnAvailabilityChanged -= HandleRespawnAvailability;
			PlayerMaster.Instance.OnPlayerSpawned -= HandlePlayerSpawned;
		}
	}

	private void ResolvePlayer()
	{
		if (player == null && PlayerMaster.Instance != null)
		{
			player = PlayerMaster.Instance.playerController;
		}
		if (player == null)
		{
			player = FindFirstObjectByType<playerController>();
		}
	}

	private void Update()
	{
		if (player == null) ResolvePlayer();

		bool isAlive = player != null && player.IsControllingAliveEntity();

		if (crosshair != null) crosshair.SetActive(isAlive);

		if (isAlive)
		{
			if (respawnPanel != null && respawnPanel.activeSelf)
			{
				respawnPanel.SetActive(false);
			}

			// Health Display
			if (player.CurrentMotor != null)
			{
				float hp = player.CurrentMotor.currentHealth;
				float maxHp = player.CurrentMotor.maxHealth;

				if (healthSlider != null)
				{
					healthSlider.maxValue = maxHp;
					healthSlider.value = hp;
				}

				if (healthText != null)
				{
					healthText.text = $"{Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(maxHp)}";
				}
			}

			// Ammo Display
			if (player.currentWeapon != null)
			{
				if (ammoText != null)
				{
					ammoText.gameObject.SetActive(true);
					if (player.currentWeapon.isReloading)
					{
						ammoText.text = "RELOADING...";
					}
					else
					{
						ammoText.text = $"{player.currentWeapon.currentAmmo} / {player.currentWeapon.reserveBullets}";
					}
				}
			}
			else if (ammoText != null)
			{
				ammoText.gameObject.SetActive(false);
			}
		}
		else
		{
			if (ammoText != null) ammoText.gameObject.SetActive(false);

			if (PlayerMaster.Instance != null && PlayerMaster.Instance.IsWaitingToRespawn)
			{
				if (respawnPanel != null && !respawnPanel.activeSelf)
				{
					respawnPanel.SetActive(true);
				}

				// Allow pressing Space or Enter to respawn when the button is ready
				if (respawnButton != null && respawnButton.interactable && Keyboard.current != null)
				{
					if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
					{
						OnRespawnButtonClicked();
					}
				}
			}
		}
	}

	private void HandleRespawnTimerUpdate(float remaining)
	{
		if (respawnPanel != null && !respawnPanel.activeSelf)
		{
			respawnPanel.SetActive(true);
		}

		if (remaining > 0f)
		{
			if (respawnStatusText != null) respawnStatusText.text = $"Respawn in {remaining:F1}s";
			if (respawnButton != null) respawnButton.interactable = false;
		}
		else
		{
			bool hasSpawn = PlayerMaster.Instance != null && PlayerMaster.Instance.HasSuitableSpawnPoint();
			UpdateRespawnStatus(hasSpawn);
		}
	}

	private void HandleRespawnAvailability(bool canRespawn)
	{
		UpdateRespawnStatus(canRespawn);
	}

	private void UpdateRespawnStatus(bool hasSpawn)
	{
		if (PlayerMaster.Instance == null || PlayerMaster.Instance.RemainingRespawnTime > 0f) return;

		if (hasSpawn)
		{
			if (respawnStatusText != null) respawnStatusText.text = "Spawn Point Ready";
			if (respawnButton != null) respawnButton.interactable = true;

			// Unlock cursor so the player can click the button
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}
		else
		{
			if (respawnStatusText != null) respawnStatusText.text = "All Spawn Points Blocked...";
			if (respawnButton != null) respawnButton.interactable = false;
		}
	}

	private void OnRespawnButtonClicked()
	{
		if (PlayerMaster.Instance != null)
		{
			bool success = PlayerMaster.Instance.RequestRespawn();
			if (!success && respawnStatusText != null)
			{
				respawnStatusText.text = "Spawn point is obstructed!";
			}
		}
	}

	private void HandlePlayerSpawned()
	{
		if (respawnPanel != null)
		{
			respawnPanel.SetActive(false);
		}

		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}
}