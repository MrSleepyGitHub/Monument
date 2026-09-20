using UnityEngine;
using UnityEngine.AI;

public class BotController : MonoBehaviour
{
	public enum BotState { Idle, Seek, Defend, Attack }

	[Header("Possession Authority")]
	[SerializeField] private humanoidMotor currentMotor;
	public humanoidMotor CurrentMotor => currentMotor;

	[Header("Team & Identity")]
	public int teamId = 2;

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

	[Header("Combat & Tactical Motion")]
	public float minAttackDistance = 5f;
	public float maxAttackDistance = 20f;
	public float rotationSpeed = 8f;
	public float repositionRadius = 6f;
	public float repositionIntervalMin = 2.5f;
	public float repositionIntervalMax = 5.0f;

	[Header("Burst Firing")]
	public float burstDurationMin = 0.25f;
	public float burstDurationMax = 0.65f;
	public float burstCooldownMin = 0.4f;
	public float burstCooldownMax = 1.1f;

	[Header("Current State (Read-Only)")]
	[SerializeField] private BotState currentState = BotState.Idle;
	[SerializeField] private Transform currentTarget;

	private NavMeshAgent agent;

	private Vector3 lastKnownTargetPos;
	private float memoryTimer = 0f;
	private float baseMemoryDuration = 6f;
	private float idlePatrolTimer = 0f;
	private Vector3 idleDestination;

	private float burstTimer = 0f;
	private float burstCooldownTimer = 0f;
	private bool isFiringBurst = false;

	private float repositionTimer = 0f;
	private Vector3 repositionDestination;
	private bool isRepositioning = false;

	public void Possess(humanoidMotor newMotor)
	{
		if (currentMotor != null)
		{
			UnpossessCurrent();
		}

		currentMotor = newMotor;
		if (currentMotor == null) return;

		agent = currentMotor.GetComponent<NavMeshAgent>();
		if (agent == null)
		{
			agent = currentMotor.gameObject.AddComponent<NavMeshAgent>();
		}

		agent.enabled = true;
		agent.updatePosition = false;
		agent.updateRotation = false;

		currentMotor.OnDeath.AddListener(OnMotorDied);
		currentMotor.teamId = teamId;

		currentMotor.Possess(this);
		repositionTimer = Random.Range(repositionIntervalMin, repositionIntervalMax);
	}

	public void UnpossessCurrent()
	{
		if (currentMotor != null)
		{
			currentMotor.OnDeath.RemoveListener(OnMotorDied);
			if (agent != null) agent.enabled = false;

			currentMotor.Unpossess();
			currentMotor = null;
		}

		agent = null;
		currentTarget = null;
	}

	public bool IsControllingAliveEntity()
	{
		if (currentMotor == null) return false;
		if (currentMotor.isDead || currentMotor.currentHealth <= 0f) return false;
		if (!currentMotor.enabled && !currentMotor.inVehicle) return false;
		return true;
	}

	private void OnMotorDied()
	{
		if (currentMotor != null && currentMotor.currentWeapon != null)
		{
			currentMotor.currentWeapon.ProcessInput(false, false);
		}

		if (agent != null)
		{
			agent.enabled = false;
		}
	}

	private void Update()
	{
		if (!IsControllingAliveEntity()) return;

		agent.nextPosition = currentMotor.transform.position;

		ScanForTargets();
		EvaluateStateTransitions();
		ExecuteStateBehavior();
	}

