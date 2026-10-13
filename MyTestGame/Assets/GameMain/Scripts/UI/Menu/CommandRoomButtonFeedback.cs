using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Small unscaled hover/focus animation shared by the command-room controls.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class CommandRoomButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    public Image highlight;
    [Range(1f, 1.05f)] public float hoverScale = 1.014f;
    private Button button;
    private bool hovered, focused, pressed;
    private float amount;
    private const float Response = 10f;

    private void Awake() { button = GetComponent<Button>(); }
    private void OnEnable() { ResetVisuals(); }
    private void OnDisable() { ResetVisuals(); }

    private void ResetVisuals()
    {
        hovered = focused = pressed = false;
        amount = 0f;
        transform.localScale = Vector3.one;
        SetHighlight(0f);
    }

    private void Update()
    {
        bool available = button != null && button.IsInteractable();
        float target = available && (hovered || focused) ? 1f : 0f;
        amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * Response);
        float size = available && pressed ? .988f : Mathf.Lerp(1f, hoverScale, amount);
        Vector3 scale = Vector3.one * size;
        if (transform.localScale != scale) transform.localScale = scale;
        SetHighlight(amount);
    }

    private void SetHighlight(float alpha)
    {
        if (highlight == null) return;
        Color color = highlight.color;
        if (Mathf.Approximately(color.a, alpha)) return;
        color.a = alpha;
        highlight.color = color;
    }

    public void OnPointerEnter(PointerEventData e) { hovered = true; }
    public void OnPointerExit(PointerEventData e) { hovered = pressed = false; }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    public void OnSelect(BaseEventData e) { focused = true; }
    public void OnDeselect(BaseEventData e) { focused = pressed = false; }
}
