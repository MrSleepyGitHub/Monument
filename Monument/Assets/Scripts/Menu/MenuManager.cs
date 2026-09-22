///////////////////////////
// MENU MANAGER
///////////////////////////
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class MenuManager : MonoBehaviour
{
	public static MenuManager Instance { get; private set; }

	[Header("Root Menu References")]
	[Tooltip("The root UI panel for the Main Menu scene.")]
	public GameObject mainMenuRoot;
	[Tooltip("The root Pause Menu canvas/panel during gameplay.")]
	public GameObject pauseMenuRoot;

	[Header("HUD Reference")]
	[Tooltip("The root GameObject containing the PlayerHUD. Automatically located if left unassigned.")]
	public GameObject playerHudRoot;

	[Header("Scene Configuration")]
	[SerializeField] private int mainMenuSceneIndex = 0;

	private readonly Stack<GameObject> menuStack = new Stack<GameObject>();
	public bool IsGameplayScene => SceneManager.GetActiveScene().buildIndex != mainMenuSceneIndex; 

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
	}

	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void Start()
	{
		InitializeSceneUI();
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		InitializeSceneUI();
	}

	private void InitializeSceneUI()
	{
		menuStack.Clear();

		if (!IsGameplayScene)
		{
			// --- MAIN MENU (SCENE 0) ---
			Time.timeScale = 1f;
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;

			if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);
			SetHudActive(false);

			if (mainMenuRoot == null)
			{
				mainMenuRoot = GameObject.Find("MainMenuRoot") ?? GameObject.Find("MainMenu");
			}

			if (mainMenuRoot != null)
			{
				OpenMenu(mainMenuRoot);
			}
		}
		else
		{
			// --- GAMEPLAY SCENES ---
			Time.timeScale = 1f;
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;

			if (mainMenuRoot != null) mainMenuRoot.SetActive(false);
			if (pauseMenuRoot != null) pauseMenuRoot.SetActive(false);

			if (pauseMenuRoot == null)
			{
				pauseMenuRoot = GameObject.Find("PauseMenuRoot") ?? GameObject.Find("PauseMenu");
			}

			SetHudActive(true);

			if (PlayerMaster.Instance != null)
			{
				PlayerMaster.Instance.SetPauseState(false);
			}
		}
	}

	private void SetHudActive(bool active)
	{
		if (playerHudRoot == null)
		{
			PlayerHUD hud = FindFirstObjectByType<PlayerHUD>(FindObjectsInactive.Include); 
			if (hud != null)
			{
				playerHudRoot = hud.gameObject;
			}
		}

		if (playerHudRoot != null)
		{
			playerHudRoot.SetActive(active);
		}
	}

	private void Update()
	{
		if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
		{
			HandleEscapePress(); 
		}
	}

	///////////////////////////
	// ESCAPE NAVIGATION LOGIC
	///////////////////////////
	private void HandleEscapePress()
	{
		PruneDeadStackEntries();

		if (IsGameplayScene) 
		{
			if (menuStack.Count == 0)
			{
				if (PlayerMaster.Instance != null)
				{
					PlayerMaster.Instance.SetPauseState(true);
				}
				else
				{
					Time.timeScale = 0f;
					Cursor.lockState = CursorLockMode.None;
					Cursor.visible = true;
				}

				if (pauseMenuRoot != null)
				{
					OpenMenu(pauseMenuRoot); 
				}
			}
			else if (menuStack.Count == 1)
			{
				CloseAllAndResume(); 
			}
			else
			{
				CloseCurrentMenu(); 
			}
		}
		else
		{
			if (menuStack.Count > 1)
			{
				CloseCurrentMenu(); 
			}
		}
	}

	///////////////////////////
	// STACK OPERATIONS
	///////////////////////////
	public void OpenMenu(GameObject menuToOpen)
	{
		if (menuToOpen == null) return;

		PruneDeadStackEntries();

		if (menuStack.Count > 0)
		{
			GameObject top = menuStack.Peek();
			if (top != null) top.SetActive(false);
		}

	menuToOpen.SetActive(true);
	menuStack.Push(menuToOpen);
		}

		public void CloseCurrentMenu()
	{
		PruneDeadStackEntries();

		if (menuStack.Count == 0) return;
		if (!IsGameplayScene && menuStack.Count <= 1) return; 

			GameObject active = menuStack.Pop();
		if (active != null)
		{
			active.SetActive(false);
		}

		PruneDeadStackEntries();

		if (menuStack.Count > 0)
		{
			GameObject previous = menuStack.Peek();
			if (previous != null) previous.SetActive(true);
		}
	}

	public void ReturnToRootMenu()
	{
		while (menuStack.Count > 0)
		{
			GameObject menu = menuStack.Pop();
			if (menu != null) menu.SetActive(false);
		}

		if (IsGameplayScene && pauseMenuRoot != null) 
			{
			OpenMenu(pauseMenuRoot); 
			}
			else if (!IsGameplayScene && mainMenuRoot != null) 
			{
			OpenMenu(mainMenuRoot); 
			}
	}

	public void CloseAllAndResume()
	{
		while (menuStack.Count > 0)
		{
			GameObject menu = menuStack.Pop();
			if (menu != null) menu.SetActive(false);
		}

		if (IsGameplayScene) 
			{
			if (PlayerMaster.Instance != null)
			{
				PlayerMaster.Instance.SetPauseState(false);
			}
			else
			{
				Time.timeScale = 1f;
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
		}
			else if (mainMenuRoot != null)
		{
			OpenMenu(mainMenuRoot); 
			}
	}

	private void PruneDeadStackEntries()
	{
		while (menuStack.Count > 0 && menuStack.Peek() == null)
		{
			menuStack.Pop();
		}
	}

	///////////////////////////
	// SCENE TRANSITIONS
	///////////////////////////
	public void LoadSceneByIndex(int sceneIndex)
	{
		if (sceneIndex == mainMenuSceneIndex)
		{
			QuitToMainMenu(); 
				return;
		}

		PrepareForSceneTransition();
		SceneManager.LoadScene(sceneIndex);
	}

	public void LoadSceneByName(string sceneName)
	{
		if (string.IsNullOrEmpty(sceneName))
		{
			Debug.LogWarning("[MenuManager] Attempted to load a scene with an empty name!");
			return;
		}

		PrepareForSceneTransition();
		SceneManager.LoadScene(sceneName);
	}

	public void QuitToMainMenu()
	{
		PrepareForSceneTransition();

		if (PlayerMaster.Instance != null)
		{
			PlayerMaster.Instance.ResetForMenuTransition();
		}

		SceneManager.LoadScene(mainMenuSceneIndex);
		}

	private void PrepareForSceneTransition()
	{
		Time.timeScale = 1f;

		if (pauseMenuRoot != null)
		{
			pauseMenuRoot.SetActive(false);
		}

		if (playerHudRoot != null)
		{
			playerHudRoot.SetActive(false);
		}

		while (menuStack.Count > 0)
		{
			GameObject menu = menuStack.Pop();
			if (menu != null) menu.SetActive(false);
		}
	}

	public void QuitGame()
	{
		Debug.Log("Quit!");
		Application.Quit();
	}
}