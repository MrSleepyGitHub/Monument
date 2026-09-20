using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MenuCloseButton : MonoBehaviour
{
	private void Awake()
	{
		GetComponent<Button>().onClick.AddListener(() =>
		{
			if (MenuManager.Instance != null) MenuManager.Instance.CloseAllAndResume();
		});
	}
}