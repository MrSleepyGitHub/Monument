using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PlayerMasterMenuButton : MonoBehaviour
{
	public enum MenuTarget
	{
		Options
	}

	[SerializeField] private MenuTarget targetMenu = MenuTarget.Options;

	private void Awake()
	{
		GetComponent<Button>().onClick.AddListener(OnButtonClicked);
	}

	private void OnButtonClicked()
	{
		if (PlayerMaster.Instance == null)
		{
			Debug.LogWarning($"[PlayerMasterMenuButton] PlayerMaster.Instance not found when clicking '{gameObject.name}'!", this);
			return;
		}

		switch (targetMenu)
		{
			case MenuTarget.Options:
				PlayerMaster.Instance.OpenOptionsMenu();
				break;
		}
	}
}