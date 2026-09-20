using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class HumanoidVisualController : MonoBehaviour
{
	public enum AnimationState { Locomotion, Seated }

	[Header("Core References")]
	[SerializeField] private humanoidMotor motor;

	[Header("Rig Roots")]
	[SerializeField] private GameObject fpRigRoot;
	[SerializeField] private GameObject tpRigRoot;

	[Header("Weapon Sockets")]
	[Tooltip("Socket located on the First-Person Arms rig.")]
	public Transform fpWeaponHolder;
	[Tooltip("Socket located on the Third-Person Humanoid rig.")]
	public Transform tpWeaponHolder;

	[Header("Animators")]
	[SerializeField] private Animator fpAnimator;
	[SerializeField] private Animator tpAnimator;

	[Header("Animation Rigging References")]
	public Rig humanoidRig;
	public MultiAimConstraint chestAim;
	public MultiAimConstraint headAim;
	public MultiAimConstraint weaponAimR;
	public TwoBoneIKConstraint armIKR;
	public TwoBoneIKConstraint armIKL;
	public Transform armIKL_Target;
	public float weightBlendSpeed = 10f;

	[Header("Layer Names")]
	[SerializeField] private string localPlayerCullLayer = "LocalPlayer_TP";
	[SerializeField] private string fpArmsLayer = "FPS_Arms";

	// Cached Internal Variables
	private RigBuilder _rigBuilder;
	private Renderer[] _tpRenderers;
	private Rigidbody[] _ragdollRigidbodies;
	private Collider[] _ragdollColliders;
	private Transform _currentGripSocket;
	private GameObject _spawnedFPVisualWeapon;
	private AnimationState _currentState = AnimationState.Locomotion;
	private bool _isRagdolled = false;
	private bool _isLocalPlayer = false;

	// Animator Hash IDs
	private readonly int moveXHash = Animator.StringToHash("MoveX");
	private readonly int moveYHash = Animator.StringToHash("MoveY");
	private readonly int groundedStanceHash = Animator.StringToHash("GroundedStance");
	private readonly int idleStanceHash = Animator.StringToHash("IdleStance");
	private readonly int isGroundedHash = Animator.StringToHash("IsGrounded");
	private readonly int inVehicleHash = Animator.StringToHash("InVehicle");
	private readonly int isArmedHash = Animator.StringToHash("IsArmed");
	private readonly int speedHash = Animator.StringToHash("Speed");
	private readonly int isAimingHash = Animator.StringToHash("IsAiming");
	private readonly int fireTriggerHash = Animator.StringToHash("Fire");
	private readonly int reloadTriggerHash = Animator.StringToHash("Reload");

	// Parameter Guards
	private bool hasMoveX, hasMoveY, hasGroundedStance, hasIdleStance, hasIsGrounded, hasInVehicle, hasIsArmed, hasSpeed;

	private void Awake()
	{
		if (motor == null) motor = GetComponentInParent<humanoidMotor>();
		AutoResolveReferences();
		CacheRenderers();
		CacheRagdollPhysics();
		CacheAnimatorParameters();

		SetRagdollPhysicsActive(false);
	}

	private void AutoResolveReferences()
	{
		if (tpRigRoot == null)
		{
			Transform yBot = transform.Find("Y Bot");
			if (yBot != null) tpRigRoot = yBot.gameObject;
		}

		if (tpAnimator == null && tpRigRoot != null)
			tpAnimator = tpRigRoot.GetComponentInChildren<Animator>();

		if (tpAnimator == null)
			tpAnimator = GetComponentInChildren<Animator>();

		if (tpAnimator != null)
			_rigBuilder = tpAnimator.GetComponent<RigBuilder>();

		if (tpWeaponHolder == null && motor != null)
			tpWeaponHolder = motor.weaponHolder;

		if (fpRigRoot != null && fpWeaponHolder == null)
		{
			Transform socket = fpRigRoot.transform.Find("FP_WeaponSocket");
			if (socket == null) socket = fpRigRoot.transform.Find("WeaponHolder");
			if (socket != null) fpWeaponHolder = socket;
		}
	}

	private void Update()
	{
		if (_isRagdolled || motor == null) return;

		EvaluateLocomotionState();
		UpdateAnimatorParameters();
		UpdateRiggingWeights();
		UpdateGripTargetPosition();
	}

	// --- SETUP & CAMERA CULLING ---

	public void CacheRenderers()
	{
		if (tpRigRoot != null)
			_tpRenderers = tpRigRoot.GetComponentsInChildren<Renderer>(true);
		else
			_tpRenderers = GetComponentsInChildren<Renderer>(true);
	}

	public void ConfigureVisuals(bool isLocal)
	{
		_isLocalPlayer = isLocal;
		CacheRenderers();
		ConfigureCameraCulling(isLocal);

		if (isLocal)
		{
			gameObject.tag = "LocalPlayer";

			if (fpRigRoot != null)
			{
				fpRigRoot.SetActive(true);
				int fpLayer = LayerMask.NameToLayer(fpArmsLayer);
				if (fpLayer != -1) SetLayerRecursively(fpRigRoot, fpLayer);
			}

			if (_tpRenderers != null)
			{
				for (int i = 0; i < _tpRenderers.Length; i++)
				{
					if (_tpRenderers[i] != null)
						_tpRenderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
				}
			}

			int cullLayer = LayerMask.NameToLayer(localPlayerCullLayer);
			if (cullLayer != -1 && tpRigRoot != null)
			{
				SetLayerRecursively(tpRigRoot, cullLayer);
			}
		}
		else
		{
			gameObject.tag = "Untagged";

			if (fpRigRoot != null) fpRigRoot.SetActive(false);

			int defaultLayer = LayerMask.NameToLayer("Default");
			if (tpRigRoot != null) SetLayerRecursively(tpRigRoot, defaultLayer);

			if (_tpRenderers != null)
			{
				for (int i = 0; i < _tpRenderers.Length; i++)
				{
					if (_tpRenderers[i] != null)
					{
						_tpRenderers[i].shadowCastingMode = ShadowCastingMode.On;
						_tpRenderers[i].enabled = true;
					}
				}
			}
		}
	}

	private void ConfigureCameraCulling(bool isLocal)
	{
		if (!isLocal || motor == null || motor.playerCamera == null) return;

		Camera worldCam = motor.playerCamera;
		int fpLayer = LayerMask.NameToLayer(fpArmsLayer);
		int tpLocalLayer = LayerMask.NameToLayer(localPlayerCullLayer);

		if (fpLayer != -1) worldCam.cullingMask &= ~(1 << fpLayer);
		if (tpLocalLayer != -1) worldCam.cullingMask &= ~(1 << tpLocalLayer);

		if (motor.camGimbal != null)
		{
			Camera[] cams = motor.camGimbal.GetComponentsInChildren<Camera>(true);
			for (int i = 0; i < cams.Length; i++)
			{
				if (cams[i] != worldCam && fpLayer != -1)
				{
					cams[i].cullingMask = (1 << fpLayer);
				}
			}
		}
	}

	// --- WEAPON PARENTING & SETUP ---

	public Weapon SetupWeaponForPerspective(Weapon sourceWeapon, bool isLocal)
	{
		if (sourceWeapon == null) return null;

		Transform targetTpParent = tpWeaponHolder != null ? tpWeaponHolder : transform;
		Weapon activeTpWeapon;

		// 1. Scene Instance vs Project Prefab: Parent existing ground weapons directly
		if (sourceWeapon.gameObject.scene.IsValid())
		{
			activeTpWeapon = sourceWeapon;
			activeTpWeapon.transform.SetParent(targetTpParent);
		}
		else
		{
			activeTpWeapon = Instantiate(sourceWeapon, targetTpParent);
			activeTpWeapon.name = $"[TP_Firing] {sourceWeapon.name}";
		}

		activeTpWeapon.transform.localPosition = Vector3.zero;
		activeTpWeapon.transform.localRotation = Quaternion.identity;

		// Purge any orphan/duplicate weapons from the third-person socket
		if (tpWeaponHolder != null)
		{
			for (int i = tpWeaponHolder.childCount - 1; i >= 0; i--)
			{
				Transform child = tpWeaponHolder.GetChild(i);
				if (child != activeTpWeapon.transform)
				{
					Destroy(child.gameObject);
				}
			}
		}

		// Disable world physics and colliders while held
		foreach (var col in activeTpWeapon.GetComponentsInChildren<Collider>(true)) col.enabled = false;
		Rigidbody rb = activeTpWeapon.GetComponent<Rigidbody>();
		if (rb != null) { rb.isKinematic = true; rb.detectCollisions = false; }

		// Untag so it cannot be re-interacted with while in hand
		activeTpWeapon.tag = "Untagged";

		BindLeftHandGrip(activeTpWeapon.leftHandGrip);

		int fpLayer = LayerMask.NameToLayer(fpArmsLayer);
		int tpLocalLayer = LayerMask.NameToLayer(localPlayerCullLayer);
		int defaultLayer = LayerMask.NameToLayer("Default");

		// Always clear all previous visual models from FP arms
		ClearFPWeapons();

		if (isLocal)
		{
			// Hide the TP weapon from local view while casting shadows
			foreach (var rend in activeTpWeapon.GetComponentsInChildren<Renderer>(true))
			{
				if (rend is ParticleSystemRenderer psr)
				{
					psr.enabled = false;
				}
				else if (rend is TrailRenderer tr)
				{
					tr.enabled = false;
				}
				else if (rend is LineRenderer lr)
				{
					lr.enabled = false;
				}
				else
				{
					rend.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
				}
			}

			foreach (var light in activeTpWeapon.GetComponentsInChildren<Light>(true))
			{
				light.enabled = false;
			}

			foreach (var ps in activeTpWeapon.GetComponentsInChildren<ParticleSystem>(true))
			{
				var em = ps.emission;
				em.enabled = false;
			}

			if (tpLocalLayer != -1)
				SetLayerRecursively(activeTpWeapon.gameObject, tpLocalLayer);
			else if (defaultLayer != -1)
				SetLayerRecursively(activeTpWeapon.gameObject, defaultLayer);

			// 2. Instantiate visual-only representation on First-Person arms
			if (fpWeaponHolder != null)
			{
				_spawnedFPVisualWeapon = Instantiate(activeTpWeapon.gameObject, fpWeaponHolder);
				_spawnedFPVisualWeapon.name = $"[FP_Visual] {activeTpWeapon.name}";
				_spawnedFPVisualWeapon.transform.localPosition = Vector3.zero;
				_spawnedFPVisualWeapon.transform.localRotation = Quaternion.identity;

				Weapon fpComp = _spawnedFPVisualWeapon.GetComponent<Weapon>();
				if (fpComp != null) fpComp.enabled = false;

				foreach (var col in _spawnedFPVisualWeapon.GetComponentsInChildren<Collider>(true)) col.enabled = false;
				foreach (var wRb in _spawnedFPVisualWeapon.GetComponentsInChildren<Rigidbody>(true)) { wRb.isKinematic = true; wRb.detectCollisions = false; }
				foreach (var aud in _spawnedFPVisualWeapon.GetComponentsInChildren<AudioSource>(true)) aud.enabled = false;

				foreach (var rend in _spawnedFPVisualWeapon.GetComponentsInChildren<Renderer>(true))
				{
					rend.shadowCastingMode = ShadowCastingMode.Off;
					rend.enabled = true;
				}

				if (fpLayer != -1) SetLayerRecursively(_spawnedFPVisualWeapon, fpLayer);
			}
		}
		else
		{
			// Remote bot: Fully visible
			foreach (var rend in activeTpWeapon.GetComponentsInChildren<Renderer>(true))
			{
				rend.shadowCastingMode = ShadowCastingMode.On;
				rend.enabled = true;
			}
			foreach (var ps in activeTpWeapon.GetComponentsInChildren<ParticleSystem>(true))
			{
				var em = ps.emission;
				em.enabled = true;
			}
			foreach (var light in activeTpWeapon.GetComponentsInChildren<Light>(true))
			{
				light.enabled = true;
			}

			if (defaultLayer != -1) SetLayerRecursively(activeTpWeapon.gameObject, defaultLayer);
		}

		return activeTpWeapon;
	}

	public void RestoreWeaponForWorld(Weapon weapon)
	{
		if (weapon == null) return;

		int defaultLayer = LayerMask.NameToLayer("Default");
		if (defaultLayer != -1)
		{
			SetLayerRecursively(weapon.gameObject, defaultLayer);
		}

		weapon.tag = "Interactable";

		foreach (var rend in weapon.GetComponentsInChildren<Renderer>(true))
		{
			rend.enabled = true;
			rend.shadowCastingMode = ShadowCastingMode.On;
		}

		foreach (var col in weapon.GetComponentsInChildren<Collider>(true))
		{
			col.enabled = true;
		}

		foreach (var light in weapon.GetComponentsInChildren<Light>(true))
		{
			light.enabled = true;
		}

		foreach (var ps in weapon.GetComponentsInChildren<ParticleSystem>(true))
		{
			var em = ps.emission;
			em.enabled = true;
		}
	}

	public void ClearFPWeapons()
	{
		if (fpWeaponHolder != null)
		{
			for (int i = fpWeaponHolder.childCount - 1; i >= 0; i--)
			{
				Destroy(fpWeaponHolder.GetChild(i).gameObject);
			}
		}
		_spawnedFPVisualWeapon = null;
		BindLeftHandGrip(null);
	}

	public void ClearAllWeapons()
	{
		ClearFPWeapons();
	}

	public void PlayFPMuzzleFlash()
	{
		if (_spawnedFPVisualWeapon == null) return;

		ParticleSystem[] systems = _spawnedFPVisualWeapon.GetComponentsInChildren<ParticleSystem>(true);
		for (int i = 0; i < systems.Length; i++)
		{
			systems[i].Play();
		}
	}

	// --- RAGDOLL HITBOX SYSTEM ---

	private void CacheRagdollPhysics()
	{
		GameObject rootToSearch = tpRigRoot != null ? tpRigRoot : gameObject;

		Rigidbody myRb = GetComponent<Rigidbody>();
		Collider myCol = GetComponent<Collider>();

		Rigidbody[] allRbs = rootToSearch.GetComponentsInChildren<Rigidbody>(true);
		List<Rigidbody> rbList = new List<Rigidbody>();
		for (int i = 0; i < allRbs.Length; i++)
		{
			if (allRbs[i] != myRb) rbList.Add(allRbs[i]);
		}
		_ragdollRigidbodies = rbList.ToArray();

		Collider[] allCols = rootToSearch.GetComponentsInChildren<Collider>(true);
		List<Collider> colList = new List<Collider>();
		for (int i = 0; i < allCols.Length; i++)
		{
			if (allCols[i] != myCol && allCols[i] is not CharacterController)
			{
				colList.Add(allCols[i]);

				if (motor != null && motor.playerCollider != null)
				{
					Physics.IgnoreCollision(motor.playerCollider, allCols[i], true);
				}
			}
		}
		_ragdollColliders = colList.ToArray();
	}

	private void SetRagdollPhysicsActive(bool active)
	{
		if (_ragdollRigidbodies != null)
		{
			for (int i = 0; i < _ragdollRigidbodies.Length; i++)
			{
				_ragdollRigidbodies[i].isKinematic = !active;
				_ragdollRigidbodies[i].detectCollisions = true;
			}
		}

		if (_ragdollColliders != null)
		{
			for (int i = 0; i < _ragdollColliders.Length; i++)
			{
				_ragdollColliders[i].enabled = true;
			}
		}
	}

	public void EnableRagdoll(Vector3 inheritedVelocity)
	{
		if (_isRagdolled) return;
		_isRagdolled = true;

		if (tpAnimator != null) tpAnimator.enabled = false;
		if (_rigBuilder != null) _rigBuilder.enabled = false;
		if (fpRigRoot != null) fpRigRoot.SetActive(false);

		if (CompareTag("LocalPlayer"))
		{
			if (_tpRenderers == null || _tpRenderers.Length == 0) CacheRenderers();
			for (int i = 0; i < _tpRenderers.Length; i++)
			{
				if (_tpRenderers[i] != null) _tpRenderers[i].shadowCastingMode = ShadowCastingMode.On;
			}
			int defaultLayer = LayerMask.NameToLayer("Default");
			if (tpRigRoot != null) SetLayerRecursively(tpRigRoot, defaultLayer);
		}

		SetRagdollPhysicsActive(true);

		if (_ragdollRigidbodies != null)
		{
			for (int i = 0; i < _ragdollRigidbodies.Length; i++)
			{
				_ragdollRigidbodies[i].linearVelocity = inheritedVelocity;
			}
		}
	}

	public void ApplyImpulseToBone(Vector3 impulse, Vector3 hitPoint)
	{
		if (_ragdollRigidbodies == null || _ragdollRigidbodies.Length == 0) return;

		Rigidbody closestBone = null;
		float closestDistSqr = float.MaxValue;

		for (int i = 0; i < _ragdollRigidbodies.Length; i++)
		{
			if (_ragdollRigidbodies[i] == null) continue;
			float distSqr = (_ragdollRigidbodies[i].position - hitPoint).sqrMagnitude;
			if (distSqr < closestDistSqr)
			{
				closestDistSqr = distSqr;
				closestBone = _ragdollRigidbodies[i];
			}
		}

		if (closestBone != null)
		{
			closestBone.AddForceAtPosition(impulse, hitPoint, ForceMode.Impulse);
		}
	}

	// --- ANIMATOR & RIGGING UPDATES ---

	private void CacheAnimatorParameters()
	{
		if (tpAnimator == null) return;

		foreach (AnimatorControllerParameter param in tpAnimator.parameters)
		{
			if (param.nameHash == moveXHash) hasMoveX = true;
			else if (param.nameHash == moveYHash) hasMoveY = true;
			else if (param.nameHash == groundedStanceHash) hasGroundedStance = true;
			else if (param.nameHash == idleStanceHash) hasIdleStance = true;
			else if (param.nameHash == isGroundedHash) hasIsGrounded = true;
			else if (param.nameHash == inVehicleHash) hasInVehicle = true;
			else if (param.nameHash == isArmedHash) hasIsArmed = true;
			else if (param.nameHash == speedHash) hasSpeed = true;
		}
	}

	private void EvaluateLocomotionState()
	{
		_currentState = motor.inVehicle ? AnimationState.Seated : AnimationState.Locomotion;

		if (hasInVehicle) tpAnimator.SetBool(inVehicleHash, _currentState == AnimationState.Seated);
		if (hasIsArmed) tpAnimator.SetBool(isArmedHash, motor.currentWeapon != null);
	}

	private void UpdateAnimatorParameters()
	{
		if (tpAnimator == null) return;

		if (_currentState == AnimationState.Seated)
		{
			if (hasMoveX) tpAnimator.SetFloat(moveXHash, 0f, 0.1f, Time.deltaTime);
			if (hasMoveY) tpAnimator.SetFloat(moveYHash, 0f, 0.1f, Time.deltaTime);
			if (hasIdleStance) tpAnimator.SetFloat(idleStanceHash, 1f, 0.1f, Time.deltaTime);
			return;
		}

		Rigidbody rb = motor.GetComponent<Rigidbody>();
		Transform refTransform = motor.bodyGeometry != null ? motor.bodyGeometry : motor.transform;
		Vector3 localVelocity = refTransform.InverseTransformDirection(rb.linearVelocity);

		float maxSpeed = motor.walkSpeed > 0f ? motor.walkSpeed : 4.5f;
		float xParam = Mathf.Clamp(localVelocity.x / maxSpeed, -1f, 1f);
		float yParam = Mathf.Clamp(localVelocity.z / maxSpeed, -1f, 1f);

		if (hasMoveX) tpAnimator.SetFloat(moveXHash, xParam, 0.1f, Time.deltaTime);
		if (hasMoveY) tpAnimator.SetFloat(moveYHash, yParam, 0.1f, Time.deltaTime);

		float stanceValue = (float)motor.currentStance;
		if (motor.isSprinting) stanceValue = -1f;

		if (hasGroundedStance) tpAnimator.SetFloat(groundedStanceHash, stanceValue, 0.15f, Time.deltaTime);
		if (hasIsGrounded) tpAnimator.SetBool(isGroundedHash, motor.isGrounded);

		float planarSpeed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
		if (hasIdleStance) tpAnimator.SetFloat(idleStanceHash, planarSpeed < 0.1f ? 1f : 0f, 0.1f, Time.deltaTime);

		if (fpAnimator != null && fpAnimator.enabled)
		{
			fpAnimator.SetFloat(speedHash, Mathf.Clamp01(planarSpeed / maxSpeed), 0.1f, Time.deltaTime);
		}
	}

	private void UpdateRiggingWeights()
	{
		if (humanoidRig == null) return;

		if (_currentState == AnimationState.Seated)
		{
			humanoidRig.weight = Mathf.MoveTowards(humanoidRig.weight, 0f, Time.deltaTime * weightBlendSpeed);
			return;
		}

		humanoidRig.weight = Mathf.MoveTowards(humanoidRig.weight, 1f, Time.deltaTime * weightBlendSpeed);

		bool isArmed = motor.currentWeapon != null;

		if (armIKL != null)
		{
			float targetLeftWeight = isArmed ? 1f : 0f;
			armIKL.weight = Mathf.MoveTowards(armIKL.weight, targetLeftWeight, Time.deltaTime * weightBlendSpeed);
		}

		if (weaponAimR != null)
		{
			float targetAimWeight = (isArmed && !motor.isSprinting) ? 1f : 0f;
			weaponAimR.weight = Mathf.MoveTowards(weaponAimR.weight, targetAimWeight, Time.deltaTime * weightBlendSpeed);
		}
	}

	private void UpdateGripTargetPosition()
	{
		if (armIKL_Target == null) return;

		if (_currentGripSocket != null)
		{
			armIKL_Target.position = _currentGripSocket.position;
			armIKL_Target.rotation = _currentGripSocket.rotation;
		}
		else
		{
			armIKL_Target.localPosition = Vector3.zero;
			armIKL_Target.localRotation = Quaternion.identity;
		}
	}

	public void BindLeftHandGrip(Transform gripSocket)
	{
		_currentGripSocket = gripSocket;
		UpdateGripTargetPosition();
	}

	public void SetAim(bool isAiming)
	{
		if (tpAnimator != null && tpAnimator.enabled) tpAnimator.SetBool(isAimingHash, isAiming);
		if (fpAnimator != null && fpAnimator.enabled) fpAnimator.SetBool(isAimingHash, isAiming);
	}

	public void TriggerFire()
	{
		if (tpAnimator != null && tpAnimator.enabled) tpAnimator.SetTrigger(fireTriggerHash);
		if (fpAnimator != null && fpAnimator.enabled) fpAnimator.SetTrigger(fireTriggerHash);
		PlayFPMuzzleFlash();
	}

	public void TriggerReload()
	{
		if (tpAnimator != null && tpAnimator.enabled) tpAnimator.SetTrigger(reloadTriggerHash);
		if (fpAnimator != null && fpAnimator.enabled) fpAnimator.SetTrigger(reloadTriggerHash);
	}

	private void SetLayerRecursively(GameObject obj, int layer)
	{
		obj.layer = layer;
		foreach (Transform child in obj.transform)
		{
			SetLayerRecursively(child.gameObject, layer);
		}
	}
}