	private void ScanForTargets()
	{
		Collider[] hits = Physics.OverlapSphere(currentMotor.transform.position, visionRange, targetMask);
		Transform bestTarget = null;
		float closestDist = Mathf.Infinity;

		for (int i = 0; i < hits.Length; i++)
		{
			Collider hit = hits[i];
			if (hit.transform == currentMotor.transform || hit.transform.IsChildOf(currentMotor.transform)) continue;

			humanoidMotor targetMotor = hit.GetComponentInParent<humanoidMotor>();
			if (targetMotor == null || targetMotor.isDead || targetMotor.currentHealth <= 0f) continue;

			if (targetMotor.teamId == this.teamId || targetMotor.teamId == 0)
			{
				continue;
			}

			Vector3 eyeOrigin = currentMotor.camHolder != null ? currentMotor.camHolder.position : currentMotor.transform.position + Vector3.up * 1.5f;
			Vector3 targetCenter = hit.bounds.center;
			Vector3 dirToTarget = (targetCenter - eyeOrigin).normalized;

			Transform forwardRef = currentMotor.bodyGeometry != null ? currentMotor.bodyGeometry : currentMotor.transform;
			if (Vector3.Angle(forwardRef.forward, dirToTarget) < visionAngle * 0.5f)
			{
				float dist = Vector3.Distance(eyeOrigin, targetCenter);

				// Verify line-of-sight without getting blocked by bot's own hitboxes
				RaycastHit[] obsHits = Physics.RaycastAll(eyeOrigin, dirToTarget, dist, obstructionMask, QueryTriggerInteraction.Ignore);
				bool isObstructed = false;

				for (int j = 0; j < obsHits.Length; j++)
				{
					if (obsHits[j].transform == currentMotor.transform || obsHits[j].transform.IsChildOf(currentMotor.transform))
						continue;
					if (obsHits[j].transform == hit.transform || obsHits[j].transform.IsChildOf(hit.transform))
						continue;

					isObstructed = true;
					break;
				}

				if (!isObstructed)
				{
					if (dist < closestDist)
					{
						closestDist = dist;
						bestTarget = targetMotor.transform;
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
		if (currentMotor == null) return;

		float effectiveHearing = hearingRange * (0.5f + attention * 0.5f);
		if (Vector3.Distance(currentMotor.transform.position, soundOrigin) <= (effectiveHearing + loudnessRadius))
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
			humanoidMotor targetMotor = currentTarget.GetComponentInParent<humanoidMotor>();

			if (targetMotor == null || targetMotor.isDead || targetMotor.currentHealth <= 0f ||
			   (targetMotor.teamId == this.teamId || targetMotor.teamId == 0))
			{
				currentTarget = null;
				isRepositioning = false;
			}
		}

		if (currentMotor != null && currentMotor.maxHealth > 0f)
		{
			float healthRatio = currentMotor.currentHealth / currentMotor.maxHealth;
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
		currentMotor.SetSprintInput(false);
		HandleBurstFiring(false, false);
		idlePatrolTimer -= Time.deltaTime;

		if (idlePatrolTimer <= 0f)
		{
			idlePatrolTimer = Random.Range(3f, 7f) / Mathf.Max(0.1f, attention);
			Vector3 randomPoint = currentMotor.transform.position + Random.insideUnitSphere * 8f;
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
		currentMotor.SetSprintInput(aggression > 0.5f);

		if (Vector3.Distance(currentMotor.transform.position, lastKnownTargetPos) <= 3f)
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

		float dist = Vector3.Distance(currentMotor.transform.position, currentTarget.position);
		float optimalDist = Mathf.Lerp(maxAttackDistance, minAttackDistance, aggression);

		repositionTimer -= Time.deltaTime;
		if (repositionTimer <= 0f)
		{
			repositionTimer = Random.Range(repositionIntervalMin, repositionIntervalMax) / Mathf.Max(0.2f, aggression);
			PickAttackRepositionPoint();
		}

		if (isRepositioning)
		{
			MoveToward(repositionDestination, 1.2f);
			if (Vector3.Distance(currentMotor.transform.position, repositionDestination) <= 1.5f)
			{
				isRepositioning = false;
			}
		}
		else if (dist > optimalDist)
		{
			currentMotor.SetSprintInput(dist > optimalDist * 1.5f && aggression > 0.4f);
			MoveToward(currentTarget.position, optimalDist * 0.8f);
		}
		else
		{
			currentMotor.SetMoveInput(Vector2.zero);
			currentMotor.SetSprintInput(false);
		}

		RotateToward(currentTarget.position);
		AlignVerticalPitch(currentTarget.position + Vector3.up * 1.3f);

		Transform forwardRef = currentMotor.bodyGeometry != null ? currentMotor.bodyGeometry : currentMotor.transform;
		Vector3 dirToTarget = (currentTarget.position - currentMotor.transform.position).normalized;
		bool onTarget = Vector3.Dot(forwardRef.forward, dirToTarget) > 0.82f;

		HandleBurstFiring(true, onTarget);
	}

	private void ExecuteDefend()
	{
		currentMotor.SetSprintInput(true);

		if (currentTarget != null)
		{
			Vector3 retreatDir = (currentMotor.transform.position - currentTarget.position).normalized;
			Vector3 candidatePos = currentMotor.transform.position + retreatDir * 12f;

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

	private void HandleBurstFiring(bool wantsToShoot, bool onTarget)
	{
		if (currentMotor.currentWeapon == null) return;

		if (!wantsToShoot || !onTarget)
		{
			currentMotor.currentWeapon.ProcessInput(false, false);
			isFiringBurst = false;
			return;
		}

		if (isFiringBurst)
		{
			burstTimer -= Time.deltaTime;
			currentMotor.currentWeapon.ProcessInput(true, true);

			if (burstTimer <= 0f)
			{
				isFiringBurst = false;
				burstCooldownTimer = Random.Range(burstCooldownMin, burstCooldownMax) / Mathf.Max(0.2f, aggression);
				currentMotor.currentWeapon.ProcessInput(false, false);
			}
		}
		else
		{
			burstCooldownTimer -= Time.deltaTime;
			currentMotor.currentWeapon.ProcessInput(false, false);

			if (burstCooldownTimer <= 0f)
			{
				isFiringBurst = true;
				burstTimer = Random.Range(burstDurationMin, burstDurationMax);
			}
		}
	}

	private void PickAttackRepositionPoint()
	{
		if (currentTarget == null) return;

		Vector3 toTarget = (currentTarget.position - currentMotor.transform.position).normalized;
		Vector3 strafeDir = Vector3.Cross(toTarget, Vector3.up) * (Random.value > 0.5f ? 1f : -1f);
		Vector3 candidatePos = currentMotor.transform.position + (strafeDir * Random.Range(3f, repositionRadius)) + (toTarget * Random.Range(-2f, 2.5f));

		if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, repositionRadius, NavMesh.AllAreas))
		{
			repositionDestination = hit.position;
			isRepositioning = true;
		}
	}

	private void AlignVerticalPitch(Vector3 targetPoint)
	{
		if (currentMotor.camGimbal == null) return;

		Vector3 eyePos = currentMotor.camGimbal.position;
		Vector3 aimDir = (targetPoint - eyePos).normalized;

		Transform refTransform = currentMotor.bodyGeometry != null ? currentMotor.bodyGeometry : currentMotor.transform;
		Vector3 localAim = refTransform.InverseTransformDirection(aimDir);

		float flatDist = Mathf.Sqrt(localAim.x * localAim.x + localAim.z * localAim.z);
		if (flatDist > 0.001f)
		{
			float desiredPitch = -Mathf.Atan2(localAim.y, flatDist) * Mathf.Rad2Deg;
			float currentPitch = currentMotor.CurrentPitch;
			float pitchDelta = Mathf.DeltaAngle(currentPitch, desiredPitch);
			currentMotor.RotateCamera(pitchDelta * rotationSpeed * Time.deltaTime);
		}
	}

	private void MoveToward(Vector3 destination, float stopDistance)
	{
		agent.stoppingDistance = stopDistance;
		agent.SetDestination(destination);

		if (agent.remainingDistance > agent.stoppingDistance)
		{
			Transform refTransform = currentMotor.bodyGeometry != null ? currentMotor.bodyGeometry : currentMotor.transform;
			Vector3 localDesired = refTransform.InverseTransformDirection(agent.desiredVelocity).normalized;
			currentMotor.SetMoveInput(new Vector2(localDesired.x, localDesired.z));

			bool isFocusingTarget = (currentState == BotState.Attack) ||
								   (currentState == BotState.Defend && aggression > 0.4f && currentTarget != null);

			if (!isFocusingTarget && agent.desiredVelocity.sqrMagnitude > 0.1f)
			{
				RotateToward(currentMotor.transform.position + agent.desiredVelocity);
			}
		}
		else
		{
			currentMotor.SetMoveInput(Vector2.zero);
		}
	}

	private void RotateToward(Vector3 targetWorldPosition)
	{
		Transform refTransform = currentMotor.bodyGeometry != null ? currentMotor.bodyGeometry : currentMotor.transform;

		Vector3 flatDir = Vector3.ProjectOnPlane(targetWorldPosition - refTransform.position, Vector3.up).normalized;
		if (flatDir.sqrMagnitude > 0.001f)
		{
			Quaternion targetRot = Quaternion.LookRotation(flatDir);
			float yAngleDelta = Mathf.DeltaAngle(refTransform.eulerAngles.y, targetRot.eulerAngles.y);

			if (Mathf.Abs(yAngleDelta) > 0.5f)
			{
				float maxStep = rotationSpeed * 50f * Time.deltaTime;
				float step = Mathf.Clamp(yAngleDelta, -maxStep, maxStep);
				currentMotor.Rotate(new Vector3(0f, step, 0f));
			}
		}
	}
}