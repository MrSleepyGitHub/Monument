using UnityEngine;

public interface IInteractable
{
	// Passing the GameObject allows the weapon to find the player's weapon holder slot
	void Interact(GameObject interactor);
}