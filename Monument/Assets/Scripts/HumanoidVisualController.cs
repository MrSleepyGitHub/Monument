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
	public Transform fpWeaponHolder;
	public Transform tpWeaponHolder;

	[Header("Animators")]
	[SerializeField] private Animator fpAnimator;
	[SerializeField] private Animator tpAnimator;

	[Header("Blend Tree Controls")]
	[SerializeField] private float scopeSpeed = 10f;
	[SerializeField] private float sprintTransitionSpeed = 8f;
	[SerializeField] private float sprintToShootSpeed = 12f;
	[SerializeField] private float sprintShootThreshold = 1.3f;

	[Header("Animation Rigging References")]
	public Rig humanoidRig;
	public TwoBoneIKConstraint armIKL;
	public TwoBoneIKConstraint armIKR;
	public MultiAimConstraint weaponAimR;
	public Transform armIKL_Target;
	public float weightBlendSpeed = 10f;

	[Header("Layer Names")]
	[SerializeField] private string localPlayerCullLayer = "LocalPlayer_TP";
	[SerializeField] private string fpArmsLayer = "FPS_Arms";

	private Renderer[] _tpRenderers;
	private Rigidbody[] _ragdollRigidbodies;
	private Collider[] _ragdollColliders;
	private Transform _currentGripSocket;
	private GameObject _spawnedFPVisualWeapon;
	private AnimationState _currentState = AnimationState.Locomotion;
	private bool _isRagdolled = false;
	private bool _isLocalPlayer = false;

	private readonly HashSet<int> _tpValidParameters = new HashSet<int>();
	private readonly HashSet<int> _fpValidParameters = new HashSet<int>();

	private readonly int tpMoveXHash = Animator.StringToHash("MoveX");
	private readonly int tpMoveYHash = Animator.StringToHash("MoveY");
	private readonly int tpGroundedStanceHash = Animator.StringToHash("GroundedStance");
	private readonly int tpIdleStanceHash = Animator.StringToHash("IdleStance");
	private readonly int tpIsGroundedHash = Animator.StringToHash("IsGrounded");
	private readonly int tpInVehicleHash = Animator.StringToHash("InVehicle");
	private readonly int tpIsArmedHash = Animator.StringToHash("IsArmed");

	private readonly int fpSpeedHash = Animator.StringToHash("Speed");
	private readonly int fpAimHash = Animator.StringToHash("Aim");
	private readonly int fpShootTriggerHash = Animator.StringToHash("Shoot");

	private float _currentSpeed = 0f;
	private float _currentAim = 0f;
	private float _targetAim = 0f;

	public bool CanShoot => _currentSpeed < sprintShootThreshold;

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
			tpAnimator = tpRigRoot.GetComponentInChildren<Animator>(true);

		if (tpAnimator == null)
			tpAnimator = GetComponentInChildren<Animator>(true);

		if (humanoidRig == null)
			humanoidRig = GetComponentInChildren<Rig>(true);

		if (tpWeaponHolder == null && motor != null)
			tpWeaponHolder = motor.weaponHolder;

		// 1. Resolve FP Rig Root recursively
		if (fpRigRoot == null)
		{
			Transform found = FindRecursive(transform, "FPS_Rig") ??
							  FindRecursive(transform, "FP_Rig") ??
							  FindRecursive(transform, "FP_Arms") ??
							  FindRecursive(transform, "Arms");

			if (found != null) fpRigRoot = found.gameObject;
		}

		// 2. Resolve FP Weapon Holder recursively under fpRigRoot
		if (fpWeaponHolder == null && fpRigRoot != null)
		{
			fpWeaponHolder = FindRecursive(fpRigRoot.transform, "FP_WeaponSocket") ??
							 FindRecursive(fpRigRoot.transform, "WeaponHolder") ??
							 FindRecursive(fpRigRoot.transform, "WeaponSocket") ??
							 FindRecursive(fpRigRoot.transform, "Socket");
		}

		if (fpAnimator == null && fpRigRoot != null)
		{
			fpAnimator = fpRigRoot.GetComponentInChildren<Animator>(true);
		}
	}

	private static Transform FindRecursive(Transform parent, string targetName)
	{
		if (parent == null) return null;
		if (parent.name == targetName) return parent;

		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);
			if (child.name == targetName) return child;
			Transform found = FindRecursive(child, targetName);
			if (found != null) return found;
		}
		return null;
	}

	private void Update()
	{
		if (_isRagdolled || motor == null) return;

		EvaluateLocomotionState();
		UpdateAnimatorParameters();
		UpdateRiggingWeights();
		UpdateGripTargetPosition();
	}

	private void CacheAnimatorParameters()
	{
		_tpValidParameters.Clear();
		if (tpAnimator != null && tpAnimator.runtimeAnimatorController != null)
		{
			foreach (AnimatorControllerParameter p in tpAnimator.parameters)
			{
				_tpValidParameters.Add(p.nameHash);
			}
		}

		_fpValidParameters.Clear();
		if (fpAnimator != null && fpAnimator.runtimeAnimatorController != null)
		{
			foreach (AnimatorControllerParameter p in fpAnimator.parameters)
			{
				_fpValidParameters.Add(p.nameHash);
			}
		}
	}

	private void SetTpBoolSafe(int hash, bool val)
	{
		if (tpAnimator != null && tpAnimator.enabled && _tpValidParameters.Contains(hash))
			tpAnimator.SetBool(hash, val);
	}

	private void SetTpFloatSafe(int hash, float val, float dampTime = 0f, float dt = 0f)
	{
		if (tpAnimator != null && tpAnimator.enabled && _tpValidParameters.Contains(hash))
		{
			if (dampTime > 0f) tpAnimator.SetFloat(hash, val, dampTime, dt);
			else tpAnimator.SetFloat(hash, val);
		}
	}

	private void SetFpFloatSafe(int hash, float val)
	{
		if (fpAnimator != null && fpAnimator.enabled && _fpValidParameters.Contains(hash))
			fpAnimator.SetFloat(hash, val);
	}

	private void SetFpTriggerSafe(int hash)
	{
		if (fpAnimator != null && fpAnimator.enabled && _fpValidParameters.Contains(hash))
		{
			fpAnimator.ResetTrigger(hash);
			fpAnimator.SetTrigger(hash);
		}
	}

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
		AutoResolveReferences();
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

				foreach (Renderer r in fpRigRoot.GetComponentsInChildren<Renderer>(true))
				{
					r.enabled = true;
				}
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

		if (tpLocalLayer != -1)
		{
			worldCam.cullingMask &= ~(1 << tpLocalLayer);
		}

		Camera overlayCam = null;
		Camera[] allCams = motor.GetComponentsInChildren<Camera>(true);
		for (int i = 0; i < allCams.Length; i++)
		{
			if (allCams[i] != worldCam)
			{
				overlayCam = allCams[i];
				break;
			}
		}

		if (fpLayer != -1)
		{
			if (overlayCam != null)
			{
				worldCam.cullingMask &= ~(1 << fpLayer);
				overlayCam.cullingMask = (1 << fpLayer);
			}
			else
			{
				// Single camera: MUST include the FPS_Arms layer
				worldCam.cullingMask |= (1 << fpLayer);
			}
		}
	}

	public Weapon SetupWeaponForPerspective(Weapon weapon, bool isLocal)
	{
		if (weapon == null) return null;

		AutoResolveReferences();
		ClearFPWeapons();

		Transform targetTpParent = tpWeaponHolder != null ? tpWeaponHolder : transform;
		weapon.transform.SetParent(targetTpParent);
		weapon.transform.localPosition = Vector3.zero;
		weapon.transform.localRotation = Quaternion.identity;

		weapon.ConfigureWorldPhysics(false);
		weapon.SetOwner(motor);

		GameObject worldModel = weapon.EnsureWorldModel();

		int fpLayer = LayerMask.NameToLayer(fpArmsLayer);
		int tpLocalLayer = LayerMask.NameToLayer(localPlayerCullLayer);
		int defaultLayer = LayerMask.NameToLayer("Default");

		if (isLocal)
		{
			if (tpLocalLayer != -1) weapon.gameObject.layer = tpLocalLayer;
			else if (defaultLayer != -1) weapon.gameObject.layer = defaultLayer;

			if (worldModel != null)
			{
				worldModel.SetActive(true);

				foreach (var rend in worldModel.GetComponentsInChildren<Renderer>(true))
				{
					if (rend is ParticleSystemRenderer psr) psr.enabled = false;
					else if (rend is TrailRenderer tr) tr.enabled = false;
					else if (rend is LineRenderer lr) lr.enabled = false;
					else rend.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
				}

				foreach (var light in worldModel.GetComponentsInChildren<Light>(true)) light.enabled = false;
				foreach (var ps in worldModel.GetComponentsInChildren<ParticleSystem>(true))
				{
					var em = ps.emission;
					em.enabled = false;
				}

				if (tpLocalLayer != -1)
					SetLayerRecursively(worldModel, tpLocalLayer);
				else if (defaultLayer != -1)
					SetLayerRecursively(worldModel, defaultLayer);
			}

			// Instantiate First-Person Model
			if (weapon.firstPersonModelPrefab != null && fpWeaponHolder != null)
			{
				_spawnedFPVisualWeapon = Instantiate(weapon.firstPersonModelPrefab, fpWeaponHolder);
				_spawnedFPVisualWeapon.name = $"[FP] {weapon.firstPersonModelPrefab.name}";
				_spawnedFPVisualWeapon.transform.localPosition = Vector3.zero;
				_spawnedFPVisualWeapon.transform.localRotation = Quaternion.identity;
				_spawnedFPVisualWeapon.transform.localScale = Vector3.one;

				foreach (var col in _spawnedFPVisualWeapon.GetComponentsInChildren<Collider>(true)) col.enabled = false;
				foreach (var rb in _spawnedFPVisualWeapon.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.detectCollisions = false; }
				foreach (var aud in _spawnedFPVisualWeapon.GetComponentsInChildren<AudioSource>(true)) aud.enabled = false;

				foreach (var rend in _spawnedFPVisualWeapon.GetComponentsInChildren<Renderer>(true))
				{
					rend.shadowCastingMode = ShadowCastingMode.Off;
					rend.enabled = true;
				}

				if (fpLayer != -1) SetLayerRecursively(_spawnedFPVisualWeapon, fpLayer);
			}
			else
			{
				if (weapon.firstPersonModelPrefab == null)
				{
					Debug.LogWarning($"[HumanoidVisualController] Weapon '{weapon.name}' is missing its firstPersonModelPrefab assignment in the inspector!");
				}
				if (fpWeaponHolder == null)
				{
					Debug.LogWarning($"[HumanoidVisualController] fpWeaponHolder could not be found under fpRigRoot on '{gameObject.name}'!");
				}
			}

			BindFPAnimator();
		}
		else
		{
			if (defaultLayer != -1) weapon.gameObject.layer = defaultLayer;

			if (worldModel != null)
			{
				worldModel.SetActive(true);

				foreach (var rend in worldModel.GetComponentsInChildren<Renderer>(true))
				{
					rend.shadowCastingMode = ShadowCastingMode.On;
					rend.enabled = true;
				}

				foreach (var ps in worldModel.GetComponentsInChildren<ParticleSystem>(true))
				{
					var em = ps.emission;
					em.enabled = true;
				}

				foreach (var light in worldModel.GetComponentsInChildren<Light>(true)) light.enabled = true;

				if (defaultLayer != -1) SetLayerRecursively(worldModel, defaultLayer);
			}
		}

		BindLeftHandGrip(weapon.leftHandGrip);
		return weapon;
	}

	private void BindFPAnimator()
	{
		Animator targetAnim = null;

		if (_spawnedFPVisualWeapon != null)
			targetAnim = _spawnedFPVisualWeapon.GetComponentInChildren<Animator>(true);

		if (targetAnim == null && fpRigRoot != null)
			targetAnim = fpRigRoot.GetComponentInChildren<Animator>(true);

		if (targetAnim != null)
		{
			fpAnimator = targetAnim;
			fpAnimator.enabled = true;
			fpAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			CacheAnimatorParameters();
		}
	}

	public void RestoreWeaponForWorld(Weapon weapon)
	{
		if (weapon == null) return;

		int interactableLayer = LayerMask.NameToLayer("Interactable");
		if (interactableLayer == -1) interactableLayer = LayerMask.NameToLayer("Default");

		GameObject worldModel = weapon.EnsureWorldModel();
		if (worldModel != null)
		{
			worldModel.SetActive(true);

			foreach (var rend in worldModel.GetComponentsInChildren<Renderer>(true))
			{
				rend.enabled = true;
				rend.shadowCastingMode = ShadowCastingMode.On;
			}

			foreach (var col in worldModel.GetComponentsInChildren<Collider>(true))
			{
				col.enabled = true;
			}

			foreach (var light in worldModel.GetComponentsInChildren<Light>(true)) light.enabled = true;
			foreach (var ps in worldModel.GetComponentsInChildren<ParticleSystem>(true))
			{
				var em = ps.emission;
				em.enabled = true;
			}

			if (interactableLayer != -1) SetLayerRecursively(worldModel, interactableLayer);
		}

		if (interactableLayer != -1)
		{
			SetLayerRecursively(weapon.gameObject, interactableLayer);
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

	public void SetAim(bool isAiming)
	{
		_targetAim = (isAiming && !motor.isSprinting) ? 1f : 0f;
	}

	public void TriggerFire()
	{
		if (!CanShoot) return;

		SetFpTriggerSafe(fpShootTriggerHash);
		PlayFPMuzzleFlash();
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

	public void DisableAndClearRig()
	{
		if (humanoidRig != null) humanoidRig.weight = 0f;
	}

	public void EnableRagdoll(Vector3 inheritedVelocity)
	{
		if (_isRagdolled) return;
		_isRagdolled = true;

		DisableAndClearRig();

		if (tpAnimator != null) tpAnimator.enabled = false;
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

	private void EvaluateLocomotionState()
	{
		_currentState = motor.inVehicle ? AnimationState.Seated : AnimationState.Locomotion;

		SetTpBoolSafe(tpInVehicleHash, _currentState == AnimationState.Seated);
		SetTpBoolSafe(tpIsArmedHash, motor.currentWeapon != null);
	}

	private void UpdateAnimatorParameters()
	{
		Rigidbody rb = motor.GetComponent<Rigidbody>();
		Transform refTransform = motor.bodyGeometry != null ? motor.bodyGeometry : motor.transform;
		Vector3 localVelocity = refTransform.InverseTransformDirection(rb.linearVelocity);
		float maxSpeed = motor.walkSpeed > 0f ? motor.walkSpeed : 4.5f;
		float planarSpeed = new Vector2(localVelocity.x, localVelocity.z).magnitude;

		float targetSpeed = 0f;
		if (motor.isSprinting)
		{
			targetSpeed = 2f;
		}
		else if (planarSpeed > 0.1f)
		{
			targetSpeed = Mathf.Clamp(planarSpeed / maxSpeed, 0.1f, 1f);
		}

		float speedBlendRate = (targetSpeed < _currentSpeed) ? sprintToShootSpeed : sprintTransitionSpeed;
		_currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, Time.deltaTime * speedBlendRate);

		_currentAim = Mathf.MoveTowards(_currentAim, _targetAim, Time.deltaTime * scopeSpeed);

		SetFpFloatSafe(fpSpeedHash, _currentSpeed);
		SetFpFloatSafe(fpAimHash, _currentAim);

		if (_currentState == AnimationState.Seated)
		{
			SetTpFloatSafe(tpMoveXHash, 0f, 0.1f, Time.deltaTime);
			SetTpFloatSafe(tpMoveYHash, 0f, 0.1f, Time.deltaTime);
			SetTpFloatSafe(tpIdleStanceHash, 1f, 0.1f, Time.deltaTime);
		}
		else
		{
			float xParam = Mathf.Clamp(localVelocity.x / maxSpeed, -1f, 1f);
			float yParam = Mathf.Clamp(localVelocity.z / maxSpeed, -1f, 1f);

			SetTpFloatSafe(tpMoveXHash, xParam, 0.1f, Time.deltaTime);
			SetTpFloatSafe(tpMoveYHash, yParam, 0.1f, Time.deltaTime);

			float stanceValue = (float)motor.currentStance;
			if (motor.isSprinting) stanceValue = -1f;

			SetTpFloatSafe(tpGroundedStanceHash, stanceValue, 0.15f, Time.deltaTime);
			SetTpBoolSafe(tpIsGroundedHash, motor.isGrounded);
			SetTpFloatSafe(tpIdleStanceHash, planarSpeed < 0.1f ? 1f : 0f, 0.1f, Time.deltaTime);
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

		bool isArmed = motor.currentWeapon != null;

		if (armIKL != null)
		{
			float targetLeftWeight = isArmed ? 1f : 0f;
			armIKL.weight = Mathf.MoveTowards(armIKL.weight, targetLeftWeight, Time.deltaTime * weightBlendSpeed);
		}

		if (weaponAimR != null)
		{
			var sources = weaponAimR.data.sourceObjects;
			bool hasTarget = sources.Count > 0 && sources[0].transform != null;
			float targetAimWeight = (isArmed && !motor.isSprinting && hasTarget) ? 1f : 0f;
			weaponAimR.weight = Mathf.MoveTowards(weaponAimR.weight, targetAimWeight, Time.deltaTime * weightBlendSpeed);
		}

		humanoidRig.weight = Mathf.MoveTowards(humanoidRig.weight, 1f, Time.deltaTime * weightBlendSpeed);
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

	private void SetLayerRecursively(GameObject obj, int layer)
	{
		obj.layer = layer;
		foreach (Transform child in obj.transform)
		{
			SetLayerRecursively(child.gameObject, layer);
		}
	}
}