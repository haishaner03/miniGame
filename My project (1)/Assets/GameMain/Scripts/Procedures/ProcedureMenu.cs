using GameFramework.Fsm;
using GameFramework.Procedure;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameMain.Procedures
{
    public sealed class ProcedureMenu : ProcedureBase
    {
        private const string MenuRootName = "MainMenu";

        private GameObject m_MenuRoot;

        protected override void OnEnter(IFsm<IProcedureManager> procedureOwner)
        {
            base.OnEnter(procedureOwner);

            Debug.Log("[UGF] Enter Menu procedure.");
            CreateMenu(procedureOwner);
        }

        protected override void OnUpdate(IFsm<IProcedureManager> procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
        }

        protected override void OnLeave(IFsm<IProcedureManager> procedureOwner, bool isShutdown)
        {
            if (m_MenuRoot != null)
            {
                Object.Destroy(m_MenuRoot);
                m_MenuRoot = null;
            }

            Debug.Log("[UGF] Leave Menu procedure.");
            base.OnLeave(procedureOwner, isShutdown);
        }

        private void CreateMenu(IFsm<IProcedureManager> procedureOwner)
        {
            EnsureEventSystem();

            m_MenuRoot = new GameObject(MenuRootName);
            Canvas canvas = m_MenuRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_MenuRoot.AddComponent<CanvasScaler>();
            m_MenuRoot.AddComponent<GraphicRaycaster>();

            GameObject panel = CreateRect("Panel", m_MenuRoot.transform, Vector2.zero, new Vector2(420f, 260f));
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.88f);

            Text title = CreateText("Title", panel.transform, "Mini Fighter", 36, new Vector2(0f, 64f), new Vector2(360f, 60f));
            title.alignment = TextAnchor.MiddleCenter;

            Button startButton = CreateButton(panel.transform, "Start Game", new Vector2(0f, -28f), new Vector2(220f, 54f));
            startButton.onClick.AddListener(() =>
            {
                Debug.Log("[UGF] Start button clicked.");
                ChangeState<ProcedureBattle>(procedureOwner);
            });
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

        private static GameObject CreateRect(string name, Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);

            RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;

            return gameObject;
        }

        private static Text CreateText(string name, Transform parent, string content, int fontSize, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject gameObject = CreateRect(name, parent, anchoredPosition, sizeDelta);
            Text text = gameObject.AddComponent<Text>();
            text.text = content;
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.color = Color.white;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject buttonObject = CreateRect("StartButton", parent, anchoredPosition, sizeDelta);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.48f, 0.86f, 1f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText("Label", buttonObject.transform, label, 22, Vector2.zero, sizeDelta);
            text.alignment = TextAnchor.MiddleCenter;

            return button;
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                return font;
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
