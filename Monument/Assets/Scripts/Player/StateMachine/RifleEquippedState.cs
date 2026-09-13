using UnityEngine;

public class RifleEquippedState : BaseState
{
	public RifleEquippedState(StateManager context) : base(context) { }

	public override void EnterState()
	{
		Debug.Log("Equipped Rifle. Initializing IK overrides.");
	}

	public override void ExitState()
	{
		Debug.Log("Dropped Rifle. Releasing IK overrides.");
	}

	public override void UpdateState()
	{
		UpdateWeaponAim();
		UpdateHandIK();
	}

	public override void LateUpdateState()
	{
		UpdateWeaponAim();
	}

	public override BaseState GetNextState()
	{
		if (ctx.player == null || ctx.player.currentWeapon == null)
		{
			return new LocomotionState(ctx);
		}

		if (ctx.player.inVehicle) return new SeatedState(ctx);

		return this;
	}

	public override void UpdateIK()
	{
		// Spine bending is already handled universally in StateManager.OnAnimatorIK
		// Hand IK is strictly handled by Animation Rigging in UpdateState

	}
}