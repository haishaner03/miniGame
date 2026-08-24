using System;
using System.Reflection;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameMain
{
    [DisallowMultipleComponent]
    public sealed class GameFrameworkProcedureSetup : MonoBehaviour
    {
        private void Awake()
        {
            EnsureFrameworkComponent<EventComponent>();
            EnsureFrameworkComponent<ObjectPoolComponent>();
            EnsureFrameworkComponent<EditorResourceComponent>();
            EnsureFrameworkComponent<ResourceComponent>();
            EnsureFrameworkComponent<SceneComponent>();
            UIComponent uiComponent = EnsureFrameworkComponent<UIComponent>();
            EnsureEmptyUIGroups(uiComponent);

            ProcedureComponent procedureComponent = GetComponent<ProcedureComponent>();
            if (procedureComponent == null)
            {
                Debug.LogError("[UGF] ProcedureComponent is missing.");
                return;
            }

            Debug.Log("[UGF] ProcedureComponent is ready. Expected flow: Launch -> Menu -> Battle.");
        }

        private T EnsureFrameworkComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return gameObject.AddComponent<T>();
        }

        private static void EnsureEmptyUIGroups(UIComponent uiComponent)
        {
            FieldInfo uiGroupsField = typeof(UIComponent).GetField("m_UIGroups", BindingFlags.Instance | BindingFlags.NonPublic);
            if (uiGroupsField == null || uiGroupsField.GetValue(uiComponent) != null)
            {
                return;
            }

            Type uiGroupArrayType = uiGroupsField.FieldType;
            Array emptyGroups = Array.CreateInstance(uiGroupArrayType.GetElementType(), 0);
            uiGroupsField.SetValue(uiComponent, emptyGroups);
        }
    }
}
