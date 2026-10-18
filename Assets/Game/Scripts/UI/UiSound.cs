using TowerDefense.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>A single click listener shared by generated menu and battle controls.</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class UiSound : MonoBehaviour
    {
        public SoundCue Cue = SoundCue.UIClick;
        public bool SuppressClick;
        private Button button;

        public static UiSound Attach(Button control)
        {
            if (control == null) return null;
            var sound = control.GetComponent<UiSound>();
            return sound != null ? sound : control.gameObject.AddComponent<UiSound>();
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            // Registered before the action, so a click can still be heard when it
            // changes scenes or hides the page containing this control.
            if (button != null) button.onClick.AddListener(PlayClick);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(PlayClick);
        }

        private void PlayClick()
        {
            if (!SuppressClick && isActiveAndEnabled && button != null && button.IsActive() && button.IsInteractable())
                GameAudio.Play(Cue);
        }
    }
}
