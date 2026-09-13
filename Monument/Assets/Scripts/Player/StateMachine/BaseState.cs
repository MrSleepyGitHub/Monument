using UnityEngine;

public abstract class BaseState
{
	protected StateManager ctx;

	public BaseState(StateManager context)
	{
		this.ctx = context;
	}

	public abstract void EnterState();
	public abstract void ExitState();
	public abstract void UpdateState();
	public abstract BaseState GetNextState();

	// Virtual allows us to optionally use LateUpdate in child states
	public virtual void LateUpdateState() { }

	public virtual void OnTriggerEnter(Collider other) { }
	public virtual void OnTriggerStay(Collider other) { }
	public virtual void OnTriggerExit(Collider other) { }

	public virtual void UpdateIK() { }

	protected void UpdateWeaponAim()
	{
		if (ctx.cameraHolder == null || ctx.playerAim == null) return;

		// 1. Determine the raw target distance
		float targetDistance = ctx.aimDistance;

		if (Physics.Raycast(ctx.cameraHolder.position, ctx.cameraHolder.forward, out RaycastHit hit, ctx.aimDistance, ctx.aimMask))
		{
			targetDistance = hit.distance;
		}

		// 2. Smoothly Lerp only the float distance to prevent horizontal lag
		ctx.currentAimDistance = Mathf.Lerp(ctx.currentAimDistance, targetDistance, Time.deltaTime * ctx.aimLerpSpeed);

		// 3. Project the PlayerAim object perfectly forward using the smoothed distance
		ctx.playerAim.position = ctx.cameraHolder.position + ctx.cameraHolder.forward * ctx.currentAimDistance;
	}

	// Shared Foot IK logic available to all child states
	

	// Shared Hand IK logic available to all child states
	protected void UpdateHandIK()
	{
		if (ctx.animator == null) return;

		// Blends the IK weight smoothly over a fraction of a second
		float blendSpeed = 10f * Time.deltaTime;

		// Route the Left Hand Rig
		if (ctx.leftHandGrip != null && ctx.leftArmIK != null)
		{
			ctx.leftArmIK.weight = Mathf.Lerp(ctx.leftArmIK.weight, 1f, blendSpeed);

			// Mathematically snap the constraint's target to the weapon's grip position
			ctx.leftArmIK.data.target.position = ctx.leftHandGrip.position;
			ctx.leftArmIK.data.target.rotation = ctx.leftHandGrip.rotation;
		}
		else if (ctx.leftArmIK != null)
		{
			// Smoothly release control back to the standard animation if no grip exists
			ctx.leftArmIK.weight = Mathf.Lerp(ctx.leftArmIK.weight, 0f, blendSpeed);
		}

		// Route the Right Hand Rig
		if (ctx.rightHandGrip != null && ctx.rightArmIK != null)
		{
			ctx.rightArmIK.weight = Mathf.Lerp(ctx.rightArmIK.weight, 1f, blendSpeed);

			ctx.rightArmIK.data.target.position = ctx.rightHandGrip.position;
			ctx.rightArmIK.data.target.rotation = ctx.rightHandGrip.rotation;
		}
		else if (ctx.rightArmIK != null)
		{
			ctx.rightArmIK.weight = Mathf.Lerp(ctx.rightArmIK.weight, 0f, blendSpeed);
		}
	}
}