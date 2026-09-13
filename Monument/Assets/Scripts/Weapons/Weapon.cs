using System.Collections;
using UnityEngine;

public class Weapon : MonoBehaviour, IInteractable
{
    [Header("References")]
    public Camera playerCamera;
    public GameObject muzzleFlashPrefab;
    public GameObject bulletPrefab;
    public Transform BulletSpawn;

    [Header("Procedural IK Grips")]
    public Transform leftHandGrip;
    public Transform rightHandGrip;

    [Header("Ammo & Reloading")]
    public int magSize = 30;
    public int currentAmmo;
    public int reserveBullets = 90;
    public float reloadTime = 2f; // How long a reload takes in seconds

    [Header("Shooting Properties")]
    public float bulletVelocity = 100;
    public float bulletLifeTime = 3f;
    public float shootingDelay = 0.1f;
    public float spreadIntensity;

    [Header("Burst")]
    public int bulletsPerBurst = 4;
    public int burstBulletsLeft;

    [Header("Physics")]
    public float dropForwardForce = 5f;
    public float dropUpwardForce = 2f;

    public enum ShootingMode { Single, Burst, Auto }
    public ShootingMode currentShootingMode;

    public bool isShooting, readyToShoot, isReloading;
    private bool allowReset = true;

    private Rigidbody rb;
    private Collider col;
    private GameObject currentOwner;

    private void Awake()
    {
        // Physics components are attached directly to this parent empty
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // --- NEW: Dynamically extract the mesh from the child FBX and apply it to the parent ---
        MeshFilter[] childFilters = GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter filter in childFilters)
        {
            // Ensure we are grabbing a child's mesh, not the parent's empty filter
            if (filter.gameObject != this.gameObject && filter.sharedMesh != null)
            {
                MeshFilter parentFilter = GetComponent<MeshFilter>();
                if (parentFilter != null) parentFilter.sharedMesh = filter.sharedMesh;

                MeshCollider parentCollider = GetComponent<MeshCollider>();
                if (parentCollider != null) parentCollider.sharedMesh = filter.sharedMesh;

                break; // Stop after successfully copying the primary model's mesh
            }
        }

        readyToShoot = true;
        burstBulletsLeft = bulletsPerBurst;
        currentAmmo = magSize;
    }

    public void Interact(GameObject interactor)
    {
        playerController player = interactor.GetComponent<playerController>();

        if (player != null && player.currentWeapon == null)
        {
            Equip(player);
        }
    }

    private void Equip(playerController player)
    {
        currentOwner = player.gameObject;
        player.currentWeapon = this;

        if (playerCamera == null && player.motor.cameraHolder != null)
        {
            playerCamera = player.motor.cameraHolder.GetComponentInChildren<Camera>();
        }

        if (rb != null) rb.isKinematic = true;
        if (col != null) col.enabled = false;

        transform.SetParent(player.weaponHolder);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        AnimatorHumanoid anim = player.GetComponent<AnimatorHumanoid>();
        if (anim != null)
        {
            anim.leftHandGrip = this.leftHandGrip;
        }
    }

    public void Drop()
    {
        if (currentOwner == null) return;

        AnimatorHumanoid anim = currentOwner.GetComponent<AnimatorHumanoid>();
        if (anim != null)
        {
            anim.leftHandGrip = null;
        }

        currentOwner.GetComponent<playerController>().currentWeapon = null;
        currentOwner = null;

        transform.SetParent(null);

        if (rb != null) rb.isKinematic = false;
        if (col != null) col.enabled = true;

        if (isReloading)
        {
            StopAllCoroutines();
            isReloading = false;
            readyToShoot = true;
        }

        if (playerCamera != null && rb != null)
        {
            rb.AddForce(playerCamera.transform.forward * dropForwardForce + Vector3.up * dropUpwardForce, ForceMode.Impulse);
            float randomDir = UnityEngine.Random.Range(-1f, 1f);
            rb.AddTorque(new Vector3(randomDir, randomDir, randomDir) * 10f);
        }
    }

    public void ProcessInput(bool isHoldingTrigger, bool triggerPulledThisFrame)
    {
        if (isReloading) return;

        if (currentShootingMode == ShootingMode.Auto)
        {
            isShooting = isHoldingTrigger;
        }
        else if (currentShootingMode == ShootingMode.Single || currentShootingMode == ShootingMode.Burst)
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

        int bulletsNeeded = magSize - currentAmmo;
        int bulletsToLoad = Mathf.Min(bulletsNeeded, reserveBullets);

        currentAmmo += bulletsToLoad;
        reserveBullets -= bulletsToLoad;

        isReloading = false;
        readyToShoot = true;
    }

    private void FireWeapon()
    {
        currentAmmo--;
        readyToShoot = false;

        Vector3 shootingDirection = CalculateDirectionAndSpread().normalized;

        GameObject bullet = Instantiate(bulletPrefab, BulletSpawn.position, Quaternion.identity);
        bullet.transform.forward = shootingDirection;

        Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            bulletRb.AddForce(shootingDirection * bulletVelocity, ForceMode.Impulse);
        }

        StartCoroutine(DestroyBulletAfterTime(bullet, bulletLifeTime));

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

        MuzzleFlashEffect();
    }

    private void ResetShot()
    {
        if (!isReloading) readyToShoot = true;
        allowReset = true;
    }

    public Vector3 CalculateDirectionAndSpread()
    {
        if (playerCamera == null) return BulletSpawn.forward;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 targetPoint;

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = ray.GetPoint(100);
        }

        Vector3 direction = targetPoint - BulletSpawn.position;
        float x = UnityEngine.Random.Range(-spreadIntensity, spreadIntensity);
        float y = UnityEngine.Random.Range(-spreadIntensity, spreadIntensity);

        return direction + new Vector3(x, y, 0);
    }

    private IEnumerator DestroyBulletAfterTime(GameObject bullet, float lifeTime)
    {
        yield return new WaitForSeconds(lifeTime);
        Destroy(bullet);
    }

    private void MuzzleFlashEffect()
    {
        if (muzzleFlashPrefab != null && BulletSpawn != null)
        {
            Instantiate(muzzleFlashPrefab, BulletSpawn);
        }
    }
}