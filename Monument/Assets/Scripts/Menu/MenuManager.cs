///////////////////////////
// MENU MANAGER
///////////////////////////
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class MenuManager : MonoBehaviour
{
	public static MenuManager Instance { get; private set; }

	[Header("Root Menu References")]
	[Tooltip("The root UI panel for the Main Menu scene.")]
	public GameObject mainMenuRoot;
	[Tooltip("The root Pause Menu canvas/panel during gameplay.")]
	public GameObject pauseMenuRoot;

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

	private void Start()
	{
		if (!IsGameplayScene)
		{
			// Ensure cursor is free and time scale is normal when entering the Main Menu
			Time.timeScale = 1f;
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;

			if (mainMenuRoot != null)
			{
				OpenMenu(mainMenuRoot);
			}
		}
	}

	private void Update()
	{
		if (Keyboard.current.escapeKey.wasPressedThisFrame)
		{
			HandleEscapePress();
		}
	}

	///////////////////////////
	// ESCAPE NAVIGATION LOGIC
	///////////////////////////
	private void HandleEscapePress()
	{
		if (menuStack.Count > 0)
		{
			// In gameplay: if at the root pause menu, unpause and resume
			if (IsGameplayScene && menuStack.Count == 1)
			{
				CloseAllAndResume();
				return;
			}

			// In Main Menu: do NOT close the root menu on Escape
			if (!IsGameplayScene && menuStack.Count == 1)
			{
				return;
			}

			// Sub-menus (Options, Controls, Audio, etc.) pop back one step
			CloseCurrentMenu();
		}
		else if (IsGameplayScene && PlayerMaster.Instance != null && !PlayerMaster.Instance.IsPaused)
		{
			PlayerMaster.Instance.SetPauseState(true);
			if (pauseMenuRoot != null)
			{
				OpenMenu(pauseMenuRoot);
			}
		}
	}

	///////////////////////////
	// STACK OPERATIONS
	///////////////////////////
	public void OpenMenu(GameObject menuToOpen)
	{
		if (menuToOpen == null) return;

		if (menuStack.Count > 0)
		{
			menuStack.Peek().SetActive(false);
		}

		menuToOpen.SetActive(true);
		menuStack.Push(menuToOpen);
	}

	public void CloseCurrentMenu()
	{
		if (menuStack.Count == 0) return;

		// Never pop or disable the root main menu
		if (!IsGameplayScene && menuStack.Count <= 1) return;

		GameObject active = menuStack.Pop();
		active.SetActive(false);

		if (menuStack.Count > 0)
		{
			menuStack.Peek().SetActive(true);
		}
	}

	/// <summary>
	/// Back Button logic: Clears sub-menus and returns strictly to the scene's highest root menu.
	/// </summary>
	public void ReturnToRootMenu()
	{
		while (menuStack.Count > 0)
		{
			menuStack.Pop().SetActive(false);
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

	/// <summary>
	/// Close Button logic: Closes everything and unpauses during gameplay; returns to root in main menu.
	/// </summary>
	public void CloseAllAndResume()
	{
		while (menuStack.Count > 0)
		{
			menuStack.Pop().SetActive(false);
		}

		if (IsGameplayScene)
		{
			if (PlayerMaster.Instance != null)
			{
				PlayerMaster.Instance.SetPauseState(false);
			}
		}
		else if (mainMenuRoot != null)
		{
			OpenMenu(mainMenuRoot);
		}
	}

	///////////////////////////
	// SCENE TRANSITIONS
	///////////////////////////
	public void QuitToMainMenu()
	{
		Time.timeScale = 1f;

		// Unlock and display the cursor before transitioning scenes
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;

		if (PlayerMaster.Instance != null)
		{
			// Clear pause flag and re-enable motor logic without locking cursor
			PlayerMaster.Instance.ResetForMenuTransition();
		}

		SceneManager.LoadScene(mainMenuSceneIndex);
	}

	public void QuitGame()
	{
		Debug.Log("Quit!");
		Application.Quit();
	}
}