using UnityEngine;

public class LocomotionState : BaseState
{
	public LocomotionState(StateManager context) : base(context) { }

	public override void EnterState() { }

	public override void ExitState()
	{
		// Removed IK weight resets - Unity forbids setting IK outside OnAnimatorIK
	}

	public override void UpdateState()
	{
		// Hands use standard updates for Animation Rigging
		UpdateHandIK();
	}

	public override void UpdateIK()
	{
		
	}

	public override void LateUpdateState()
	{
		// Keeps the head and spine tracking exactly what you look at while unarmed
		UpdateWeaponAim();
	}

	public override BaseState GetNextState()
	{
		if (ctx.player != null && ctx.player.currentWeapon != null)
		{
			return new RifleEquippedState(ctx);
		}

		if (ctx.player.inVehicle) return new SeatedState(ctx);

		return this;
	}
}