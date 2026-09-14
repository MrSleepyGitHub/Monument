using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(humanoidMotor))]
[RequireComponent(typeof(NavMeshAgent))]
public class BotController : MonoBehaviour
{
	public enum BotState { Idle, Seek, Defend, Attack }

	[Header("Team Setup")]
	public int teamId = 1;

	[Header("Starter Weapon")]
	public GameObject starterWeaponPrefab;
	public Transform weaponHolder;
	public Weapon currentWeapon;

	[Header("Personality Traits (0 - 1)")]
	[Range(0f, 1f)] public float aggression = 0.6f;
	[Range(0f, 1f)] public float fear = 0.3f;
	[Range(0f, 1f)] public float attention = 0.7f;

	[Header("Sensory Ranges")]
	public float visionRange = 30f;
	[Range(30f, 180f)] public float visionAngle = 110f;
	public float hearingRange = 18f;
	public LayerMask targetMask;
	public LayerMask obstructionMask;

	[Header("Combat & Engagement")]
	public float minAttackDistance = 5f;
	public float maxAttackDistance = 20f;
	public float rotationSpeed = 8f;

	[Header("Burst Firing")]
	public float burstDurationMin = 0.25f;
	public float burstDurationMax = 0.65f;
	public float burstCooldownMin = 0.4f;
	public float burstCooldownMax = 1.1f;

	[Header("Repositioning")]
	public float repositionIntervalMin = 2.5f;
	public float repositionIntervalMax = 5.0f;
	public float repositionRadius = 6f;

	[Header("Current State (Read-Only)")]
	[SerializeField] private BotState currentState = BotState.Idle;
	[SerializeField] private Transform currentTarget;

	private humanoidMotor motor;
	private NavMeshAgent agent;
	private Health health;
	private TeamMember teamMember;

	private Vector3 lastKnownTargetPos;
	private float memoryTimer = 0f;
	private float baseMemoryDuration = 6f;
	private float idlePatrolTimer = 0f;
	private Vector3 idleDestination;

	// Burst firing internal tracking
	private float burstTimer = 0f;
	private float burstCooldownTimer = 0f;
	private bool isFiringBurst = false;

	// Reposition internal tracking
	private float repositionTimer = 0f;
	private Vector3 repositionDestination;
	private bool isRepositioning = false;

	private void Awake()
	{
		motor = GetComponent<humanoidMotor>();
		agent = GetComponent<NavMeshAgent>();
		health = GetComponent<Health>();
		teamMember = GetComponent<TeamMember>();

		if (teamMember != null) teamId = teamMember.teamId;

		agent.updatePosition = false;
		agent.updateRotation = false;

		if (motor != null && motor.cameraHolder != null)
		{
			Camera botCam = motor.cameraHolder.GetComponentInChildren<Camera>();
			if (botCam != null) botCam.enabled = false;

			AudioListener listener = motor.cameraHolder.GetComponentInChildren<AudioListener>();
			if (listener != null) listener.enabled = false;
		}
	}

	private void Start()
	{
		SpawnStarterWeapon();
		repositionTimer = Random.Range(repositionIntervalMin, repositionIntervalMax);
	}

	private void SpawnStarterWeapon()
	{
		if (starterWeaponPrefab == null || weaponHolder == null) return;

		GameObject weaponInstance = Instantiate(starterWeaponPrefab, weaponHolder);
		weaponInstance.transform.localPosition = Vector3.zero;
		weaponInstance.transform.localRotation = Quaternion.identity;

		currentWeapon = weaponInstance.GetComponent<Weapon>();

		if (currentWeapon != null)
		{
			Rigidbody wRb = weaponInstance.GetComponent<Rigidbody>();
			if (wRb != null) wRb.isKinematic = true;

			Collider wCol = weaponInstance.GetComponent<Collider>();
			if (wCol != null) wCol.enabled = false;

			AnimatorHumanoid anim = GetComponent<AnimatorHumanoid>();
			if (anim != null)
			{
				anim.leftHandGrip = currentWeapon.leftHandGrip;
				currentWeapon.aimTarget = anim.playerAim;
			}
		}
	}

