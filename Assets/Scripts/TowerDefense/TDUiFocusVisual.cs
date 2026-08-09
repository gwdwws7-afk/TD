using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TD
{
    [DisallowMultipleComponent]
    public sealed class TDUiFocusVisual : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private RectTransform _focusRoot;
        private Selectable _selectable;

        public bool IsFocused => _focusRoot != null && _focusRoot.gameObject.activeSelf;

        public static TDUiFocusVisual Attach(Button button)
        {
            return Attach((Selectable)button);
        }

        public static TDUiFocusVisual Attach(Selectable selectable)
        {
            if (selectable == null)
            {
                return null;
            }

            var visual = selectable.GetComponent<TDUiFocusVisual>() ?? selectable.gameObject.AddComponent<TDUiFocusVisual>();
            visual.Initialize(selectable);
            return visual;
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetFocused(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetFocused(false);
        }

        private void Initialize(Selectable selectable)
        {
            _selectable = selectable;
            _selectable.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            if (_focusRoot != null)
            {
                return;
            }

            var rootObject = new GameObject("Controller Focus", typeof(RectTransform));
            rootObject.transform.SetParent(transform, false);
            _focusRoot = rootObject.GetComponent<RectTransform>();
            _focusRoot.anchorMin = Vector2.zero;
            _focusRoot.anchorMax = Vector2.one;
            _focusRoot.pivot = new Vector2(0.5f, 0.5f);
            _focusRoot.anchoredPosition = Vector2.zero;
            _focusRoot.sizeDelta = new Vector2(6f, 6f);
            CreateRail("Focus Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f), new Vector2(-8f, 3f));
            CreateRail("Focus Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-8f, 3f));
            CreateRail("Focus Left", Vector2.zero, new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(3f, -8f));
            CreateRail("Focus Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-1f, 0f), new Vector2(3f, -8f));
            _focusRoot.SetAsLastSibling();
            SetFocused(false);
        }

        private void CreateRail(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var railObject = new GameObject(name, typeof(RectTransform));
            railObject.transform.SetParent(_focusRoot, false);
            var rect = railObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = railObject.AddComponent<Image>();
            TDUiWorldSkin.ApplyRule(image, new Color(0.30f, 0.88f, 1f, 0.98f), size.y > size.x);
            image.raycastTarget = false;
        }

        private void SetFocused(bool focused)
        {
            if (_focusRoot != null)
            {
                _focusRoot.gameObject.SetActive(focused && (_selectable == null || _selectable.interactable));
            }
        }
    }
}
