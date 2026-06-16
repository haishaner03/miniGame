using GameFramework.Fsm;
using GameFramework.Procedure;
using UnityEngine;

namespace GameMain.Procedures
{
    public sealed class ProcedureBattle : ProcedureBase
    {
        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            Debug.Log("[UGF] Enter Battle procedure. This is where gameplay will be connected next.");
        }

        protected override void OnLeave(IFsm<IProcedureManager> procedureOwner, bool isShutdown)
        {
            Debug.Log("[UGF] Leave Battle procedure.");
            base.OnLeave(procedureOwner, isShutdown);
        }
    }
}
