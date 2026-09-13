using UnityEngine;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
	[Header("Player Reference")]
	public playerController player;

	[Header("HUD Elements")]
	public GameObject crosshair;
	public TextMeshProUGUI ammoText;

	private void Update()
	{
		if (player == null) return;

		// Always keep the crosshair visible for interacting with the world
		if (crosshair != null && !crosshair.activeSelf)
		{
			crosshair.SetActive(true);
		}

		if (player.currentWeapon != null)
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
		else
		{
			// Hide the ammo counter if the player drops their weapon
			ammoText.gameObject.SetActive(false);
		}
	}
}