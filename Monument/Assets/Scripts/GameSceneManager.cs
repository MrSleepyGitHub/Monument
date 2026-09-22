using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameSceneManager : MonoBehaviour
{
	[Header("Persistent Singletons")]
	[Tooltip("The PlayerMaster prefab to instantiate if one doesn't exist in DontDestroyOnLoad.")]
	[SerializeField] private GameObject playerMasterPrefab;

	private void Awake()
	{
		EnsurePlayerMasterExists();
	}

	public void EnsurePlayerMasterExists()
	{
		if (PlayerMaster.Instance != null)
		{
			return;
		}

		PlayerMaster existingInScene = FindFirstObjectByType<PlayerMaster>(FindObjectsInactive.Include);
		if (existingInScene != null)
		{
			return;
		}

		if (playerMasterPrefab != null)
		{
			GameObject pmInstance = Instantiate(playerMasterPrefab);
			pmInstance.name = "PlayerMaster";
		}
		else
		{
			Debug.LogError("[GameSceneManager] PlayerMaster Prefab is not assigned in the inspector!", this);
		}
	}

	// ==========================================
	// INSPECTOR BUTTON ONCLICK TARGETS
	// ==========================================

	public void LoadSceneByIndex(int sceneIndex)
	{
		Time.timeScale = 1f;

		// Clean up menus if MenuManager exists in the scene
		if (MenuManager.Instance != null)
		{
			MenuManager.Instance.LoadSceneByIndex(sceneIndex);
			return;
		}

		SceneManager.LoadScene(sceneIndex);
	}

	public void LoadSceneByName(string sceneName)
	{
		if (string.IsNullOrEmpty(sceneName))
		{
			Debug.LogWarning("[GameSceneManager] Scene name is empty!", this);
			return;
		}

		Time.timeScale = 1f;

		if (MenuManager.Instance != null)
		{
			MenuManager.Instance.LoadSceneByName(sceneName);
			return;
		}

		SceneManager.LoadScene(sceneName);
	}

	public void QuitGame()
	{
		Debug.Log("Quit Game requested.");
		Application.Quit();
	}
}