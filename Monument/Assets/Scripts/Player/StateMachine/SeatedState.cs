using UnityEngine;

public class SeatedState : BaseState
{
	public SeatedState(StateManager context) : base(context) { }

	public override void EnterState()
	{
		// Intentionally empty. Unity forbids setting IK outside OnAnimatorIK.
	}

	public override void ExitState() { }

	public override void UpdateState() { }

	public override void LateUpdateState() { }

	public override BaseState GetNextState()
	{
		if (!ctx.player.inVehicle)
		{
			if (ctx.player.currentWeapon != null) return new RifleEquippedState(ctx);
			return new LocomotionState(ctx);
		}

		return this;
	}

	public override void UpdateIK()
	{
		// Continuously zeroes out lingering foot IK weights from Locomotion while seated
		ctx.animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
		ctx.animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
		ctx.animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
		ctx.animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
	}
}