using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class MenuManager : MonoBehaviour
{
	[Header("Menu List")]
	public GameObject baseMainMenu;
	public GameObject profileMenu;
	public GameObject optionsMenu;

	// Inside options menu
	public GameObject audioMenu;
	public GameObject videoMenu;
	public GameObject controlsMenu;
	public GameObject keybindMenu;

	[Header("Pause Settings")]
	public GameObject pauseMenu;
	public MonoBehaviour playerController;

	private bool isPaused = false;
	private int mainMenuSceneIndex = 0;

	private Stack<GameObject> menuStack = new Stack<GameObject>();

	private void Start()
	{
		if (SceneManager.GetActiveScene().buildIndex == mainMenuSceneIndex && baseMainMenu != null)
		{
			OpenMenu(baseMainMenu);
		}
	}

	private void Update()
	{
		if (Keyboard.current.escapeKey.wasPressedThisFrame)
		{
			HandleEscapeKey();
		}
	}

	private void HandleEscapeKey()
	{
		bool isGameplayScene = SceneManager.GetActiveScene().buildIndex != mainMenuSceneIndex;

		// If a menu is open, safely attempt to close it
		if (menuStack.Count > 0)
		{
			CloseCurrentMenu();
		}
		// If no menu is open and we are in gameplay, pause
		else if (isGameplayScene && !isPaused && pauseMenu != null)
		{
			PauseGame();
		}
	}

	public void OpenMenu(GameObject menuToOpen)
	{
		if (menuStack.Count > 0)
		{
			menuStack.Peek().SetActive(false);
		}

		menuToOpen.SetActive(true);
		menuStack.Push(menuToOpen);
	}

	public void CloseCurrentMenu()
	{
		// Prevent closing if the stack is already empty
		if (menuStack.Count == 0) return;

		bool isGameplayScene = SceneManager.GetActiveScene().buildIndex != mainMenuSceneIndex;

		// If we are down to the last menu in the stack...
		if (menuStack.Count == 1)
		{
			// Unpause if in gameplay, otherwise do nothing so the root UI doesn't vanish
			if (isGameplayScene) ResumeGame();
			return;
		}

		// Hide and remove the current menu
		GameObject topMenu = menuStack.Pop();
		topMenu.SetActive(false);

		// Reactivate the previous menu in the history
		if (menuStack.Count > 0)
		{
			menuStack.Peek().SetActive(true);
		}
	}

	// Forcefully resets the UI to the root menu for the current scene
	public void ReturnToRootMenu()
	{
		// Clear all history and hide all active menus
		while (menuStack.Count > 0)
		{
			menuStack.Pop().SetActive(false);
		}

		bool isGameplayScene = SceneManager.GetActiveScene().buildIndex != mainMenuSceneIndex;

		// Open the correct root menu based on context
		if (isGameplayScene && pauseMenu != null)
		{
			isPaused = true;
			if (playerController != null) playerController.enabled = false;
			Cursor.lockState = CursorLockMode.Confined;

			OpenMenu(pauseMenu);
		}
		else if (!isGameplayScene && baseMainMenu != null)
		{
			OpenMenu(baseMainMenu);
		}
	}

	public void PauseGame()
	{
		isPaused = true;
		if (playerController != null) playerController.enabled = false;
		Cursor.lockState = CursorLockMode.Confined;

		OpenMenu(pauseMenu);
	}

	public void ResumeGame()
	{
		isPaused = false;
		if (playerController != null) playerController.enabled = true;
		Cursor.lockState = CursorLockMode.Locked;

		while (menuStack.Count > 0)
		{
			menuStack.Pop().SetActive(false);
		}
	}

	// scene management
	public void PlayNextScene()
	{
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
	}

	public void QuitToMainMenu()
	{
		// 1. Reset any active pause states
		isPaused = false;
		if (playerController != null) playerController.enabled = true;

		// 2. Completely unlock and show the cursor so the player can interact with the Main Menu
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;

		// 3. Load the Main Menu scene (using the index already defined at the top of the script)
		SceneManager.LoadScene(mainMenuSceneIndex);
	}

	public void LoadSceneByIndex(int sceneIndex)
	{
		SceneManager.LoadScene(sceneIndex);
	}

	public void QuitGame()
	{
		Debug.Log("Quit!");
		Application.Quit();
	}
}