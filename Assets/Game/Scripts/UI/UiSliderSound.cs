using TowerDefense.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>Audible volume preview on release, without sounding on every drag update.</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class UiSliderSound : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Slider slider;
        private bool pointerHeld;
        private bool changedDuringGesture;
        private int lastChangeFrame = -1;

        private void Awake()
        {
            slider = GetComponent<Slider>();
            if (slider != null) slider.onValueChanged.AddListener(RecordChange);
        }

        private void OnDisable()
        {
            pointerHeld = changedDuringGesture = false;
            lastChangeFrame = -1;
        }

        private void OnDestroy()
        {
            if (slider != null) slider.onValueChanged.RemoveListener(RecordChange);
        }

        private void RecordChange(float value)
        {
            lastChangeFrame = Time.frameCount;
            if (pointerHeld) changedDuringGesture = true;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || slider == null || !slider.IsInteractable()) return;
            // Slider's own pointer handler may run first and already move the
            // value when the track is clicked. Count that same-frame change.
            changedDuringGesture = lastChangeFrame == Time.frameCount;
            pointerHeld = true;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !pointerHeld) return;
            pointerHeld = false;
            if (isActiveAndEnabled && slider != null && slider.IsActive() && slider.IsInteractable() &&
                changedDuringGesture)
                GameAudio.Play(SoundCue.UIConfirm);
            changedDuringGesture = false;
        }
    }
}
