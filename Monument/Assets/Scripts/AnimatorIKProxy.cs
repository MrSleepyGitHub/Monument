using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimatorIKProxy : MonoBehaviour
{
    public AnimatorHumanoid rootScript;

    private void OnAnimatorIK(int layerIndex)
    {
        if (rootScript != null)
        {
            rootScript.ExecuteProceduralIK();
        }
    }
}