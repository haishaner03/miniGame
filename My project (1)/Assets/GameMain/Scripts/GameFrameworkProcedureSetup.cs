using System;
using System.Reflection;
using GameMain.Procedures;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameMain
{
    [DisallowMultipleComponent]
    public sealed class GameFrameworkProcedureSetup : MonoBehaviour
    {
        private static readonly string[] ProcedureTypeNames =
        {
            typeof(ProcedureLaunch).FullName,
            typeof(ProcedureMenu).FullName,
            typeof(ProcedureBattle).FullName,
        };

        private void Awake()
        {
            ProcedureComponent procedureComponent = GetComponent<ProcedureComponent>();
            if (procedureComponent == null)
            {
                Debug.LogError("[UGF] ProcedureComponent is missing.");
                return;
            }

            SetPrivateField(procedureComponent, "m_AvailableProcedureTypeNames", ProcedureTypeNames);
            SetPrivateField(procedureComponent, "m_EntranceProcedureTypeName", typeof(ProcedureLaunch).FullName);
            Debug.Log("[UGF] Procedure list configured: Launch -> Menu -> Battle.");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo fieldInfo = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            fieldInfo.SetValue(target, value);
        }
    }
}
