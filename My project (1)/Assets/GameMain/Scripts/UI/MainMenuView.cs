using System;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace GameMain.UI
{
    public sealed class MainMenuView : UIFormLogic
    {
        [SerializeField]
        private Button m_StartButton;

        public event Action StartClicked;

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            if (m_StartButton == null)
            {
                m_StartButton = GetComponentInChildren<Button>(true);
            }
        }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            if (m_StartButton != null)
            {
                m_StartButton.onClick.AddListener(OnStartButtonClicked);
            }
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            if (m_StartButton != null)
            {
                m_StartButton.onClick.RemoveListener(OnStartButtonClicked);
            }

            StartClicked = null;
            base.OnClose(isShutdown, userData);
        }

        private void OnStartButtonClicked()
        {
            StartClicked?.Invoke();
        }
    }
}
