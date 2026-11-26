using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectoRTS.UI
{
    /// <summary>
    /// Shows the shared tooltip while the pointer is over this element and
    /// hides it as soon as the pointer leaves or the element is disabled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TooltipTrigger : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler
    {
        private string _title;
        private string _body;
        private bool _hovered;

        public void Configure(string title, string body)
        {
            _title = title;
            _body = body;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(_title) && string.IsNullOrEmpty(_body))
            {
                return;
            }

            _hovered = true;
            UiTooltip.Show(this, _title, _body);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Dismiss();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Dismiss();
        }

        private void OnDisable()
        {
            Dismiss();
        }

        private void Dismiss()
        {
            if (!_hovered)
            {
                return;
            }

            _hovered = false;
            UiTooltip.Hide(this);
        }
    }
}
