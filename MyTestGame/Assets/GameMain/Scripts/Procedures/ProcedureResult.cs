using GameFramework.Fsm;
using GameFramework.Procedure;
using UnityEngine;

namespace GameMain.Procedures
{
    public sealed class ProcedureResult : ProcedureBase
    {
        private float m_ElapsedTime;

        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_ElapsedTime = 0f;
            Debug.Log("[UGF] Enter Result procedure.");
        }

        protected override void OnUpdate(IFsm<IProcedureManager> procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

            m_ElapsedTime += elapseSeconds;
            if (m_ElapsedTime >= 2f)
            {
                ChangeState<ProcedureMenu>(procedureOwner);
            }
        }

        protected override void OnLeave(IFsm<IProcedureManager> procedureOwner, bool isShutdown)
        {
            Debug.Log("[UGF] Leave Result procedure.");
            base.OnLeave(procedureOwner, isShutdown);
        }
    }
}