	private void Update()
	{
		agent.nextPosition = transform.position;

		ScanForTargets();
		EvaluateStateTransitions();
		ExecuteStateBehavior();
	}

	private void ScanForTargets()
	{
		Collider[] hits = Physics.OverlapSphere(transform.position, visionRange, targetMask);
		Transform bestTarget = null;
		float closestDist = Mathf.Infinity;

		foreach (Collider hit in hits)
		{
			if (hit.transform == this.transform || hit.transform.IsChildOf(this.transform)) continue;

			Health targetHealth = hit.GetComponentInParent<Health>();
			if (targetHealth == null || targetHealth.currentHealth <= 0f) continue;

			TeamMember targetTeam = hit.GetComponentInParent<TeamMember>();
			if (targetTeam != null && (targetTeam.teamId == this.teamId || targetTeam.teamId == 0))
			{
				continue;
			}

			Vector3 eyeOrigin = motor.cameraHolder != null ? motor.cameraHolder.position : transform.position + Vector3.up * 1.5f;
			Vector3 targetCenter = hit.bounds.center;
			Vector3 dirToTarget = (targetCenter - eyeOrigin).normalized;

			Transform forwardRef = motor.bodyGeometry != null ? motor.bodyGeometry : transform;
			if (Vector3.Angle(forwardRef.forward, dirToTarget) < visionAngle * 0.5f)
			{
				float dist = Vector3.Distance(eyeOrigin, targetCenter);

				if (!Physics.Raycast(eyeOrigin, dirToTarget, dist, obstructionMask))
				{
					if (dist < closestDist)
					{
						closestDist = dist;
						bestTarget = targetHealth.transform;
					}
				}
			}
		}

		if (bestTarget != null)
		{
			currentTarget = bestTarget;
			lastKnownTargetPos = currentTarget.position;
			memoryTimer = baseMemoryDuration * (0.5f + attention);
		}
		else if (memoryTimer > 0f)
		{
			memoryTimer -= Time.deltaTime;
			if (memoryTimer <= 0f) currentTarget = null;
		}
	}

	public void HearNoise(Vector3 soundOrigin, float loudnessRadius)
	{
		float effectiveHearing = hearingRange * (0.5f + attention * 0.5f);
		if (Vector3.Distance(transform.position, soundOrigin) <= (effectiveHearing + loudnessRadius))
		{
			lastKnownTargetPos = soundOrigin;
			memoryTimer = baseMemoryDuration * (0.5f + attention);

			if (currentState == BotState.Idle)
			{
				currentState = BotState.Seek;
			}
		}
	}

	private void EvaluateStateTransitions()
	{
		if (currentTarget != null)
		{
			Health targetHealth = currentTarget.GetComponentInParent<Health>();
			TeamMember targetTeam = currentTarget.GetComponentInParent<TeamMember>();

			if (targetHealth == null || targetHealth.currentHealth <= 0f ||
			   (targetTeam != null && (targetTeam.teamId == this.teamId || targetTeam.teamId == 0)))
			{
				currentTarget = null;
				isRepositioning = false;
			}
		}

		if (health != null && health.maxHealth > 0f)
		{
			float healthRatio = health.currentHealth / health.maxHealth;
			if (healthRatio <= fear && currentTarget != null)
			{
				currentState = BotState.Defend;
				return;
			}
		}

		if (currentTarget != null)
		{
			currentState = BotState.Attack;
		}
		else if (memoryTimer > 0f)
		{
			currentState = BotState.Seek;
		}
		else
		{
			currentState = BotState.Idle;
			isRepositioning = false;
		}
	}

	private void ExecuteStateBehavior()
	{
		switch (currentState)
		{
			case BotState.Idle:
				ExecuteIdle();
				break;
			case BotState.Seek:
				ExecuteSeek();
				break;
			case BotState.Attack:
				ExecuteAttack();
				break;
			case BotState.Defend:
				ExecuteDefend();
				break;
		}
	}

