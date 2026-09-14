using UnityEngine;

public class TeamMember : MonoBehaviour
{
	[Header("Team Setup")]
	[Tooltip("Entities with the identical team ID are considered friendly allies.")]
	public int teamId = 0; // e.g., 0 = Players, 1 = Red Team, 2 = Blue Team

	public bool IsAllied(TeamMember other)
	{
		if (other == null) return false;
		return other.teamId == this.teamId;
	}

	public bool IsAllied(int otherTeamId)
	{
		return otherTeamId == this.teamId;
	}
}