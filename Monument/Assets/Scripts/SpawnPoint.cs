using System.Collections.Generic;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
	public enum SpawnRole { Any, PlayerOnly, BotOnly }

	[Header("Role & Team Configuration")]
	[Tooltip("Restricts which entity type can use this spawn point.")]
	public SpawnRole allowedRole = SpawnRole.Any;

	[Tooltip("Team ID for this spawn point. Set to 0 for neutral / open to any team.")]
	public int teamId = 0;

	[Header("Clearance Check")]
	public float clearanceRadius = 1.2f;
	public LayerMask clearanceObstructionMask = ~0;

	[Header("Occupant State (Read-Only)")]
	[SerializeField] private GameObject currentOccupant;

	private static readonly List<SpawnPoint> activeSpawnPoints = new List<SpawnPoint>();

	private void OnEnable()
	{
		if (!activeSpawnPoints.Contains(this))
		{
			activeSpawnPoints.Add(this);
		}
	}

	private void OnDisable()
	{
		activeSpawnPoints.Remove(this);
	}

	public bool IsAvailable()
	{
		// 1. Check active occupant reference
		if (currentOccupant != null)
		{
			humanoidMotor occupantMotor = currentOccupant.GetComponent<humanoidMotor>();
			bool isOccupantAlive = occupantMotor == null || (!occupantMotor.isDead && occupantMotor.currentHealth > 0f);

			if (isOccupantAlive && Vector3.Distance(transform.position, currentOccupant.transform.position) <= clearanceRadius * 1.5f)
			{
				return false;
			}
			currentOccupant = null;
		}

		// 2. Physical overlap test for living motors and vehicles
		Vector3 checkCenter = transform.position + Vector3.up * 0.5f;
		Collider[] hits = Physics.OverlapSphere(checkCenter, clearanceRadius, clearanceObstructionMask, QueryTriggerInteraction.Ignore);

		for (int i = 0; i < hits.Length; i++)
		{
			humanoidMotor motor = hits[i].GetComponentInParent<humanoidMotor>();
			if (motor != null && !motor.isDead)
			{
				return false;
			}

			Vehicle vehicle = hits[i].GetComponentInParent<Vehicle>();
			if (vehicle != null)
			{
				return false;
			}
		}

		return true;
	}

	public void Claim(GameObject entity)
	{
		currentOccupant = entity;
	}

	public static SpawnPoint GetAvailableSpawnPoint(SpawnRole role, int requestedTeamId, bool randomPick = true)
	{
		// Try exact role match first, then fall back to Any if role was PlayerOnly or BotOnly
		SpawnPoint point = QuerySpawnList(role, requestedTeamId, randomPick);
		if (point == null && role != SpawnRole.Any)
		{
			point = QuerySpawnList(SpawnRole.Any, requestedTeamId, randomPick);
		}
		return point;
	}

	private static SpawnPoint QuerySpawnList(SpawnRole role, int requestedTeamId, bool randomPick)
	{
		List<SpawnPoint> exactTeamCandidates = new List<SpawnPoint>();
		List<SpawnPoint> neutralTeamCandidates = new List<SpawnPoint>();

		for (int i = 0; i < activeSpawnPoints.Count; i++)
		{
			SpawnPoint sp = activeSpawnPoints[i];
			if (sp == null || !sp.IsAvailable()) continue;

			bool roleMatches = sp.allowedRole == role;
			if (!roleMatches) continue;

			if (sp.teamId == requestedTeamId && requestedTeamId != 0)
			{
				exactTeamCandidates.Add(sp);
			}
			else if (sp.teamId == 0)
			{
				neutralTeamCandidates.Add(sp);
			}
		}

		List<SpawnPoint> finalCandidates = exactTeamCandidates.Count > 0 ? exactTeamCandidates : neutralTeamCandidates;
		if (finalCandidates.Count == 0) return null;

		return randomPick ? finalCandidates[Random.Range(0, finalCandidates.Count)] : finalCandidates[0];
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = allowedRole switch
		{
			SpawnRole.PlayerOnly => Color.cyan,
			SpawnRole.BotOnly => new Color(1f, 0.4f, 0f),
			_ => Color.green
		};

		Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, clearanceRadius);
		Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
	}
}