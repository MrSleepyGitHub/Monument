using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class BlendTreeCopyPaste : EditorWindow
{
    private static BlendTree sourceBlendTree;

    [MenuItem("Assets/AnimTools/Copy Blend Tree", false, 1)]
    private static void CopyBlendTree()
    {
        sourceBlendTree = Selection.activeObject as BlendTree;
        Debug.Log($"[BlendTreeCopyPaste] Copied Blend Tree: {sourceBlendTree.name}");
    }

    [MenuItem("Assets/AnimTools/Copy Blend Tree", true)]
    private static bool ValidateCopyBlendTree()
    {
        return Selection.activeObject is BlendTree;
    }

    [MenuItem("Assets/AnimTools/Paste Blend Tree", false, 2)]
    private static void PasteBlendTree()
    {
        BlendTree targetBlendTree = Selection.activeObject as BlendTree;

        if (sourceBlendTree == null || targetBlendTree == null || sourceBlendTree == targetBlendTree)
        {
            Debug.LogError("[BlendTreeCopyPaste] Invalid paste operation. Ensure you have copied a tree and selected a different target tree.");
            return;
        }

        // Register action for Unity's Undo history
        Undo.RecordObject(targetBlendTree, "Paste Blend Tree Configuration");

        // Copy top level configuration
        targetBlendTree.blendType = sourceBlendTree.blendType;
        targetBlendTree.blendParameter = sourceBlendTree.blendParameter;
        targetBlendTree.blendParameterY = sourceBlendTree.blendParameterY;
        targetBlendTree.useAutomaticThresholds = sourceBlendTree.useAutomaticThresholds;
        targetBlendTree.minThreshold = sourceBlendTree.minThreshold;
        targetBlendTree.maxThreshold = sourceBlendTree.maxThreshold;

        // Clear existing children in the destination tree
        while (targetBlendTree.children.Length > 0)
        {
            targetBlendTree.RemoveChild(0);
        }

        // Replicate children into target tree
        ChildMotion[] sourceChildren = sourceBlendTree.children;
        foreach (var child in sourceChildren)
        {
            if (child.motion is BlendTree childSubTree)
            {
                // Create a sub-tree and recursively handle its copy logic if required
                BlendTree newSubTree = targetBlendTree.CreateBlendTreeChild(child.position);
                CopyTreeProperties(childSubTree, newSubTree);
            }
            else
            {
                // Assign basic animation clips or assets
                targetBlendTree.AddChild(child.motion, child.position);

                // Mirror custom thresholds/speeds/mirrors 
                int addedIndex = targetBlendTree.children.Length - 1;
                ChildMotion[] targetChildren = targetBlendTree.children;
                targetChildren[addedIndex].threshold = child.threshold;
                targetChildren[addedIndex].timeScale = child.timeScale;
                targetChildren[addedIndex].mirror = child.mirror;
                targetBlendTree.children = targetChildren;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[BlendTreeCopyPaste] Successfully pasted setup into: {targetBlendTree.name}");
    }

    [MenuItem("Assets/AnimTools/Paste Blend Tree", true)]
    private static bool ValidatePasteBlendTree()
    {
        return Selection.activeObject is BlendTree && sourceBlendTree != null;
    }

    private static void CopyTreeProperties(BlendTree source, BlendTree target)
    {
        target.blendType = source.blendType;
        target.blendParameter = source.blendParameter;
        target.blendParameterY = source.blendParameterY;
        target.useAutomaticThresholds = source.useAutomaticThresholds;

        foreach (var child in source.children)
        {
            if (child.motion is BlendTree deepSubTree)
            {
                BlendTree deepTargetSubTree = target.CreateBlendTreeChild(child.position);
                CopyTreeProperties(deepSubTree, deepTargetSubTree);
            }
            else
            {
                target.AddChild(child.motion, child.position);
            }
        }
    }
}
