using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MenuOpenButton : MonoBehaviour
{
	[Tooltip("The canvas or panel GameObject this button should reveal.")]
	[SerializeField] private GameObject targetMenu;

	private void Awake()
	{
		GetComponent<Button>().onClick.AddListener(() =>
		{
			if (targetMenu != null && MenuManager.Instance != null)
			{
				MenuManager.Instance.OpenMenu(targetMenu);

			}
		});
    }
}