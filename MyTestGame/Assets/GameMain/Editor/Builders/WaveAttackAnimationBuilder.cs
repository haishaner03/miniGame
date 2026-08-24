using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GameMain.Editor
{
    [InitializeOnLoad]
    public static class WaveAttackAnimationBuilder
    {
        private const string ControllerPath = "Assets/GameMain/LearningDemos/WaveOfTheFist/Animation/Player.controller";
        private const string AttackClipPath = "Assets/GameMain/LearningDemos/WaveOfTheFist/Animation/Unarmed_Attack.anim";
        private const string CombatSheetPath = "Assets/GameMain/LearningDemos/WaveOfTheFist/Sprites/PlayerCombatSheet.png";
        private const string AttackStateName = "Unarmed_Attack";
        private const string AttackParameterName = "Attack";

        static WaveAttackAnimationBuilder()
        {
            EditorApplication.delayCall += BuildIfNeeded;
        }

        [MenuItem("GameMain/Build Animations/Wave Attack")]
        public static void BuildIfNeeded()
        {
            EditorApplication.delayCall -= BuildIfNeeded;

            AnimationClip attackClip = EnsureAttackClip();
            if (attackClip == null)
            {
                return;
            }

            EnsureAnimatorState(attackClip);
        }

        private static AnimationClip EnsureAttackClip()
        {
            AnimationClip existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClipPath);
            if (existingClip != null)
            {
                return existingClip;
            }

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(CombatSheetPath)
                .OfType<Sprite>()
                .OrderBy(sprite => GetSpriteIndex(sprite.name))
                .Take(8)
                .ToArray();

            if (sprites.Length == 0)
            {
                Debug.LogError("[UGF] Cannot build attack animation. Combat sprites are missing: " + CombatSheetPath);
                return null;
            }

            AnimationClip clip = new AnimationClip
            {
                name = AttackStateName,
                frameRate = 24f
            };

            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / clip.frameRate,
                    value = sprites[i]
                };
            }

            EditorCurveBinding spriteBinding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite"
            };

            AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);
            SetLoopTime(clip, false);
            AssetDatabase.CreateAsset(clip, AttackClipPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[UGF] Built attack animation clip: " + AttackClipPath);
            return clip;
        }

        private static void EnsureAnimatorState(AnimationClip attackClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("[UGF] Player animator controller is missing: " + ControllerPath);
                return;
            }

            EnsureTriggerParameter(controller);

            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
            AnimatorState attackState = FindState(rootStateMachine, AttackStateName);
            if (attackState == null)
            {
                attackState = rootStateMachine.AddState(AttackStateName, new Vector3(430f, 260f, 0f));
            }

            attackState.motion = attackClip;
            attackState.writeDefaultValues = true;

            if (!HasAnyStateTransition(rootStateMachine, attackState))
            {
                AnimatorStateTransition toAttack = rootStateMachine.AddAnyStateTransition(attackState);
                toAttack.hasExitTime = false;
                toAttack.duration = 0.02f;
                toAttack.canTransitionToSelf = false;
                toAttack.AddCondition(AnimatorConditionMode.If, 0f, AttackParameterName);
            }

            AnimatorState idleState = FindState(rootStateMachine, "Unarmed_Idle");
            if (idleState != null && !HasTransition(attackState, idleState))
            {
                AnimatorStateTransition toIdle = attackState.AddTransition(idleState);
                toIdle.hasExitTime = true;
                toIdle.exitTime = 0.95f;
                toIdle.duration = 0.05f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureTriggerParameter(AnimatorController controller)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == AttackParameterName)
                {
                    return;
                }
            }

            controller.AddParameter(AttackParameterName, AnimatorControllerParameterType.Trigger);
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state.name == stateName)
                {
                    return childState.state;
                }
            }

            foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
            {
                AnimatorState state = FindState(childStateMachine.stateMachine, stateName);
                if (state != null)
                {
                    return state;
                }
            }

            return null;
        }

        private static bool HasAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState targetState)
        {
            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            {
                if (transition.destinationState == targetState)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTransition(AnimatorState fromState, AnimatorState toState)
        {
            foreach (AnimatorStateTransition transition in fromState.transitions)
            {
                if (transition.destinationState == toState)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetSpriteIndex(string spriteName)
        {
            int separatorIndex = spriteName.LastIndexOf('_');
            if (separatorIndex < 0 || separatorIndex == spriteName.Length - 1)
            {
                return int.MaxValue;
            }

            return int.TryParse(spriteName.Substring(separatorIndex + 1), out int index) ? index : int.MaxValue;
        }

        private static void SetLoopTime(AnimationClip clip, bool loopTime)
        {
            SerializedObject serializedClip = new SerializedObject(clip);
            SerializedProperty settings = serializedClip.FindProperty("m_AnimationClipSettings");
            settings.FindPropertyRelative("m_LoopTime").boolValue = loopTime;
            serializedClip.ApplyModifiedProperties();
        }
    }
}
