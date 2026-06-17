using GameFramework.Fsm;
using GameFramework.Event;
using GameFramework.Procedure;
using GameMain.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityGameFramework.Runtime;

namespace GameMain.Procedures
{
    public sealed class ProcedureMenu : ProcedureBase
    {
        private const string MenuPrefabPath = "Assets/GameMain/Prefabs/UI/MainMenu.prefab";
        private const string MenuUIGroupName = "Default";

        private EventComponent m_EventComponent;
        private UIComponent m_UIComponent;
        private MainMenuView m_MenuView;
        private IFsm<IProcedureManager> m_ProcedureOwner;
        private int m_MenuSerialId;

        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            Debug.Log("[UGF] Enter Menu procedure.");
            m_ProcedureOwner = procedureOwner;
            OpenMenu();
        }

        protected override void OnUpdate(IFsm<IProcedureManager> procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
        }

        protected override void OnLeave(IFsm<IProcedureManager> procedureOwner, bool isShutdown)
        {
            CloseMenu();

            m_ProcedureOwner = null;
            Debug.Log("[UGF] Leave Menu procedure.");
            base.OnLeave(procedureOwner, isShutdown);
        }

        private void OpenMenu()
        {
            EnsureEventSystem();

            m_EventComponent = GameEntry.GetComponent<EventComponent>();
            m_UIComponent = GameEntry.GetComponent<UIComponent>();

            if (m_EventComponent == null || m_UIComponent == null)
            {
                Debug.LogError("[UGF] EventComponent or UIComponent is missing on GameEntry.");
                return;
            }

            if (!m_UIComponent.HasUIGroup(MenuUIGroupName))
            {
                m_UIComponent.AddUIGroup(MenuUIGroupName, 0);
            }

            m_EventComponent.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnOpenUIFormSuccess);
            m_EventComponent.Subscribe(OpenUIFormFailureEventArgs.EventId, OnOpenUIFormFailure);
            m_MenuSerialId = m_UIComponent.OpenUIForm(MenuPrefabPath, MenuUIGroupName, this);
            Debug.Log("[UGF] Open menu UI form, serial id: " + m_MenuSerialId + ".");
        }

        private void CloseMenu()
        {
            if (m_MenuView != null)
            {
                m_MenuView.StartClicked -= OnStartClicked;
                m_MenuView = null;
            }

            if (m_EventComponent != null)
            {
                m_EventComponent.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnOpenUIFormSuccess);
                m_EventComponent.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnOpenUIFormFailure);
            }

            if (m_UIComponent != null && m_MenuSerialId > 0)
            {
                m_UIComponent.CloseUIForm(m_MenuSerialId);
            }

            m_MenuSerialId = 0;
            m_EventComponent = null;
            m_UIComponent = null;
        }

        private void OnOpenUIFormSuccess(object sender, GameEventArgs e)
        {
            OpenUIFormSuccessEventArgs ne = (OpenUIFormSuccessEventArgs)e;
            if (ne.UserData != this || ne.UIForm.SerialId != m_MenuSerialId)
            {
                return;
            }

            m_MenuView = ne.UIForm.Logic as MainMenuView;
            if (m_MenuView == null)
            {
                Debug.LogError("[UGF] MainMenuView is missing on menu UI form.");
                return;
            }

            m_MenuView.StartClicked += OnStartClicked;
            Debug.Log("[UGF] Menu UI form opened.");
        }

        private void OnOpenUIFormFailure(object sender, GameEventArgs e)
        {
            OpenUIFormFailureEventArgs ne = (OpenUIFormFailureEventArgs)e;
            if (ne.UserData != this || ne.SerialId != m_MenuSerialId)
            {
                return;
            }

            Debug.LogError("[UGF] Open menu UI form failed: " + ne.ErrorMessage);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private void OnStartClicked()
        {
            if (m_ProcedureOwner == null)
            {
                return;
            }

            Debug.Log("[UGF] Start button clicked.");
            ChangeState<ProcedureBattle>(m_ProcedureOwner);
        }
    }
}
