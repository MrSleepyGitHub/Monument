using UnityEngine;

public class KeybindUI : MonoBehaviour
{
	[Header("Script References")]
	public KeybindFramework framework;

	[Header("Prefab Instances")]
	public KeybindEntry walkForwardsEntry;
	public KeybindEntry walkBackwardsEntry;
	public KeybindEntry strafeLeftEntry;
	public KeybindEntry strafeRightEntry;
	public KeybindEntry sprintEntry;
	public KeybindEntry jumpEntry;
	public KeybindEntry crouchEntry;
	public KeybindEntry proneEntry;
	public KeybindEntry interactEntry;
	public KeybindEntry fireEntry;
	public KeybindEntry dropEntry;
	public KeybindEntry reloadEntry;

	private void Start()
	{
		// Initializes each prefab with a clean display name and the direct action reference
		walkForwardsEntry.Initialize("Walk Forwards", framework.walkForwards, framework);
		walkBackwardsEntry.Initialize("Walk Backwards", framework.walkBackwards, framework);
		strafeLeftEntry.Initialize("Strafe Left", framework.strafeLeft, framework);
		strafeRightEntry.Initialize("Strafe Right", framework.strafeRight, framework);

		sprintEntry.Initialize("Sprint", framework.sprint, framework);
		jumpEntry.Initialize("Jump", framework.jump, framework);
		crouchEntry.Initialize("Crouch", framework.crouch, framework);
		proneEntry.Initialize("Prone", framework.prone, framework);

		interactEntry.Initialize("Interact", framework.interact, framework);
		fireEntry.Initialize("Shoot", framework.fire, framework);
		dropEntry.Initialize("Drop", framework.drop, framework);
		reloadEntry.Initialize("Reload", framework.reload, framework);
	}
}