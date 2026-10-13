using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Pointer/keyboard sounds independent of onClick listener rebinding.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UiButtonSound : MonoBehaviour, IPointerDownHandler, ISubmitHandler
{
    private Button button;
    public static void Ensure(Button target)
    {
        if (target != null && !target.TryGetComponent<UiButtonSound>(out _))
            target.gameObject.AddComponent<UiButtonSound>();
    }
    private void Awake() { button = GetComponent<Button>(); }
    public void OnPointerDown(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left) Play();
    }
    // Button's submit handler may already have hidden this panel by the time we run.
    public void OnSubmit(BaseEventData data) { Play(false); }
    private void Play(bool requireActive = true)
    {
        if (button != null && (!requireActive || button.IsActive()) && button.IsInteractable())
            ZombieAudioController.Play(ZombieAudioCue.Button);
    }
}
