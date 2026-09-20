using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MenuBackButton : MonoBehaviour
{
	private void Awake()
	{
		GetComponent<Button>().onClick.AddListener(() =>
		{
			if (MenuManager.Instance != null) MenuManager.Instance.ReturnToRootMenu();
		});
	}
}