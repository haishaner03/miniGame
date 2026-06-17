using GameFramework.Event;
using GameFramework.Fsm;
using GameFramework.Procedure;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameFramework.Runtime;

namespace GameMain.Procedures
{
    public sealed class ProcedureBattle : ProcedureBase
    {
        private const string BattleSceneAssetName = "Assets/GameMain/Scenes/Battle.unity";

        private EventComponent m_EventComponent;
        private SceneComponent m_SceneComponent;
        private bool m_IsSceneLoaded;

        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_IsSceneLoaded = false;
            m_EventComponent = GameEntry.GetComponent<EventComponent>();
            m_SceneComponent = GameEntry.GetComponent<SceneComponent>();

            if (m_EventComponent == null || m_SceneComponent == null)
            {
                Debug.LogError("[UGF] EventComponent or SceneComponent is missing on GameEntry.");
                return;
            }

            m_EventComponent.Subscribe(LoadSceneSuccessEventArgs.EventId, OnLoadSceneSuccess);
            m_EventComponent.Subscribe(LoadSceneFailureEventArgs.EventId, OnLoadSceneFailure);

            if (m_SceneComponent.SceneIsLoaded(BattleSceneAssetName))
            {
                m_IsSceneLoaded = true;
                Debug.Log("[UGF] Battle scene already loaded.");
                return;
            }

            Debug.Log("[UGF] Enter Battle procedure. Loading battle scene.");
            m_SceneComponent.LoadScene(BattleSceneAssetName, this);
        }

        protected override void OnUpdate(IFsm<IProcedureManager> procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

            if (!m_IsSceneLoaded)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                Debug.Log("[UGF] Battle finished by keyboard shortcut.");
                ChangeState<ProcedureResult>(procedureOwner);
            }
        }

        protected override void OnLeave(IFsm<IProcedureManager> procedureOwner, bool isShutdown)
        {
            if (m_EventComponent != null)
            {
                m_EventComponent.Unsubscribe(LoadSceneSuccessEventArgs.EventId, OnLoadSceneSuccess);
                m_EventComponent.Unsubscribe(LoadSceneFailureEventArgs.EventId, OnLoadSceneFailure);
            }

            if (m_SceneComponent != null && m_SceneComponent.SceneIsLoaded(BattleSceneAssetName))
            {
                m_SceneComponent.UnloadScene(BattleSceneAssetName, this);
            }

            m_EventComponent = null;
            m_SceneComponent = null;
            m_IsSceneLoaded = false;
            Debug.Log("[UGF] Leave Battle procedure.");
            base.OnLeave(procedureOwner, isShutdown);
        }

        private void OnLoadSceneSuccess(object sender, GameEventArgs e)
        {
            LoadSceneSuccessEventArgs ne = (LoadSceneSuccessEventArgs)e;
            if (ne.UserData != this || ne.SceneAssetName != BattleSceneAssetName)
            {
                return;
            }

            m_IsSceneLoaded = true;
            Debug.Log("[UGF] Battle scene loaded.");
        }

        private void OnLoadSceneFailure(object sender, GameEventArgs e)
        {
            LoadSceneFailureEventArgs ne = (LoadSceneFailureEventArgs)e;
            if (ne.UserData != this || ne.SceneAssetName != BattleSceneAssetName)
            {
                return;
            }

            Debug.LogError("[UGF] Load battle scene failed: " + ne.ErrorMessage);
        }
    }
}
