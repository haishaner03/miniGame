using UnityEngine;
using UnityGameFramework.Runtime;

namespace GameMain
{
    [DisallowMultipleComponent]
    public sealed class GameFrameworkProcedureSetup : MonoBehaviour
    {
        private void Awake()
        {
            ProcedureComponent procedureComponent = GetComponent<ProcedureComponent>();
            if (procedureComponent == null)
            {
                Debug.LogError("[UGF] ProcedureComponent is missing.");
                return;
            }

            Debug.Log("[UGF] ProcedureComponent is ready. Expected flow: Launch -> Menu -> Battle.");
        }
    }
}
