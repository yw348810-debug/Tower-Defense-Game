using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>Small, unscaled motion and border emphasis. It never owns clicks or navigation.</summary>
    [AddComponentMenu("")]
    public sealed class UiFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float HoverScale = 1.015f;
        private Button button;
        private UiSurface surface;
        private Vector3 originalScale;
        private bool hovered;
        private bool pressed;
        private float amount;

        private void Awake()
        {
            originalScale = transform.localScale;
            button = GetComponent<Button>();
            surface = GetComponentInChildren<UiSurface>();
        }

        private void OnDisable()
        {
            hovered = pressed = false;
            amount = 0f;
            transform.localScale = originalScale;
            if (surface != null) surface.Hover = 0f;
        }

        private void LateUpdate()
        {
            bool available = button != null && button.IsInteractable();
            float target = available && hovered ? 1f : 0f;
            amount = Mathf.Lerp(amount, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            float scale = available && pressed ? .988f : Mathf.Lerp(1f, HoverScale, amount);
            Vector3 nextScale = originalScale * scale;
            if ((transform.localScale - nextScale).sqrMagnitude > .00000005f) transform.localScale = nextScale;
            if (surface != null) surface.Hover = amount;
        }

        public void OnPointerEnter(PointerEventData data) => hovered = true;
        public void OnPointerExit(PointerEventData data) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData data) => pressed = false;
    }
}
