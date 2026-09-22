using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class GameMode : MonoBehaviour
{
	public static GameMode Instance { get; private set; }

	[Header("Respawn Configuration")]
	[Tooltip("Time in seconds a player must wait after dying before respawning.")]
	public float respawnDelay = 5.0f;
	[Tooltip("If true, the player automatically spawns as soon as a point is clear.")]
	public bool autoRespawn = false;

	[Header("Match Rules")]
	public int targetScoreToWin = 50;
	public float matchTimeLimitMinutes = 15f;
	public bool friendlyFire = false;

	[Header("Team Scores")]
	public int team1Score = 0;
	public int team2Score = 0;
	public float matchElapsedTime { get; private set; } = 0f;
	public bool isMatchOver { get; private set; } = false;

	[Header("Events")]
	public UnityEvent<int, int> OnScoreChanged;
	public UnityEvent<int> OnMatchWon;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
	}

	private void Update()
	{
		if (isMatchOver) return;
		matchElapsedTime += Time.deltaTime;
	}

	public void AddScore(int teamId, int points = 1)
	{
		if (isMatchOver) return;

		if (teamId == 1) team1Score += points;
		else if (teamId == 2) team2Score += points;

		OnScoreChanged?.Invoke(team1Score, team2Score);

		if (team1Score >= targetScoreToWin) EndMatch(1);
		else if (team2Score >= targetScoreToWin) EndMatch(2);
	}

	private void EndMatch(int winningTeam)
	{
		isMatchOver = true;
		OnMatchWon?.Invoke(winningTeam);
	}
}