	private void ExecuteIdle()
	{
		motor.SetSprintInput(false);
		HandleBurstFiring(false, false);
		idlePatrolTimer -= Time.deltaTime;

		if (idlePatrolTimer <= 0f)
		{
			idlePatrolTimer = Random.Range(3f, 7f) / Mathf.Max(0.1f, attention);
			Vector3 randomPoint = transform.position + Random.insideUnitSphere * 8f;
			if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 8f, NavMesh.AllAreas))
			{
				idleDestination = hit.position;
			}
		}

		MoveToward(idleDestination, 1.5f);
	}

	private void ExecuteSeek()
	{
		HandleBurstFiring(false, false);
		motor.SetSprintInput(aggression > 0.5f);

		// Reposition/sweep search points when arriving near the target's last known location
		if (Vector3.Distance(transform.position, lastKnownTargetPos) <= 3f)
		{
			repositionTimer -= Time.deltaTime;
			if (repositionTimer <= 0f || !isRepositioning)
			{
				repositionTimer = Random.Range(2f, 4f);
				Vector3 searchPoint = lastKnownTargetPos + Random.insideUnitSphere * repositionRadius;
				if (NavMesh.SamplePosition(searchPoint, out NavMeshHit hit, repositionRadius, NavMesh.AllAreas))
				{
					repositionDestination = hit.position;
					isRepositioning = true;
				}
			}

			if (isRepositioning)
			{
				MoveToward(repositionDestination, 1.5f);
			}
			memoryTimer -= Time.deltaTime * 1.5f;
		}
		else
		{
			MoveToward(lastKnownTargetPos, 2f);
		}
	}

	private void ExecuteAttack()
	{
		if (currentTarget == null) return;

		float dist = Vector3.Distance(transform.position, currentTarget.position);
		float optimalDist = Mathf.Lerp(maxAttackDistance, minAttackDistance, aggression);

		// --- Tactical Repositioning Cycle ---
		repositionTimer -= Time.deltaTime;
		if (repositionTimer <= 0f)
		{
			repositionTimer = Random.Range(repositionIntervalMin, repositionIntervalMax) / Mathf.Max(0.2f, aggression);
			PickAttackRepositionPoint();
		}

		if (isRepositioning)
		{
			MoveToward(repositionDestination, 1.2f);
			if (Vector3.Distance(transform.position, repositionDestination) <= 1.5f)
			{
				isRepositioning = false;
			}
		}
		else if (dist > optimalDist)
		{
			motor.SetSprintInput(dist > optimalDist * 1.5f && aggression > 0.4f);
			MoveToward(currentTarget.position, optimalDist * 0.8f);
		}
		else
		{
			motor.SetMoveInput(Vector2.zero);
			motor.SetSprintInput(false);
		}

		// Aim alignment
		RotateToward(currentTarget.position);
		AlignVerticalPitch(currentTarget.position + Vector3.up * 1.3f);

		// Burst firing execution
		Transform forwardRef = motor.bodyGeometry != null ? motor.bodyGeometry : transform;
		Vector3 dirToTarget = (currentTarget.position - transform.position).normalized;
		bool onTarget = Vector3.Dot(forwardRef.forward, dirToTarget) > 0.82f;

		HandleBurstFiring(true, onTarget);
	}

	private void ExecuteDefend()
	{
		motor.SetSprintInput(true);

		if (currentTarget != null)
		{
			Vector3 retreatDir = (transform.position - currentTarget.position).normalized;
			Vector3 candidatePos = transform.position + retreatDir * 12f;

			if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
			{
				MoveToward(hit.position, 1f);
			}

			if (aggression > 0.4f)
			{
				RotateToward(currentTarget.position);
				AlignVerticalPitch(currentTarget.position + Vector3.up * 1.3f);
				HandleBurstFiring(true, true);
			}
			else
			{
				HandleBurstFiring(false, false);
			}
		}
	}

	// --- BURST FIRING LOGIC ---
	private void HandleBurstFiring(bool wantsToShoot, bool onTarget)
	{
		if (currentWeapon == null) return;

		if (!wantsToShoot || !onTarget)
		{
			currentWeapon.ProcessInput(false, false);
			isFiringBurst = false;
			return;
		}

		if (isFiringBurst)
		{
			burstTimer -= Time.deltaTime;
			currentWeapon.ProcessInput(true, true);

			if (burstTimer <= 0f)
			{
				isFiringBurst = false;
				burstCooldownTimer = Random.Range(burstCooldownMin, burstCooldownMax) / Mathf.Max(0.2f, aggression);
				currentWeapon.ProcessInput(false, false);
			}
		}
		else
		{
			burstCooldownTimer -= Time.deltaTime;
			currentWeapon.ProcessInput(false, false);

			if (burstCooldownTimer <= 0f)
			{
				isFiringBurst = true;
				burstTimer = Random.Range(burstDurationMin, burstDurationMax);
			}
		}
	}

	// --- TACTICAL FLANKING / REPOSITIONING ---
	private void PickAttackRepositionPoint()
	{
		if (currentTarget == null) return;

		Vector3 toTarget = (currentTarget.position - transform.position).normalized;

		// Strafe perpendicular to the target (flanking left or right) with slight depth shifts
		Vector3 strafeDir = Vector3.Cross(toTarget, Vector3.up) * (Random.value > 0.5f ? 1f : -1f);
		Vector3 candidatePos = transform.position + (strafeDir * Random.Range(3f, repositionRadius)) + (toTarget * Random.Range(-2f, 2.5f));

		if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, repositionRadius, NavMesh.AllAreas))
		{
			repositionDestination = hit.position;
			isRepositioning = true;
		}
	}

	private void AlignVerticalPitch(Vector3 targetPoint)
	{
		if (motor.cameraHolder == null) return;

		Vector3 eyePos = motor.cameraHolder.position;
		Vector3 aimDir = (targetPoint - eyePos).normalized;

		Transform refTransform = motor.bodyGeometry != null ? motor.bodyGeometry : transform;
		Vector3 localAim = refTransform.InverseTransformDirection(aimDir);
		float pitchAngle = -Mathf.Atan2(localAim.y, Mathf.Sqrt(localAim.x * localAim.x + localAim.z * localAim.z)) * Mathf.Rad2Deg;

		float currentPitch = motor.cameraHolder.localEulerAngles.x;
		if (currentPitch > 180f) currentPitch -= 360f;

		float pitchDelta = Mathf.DeltaAngle(currentPitch, pitchAngle);
		motor.RotateCamera(pitchDelta * rotationSpeed * Time.deltaTime);
	}

	private void MoveToward(Vector3 destination, float stopDistance)
	{
		agent.stoppingDistance = stopDistance;
		agent.SetDestination(destination);

		if (agent.remainingDistance > agent.stoppingDistance)
		{
			Transform refTransform = motor.bodyGeometry != null ? motor.bodyGeometry : transform;
			Vector3 localDesired = refTransform.InverseTransformDirection(agent.desiredVelocity).normalized;
			motor.SetMoveInput(new Vector2(localDesired.x, localDesired.z));

			bool isFocusingTarget = (currentState == BotState.Attack) ||
								   (currentState == BotState.Defend && aggression > 0.4f && currentTarget != null);

			if (!isFocusingTarget && agent.desiredVelocity.sqrMagnitude > 0.1f)
			{
				RotateToward(transform.position + agent.desiredVelocity);
			}
		}
		else
		{
			motor.SetMoveInput(Vector2.zero);
		}
	}

	private void RotateToward(Vector3 targetWorldPosition)
	{
		Transform refTransform = motor.bodyGeometry != null ? motor.bodyGeometry : transform;

		Vector3 flatDir = Vector3.ProjectOnPlane(targetWorldPosition - refTransform.position, Vector3.up).normalized;
		if (flatDir.sqrMagnitude > 0.001f)
		{
			Quaternion targetRot = Quaternion.LookRotation(flatDir);
			float yAngleDelta = Mathf.DeltaAngle(refTransform.eulerAngles.y, targetRot.eulerAngles.y);

			if (Mathf.Abs(yAngleDelta) > 0.5f)
			{
				float maxStep = rotationSpeed * 50f * Time.deltaTime;
				float step = Mathf.Clamp(yAngleDelta, -maxStep, maxStep);
				motor.Rotate(new Vector3(0f, step, 0f));
			}
		}
	}
}