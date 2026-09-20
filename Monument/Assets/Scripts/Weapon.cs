using System.Collections;
using UnityEngine;

public class Weapon : MonoBehaviour, IInteractable
{
	[Header("Aim & Spawning")]
	public Transform bulletSpawn;
	public GameObject bulletPrefab;
	public GameObject muzzleFlashPrefab;

	[Header("Procedural Grips")]
	public Transform leftHandGrip;
	public Transform rightHandGrip;

	[Header("Ammo & Reloading")]
	public int magSize = 30;
	public int currentAmmo;
	public int reserveBullets = 90;
	public float reloadTime = 2f;

	[Header("Shooting Properties")]
	public float damage = 25f;
	public float bulletVelocity = 100f;
	public float bulletLifeTime = 3f;
	public float shootingDelay = 0.1f;
	public float spreadIntensity = 0.02f;

	[Header("Burst")]
	public int bulletsPerBurst = 3;
	private int burstBulletsLeft;

	[Header("Physics")]
	public float dropForwardForce = 5f;
	public float dropUpwardForce = 2f;

	public enum ShootingMode { Single, Burst, Auto }
	public ShootingMode currentShootingMode = ShootingMode.Auto;

	public bool isShooting { get; private set; }
	public bool readyToShoot { get; private set; } = true;
	public bool isReloading { get; private set; } = false;

	private bool allowReset = true;
	private Rigidbody rb;
	private Collider col;
	private humanoidMotor ownerMotor;

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		col = GetComponent<Collider>();
		currentAmmo = magSize;
		burstBulletsLeft = bulletsPerBurst;
	}

	public void Interact(GameObject interactor)
	{
		humanoidMotor motor = interactor.GetComponent<humanoidMotor>();
		if (motor == null)
		{
			playerController pc = interactor.GetComponent<playerController>();
			if (pc != null) motor = pc.CurrentMotor;
		}

		if (motor != null && motor.currentWeapon == null)
		{
			ownerMotor = motor;
			motor.EquipWeapon(this);
		}
	}

	public void Drop()
	{
		ownerMotor = null;
		transform.SetParent(null);

		if (rb != null)
		{
			rb.isKinematic = false;
			rb.detectCollisions = true;
			rb.AddForce(transform.forward * dropForwardForce + Vector3.up * dropUpwardForce, ForceMode.Impulse);
			rb.AddTorque(Random.insideUnitSphere * 8f, ForceMode.Impulse);
		}

		if (col != null) col.enabled = true;

		if (isReloading)
		{
			StopAllCoroutines();
			isReloading = false;
			readyToShoot = true;
		}
	}

	public void ProcessInput(bool isHoldingTrigger, bool triggerPulledThisFrame)
	{
		if (isReloading) return;

		if (currentShootingMode == ShootingMode.Auto)
		{
			isShooting = isHoldingTrigger;
		}
		else
		{
			isShooting = triggerPulledThisFrame;
		}

		if (readyToShoot && isShooting && currentAmmo > 0)
		{
			burstBulletsLeft = bulletsPerBurst;
			FireWeapon();
		}
		else if (readyToShoot && isShooting && currentAmmo <= 0)
		{
			Reload();
		}
	}

	public void Reload()
	{
		if (isReloading || currentAmmo == magSize || reserveBullets <= 0) return;
		StartCoroutine(PerformReload());
	}

	private IEnumerator PerformReload()
	{
		isReloading = true;
		readyToShoot = false;

		yield return new WaitForSeconds(reloadTime);

		int needed = magSize - currentAmmo;
		int toLoad = Mathf.Min(needed, reserveBullets);

		currentAmmo += toLoad;
		reserveBullets -= toLoad;

		isReloading = false;
		readyToShoot = true;
	}

	private void FireWeapon()
	{
		currentAmmo--;
		readyToShoot = false;

		Vector3 shootDir = CalculateDirectionAndSpread();

		if (bulletPrefab != null && bulletSpawn != null)
		{
			GameObject bullet = Instantiate(bulletPrefab, bulletSpawn.position, Quaternion.LookRotation(shootDir));
			Bullet bulletScript = bullet.GetComponent<Bullet>();
			if (bulletScript != null)
			{
				bulletScript.Initialize(damage, bulletLifeTime);
			}

			Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
			if (bulletRb != null)
			{
				bulletRb.AddForce(shootDir * bulletVelocity, ForceMode.Impulse);
			}
		}

		if (muzzleFlashPrefab != null && bulletSpawn != null)
		{
			Instantiate(muzzleFlashPrefab, bulletSpawn);
		}

		if (allowReset)
		{
			Invoke(nameof(ResetShot), shootingDelay);
			allowReset = false;
		}

		if (currentShootingMode == ShootingMode.Burst && burstBulletsLeft > 1 && currentAmmo > 0)
		{
			burstBulletsLeft--;
			Invoke(nameof(FireWeapon), shootingDelay);
		}
	}

	private void ResetShot()
	{
		if (!isReloading) readyToShoot = true;
		allowReset = true;
	}

	private Vector3 CalculateDirectionAndSpread()
	{
		Vector3 origin = bulletSpawn != null ? bulletSpawn.position : transform.position;
		Vector3 targetPoint = origin + transform.forward * 50f;

		if (ownerMotor != null && ownerMotor.aimTarget != null)
		{
			targetPoint = ownerMotor.aimTarget.position;
		}

		Vector3 baseDir = (targetPoint - origin).normalized;
		Vector3 spread = Random.insideUnitSphere * spreadIntensity;

		return (baseDir + spread).normalized;
	}
}