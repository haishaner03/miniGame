using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

namespace Flower
{
    public class UIMainMenuForm : UGuiFormEx
    {
        public Button levelSelectButton;
        public Button optionButton;
        public Button quitButton;

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            levelSelectButton.onClick.AddListener(OnLevelSelectButtonClick);
            levelSelectButton.gameObject.SetActive(false);
            optionButton.onClick.AddListener(OnOptionButtonClick);
            quitButton.onClick.AddListener(OnQuitButtonClick);
        }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);
        }

        private void OnLevelSelectButtonClick()
        {
            // Level selection is intentionally disabled while the project is being rebuilt as a 2D platformer.
        }

        private void OnOptionButtonClick()
        {
            GameEntry.Sound.PlaySound(EnumSound.ui_sound_forward);
            GameEntry.UI.OpenUIForm(EnumUIForm.UIOptionsForm);
        }

        private void OnQuitButtonClick()
        {
            UnityGameFramework.Runtime.GameEntry.Shutdown(ShutdownType.Quit);
        }

    }
}


