using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CreateWorkerIdleController
{
    public static void Run()
    {
        string idlePath = "Assets/_Project/Animations/Breathing Idle.fbx";
        string controllerPath = "Assets/_Project/Animations/WorkerIdle.controller";

        AnimationClip idleClip = AssetDatabase.LoadAllAssetsAtPath(idlePath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.Contains("__preview__"));
        if (idleClip == null)
        {
            Debug.LogError("CreateWorkerIdleController: idle clip not found.");
            EditorApplication.Exit(1);
            return;
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var rootStateMachine = controller.layers[0].stateMachine;
        var idleState = rootStateMachine.AddState("Idle");
        idleState.motion = idleClip;
        rootStateMachine.defaultState = idleState;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CreateWorkerIdleController: created " + controllerPath);
        EditorApplication.Exit(0);
    }
}
