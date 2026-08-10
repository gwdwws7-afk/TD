using System.Collections;
using UnityEngine;

namespace TD
{
    /// <summary>
    /// Lightweight coroutine-based UI animation helpers.
    /// No external dependencies (DOTween/LeanTween not required).
    /// Provides panel open/close scale+fade and simple value tweens.
    /// </summary>
    public static class TDUiAnimator
    {
        private const float DefaultDuration = 0.18f;

        /// <summary>
        /// Animate a RectTransform + CanvasGroup from scaled-down/faded to full.
        /// Adds a CanvasGroup if one doesn't exist. Returns the coroutine handle.
        /// </summary>
        public static Coroutine PanelOpen(MonoBehaviour host, RectTransform rt, float duration = DefaultDuration)
        {
            if (rt == null)
            {
                return null;
            }

            return host.StartCoroutine(PanelOpenRoutine(rt, duration));
        }

        /// <summary>
        /// Animate a panel to scaled-down/faded, then call onComplete.
        /// The caller should SetActive(false) in onComplete.
        /// </summary>
        public static Coroutine PanelClose(MonoBehaviour host, RectTransform rt, System.Action onComplete = null, float duration = DefaultDuration * 0.7f)
        {
            if (rt == null)
            {
                onComplete?.Invoke();
                return null;
            }

            return host.StartCoroutine(PanelCloseRoutine(rt, duration, onComplete));
        }

        /// <summary>Fade a CanvasGroup from current to target alpha over duration.</summary>
        public static Coroutine FadeTo(MonoBehaviour host, CanvasGroup cg, float targetAlpha, float duration)
        {
            if (cg == null)
            {
                return null;
            }

            return host.StartCoroutine(FadeRoutine(cg, targetAlpha, duration));
        }

        private static IEnumerator PanelOpenRoutine(RectTransform rt, float duration)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = rt.gameObject.AddComponent<CanvasGroup>();
            }

            var startScale = Vector3.one * 0.88f;
            var endScale = Vector3.one;
            rt.localScale = startScale;
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = EaseOutCubic(elapsed / duration);
                rt.localScale = Vector3.Lerp(startScale, endScale, t);
                cg.alpha = t;
                yield return null;
            }

            rt.localScale = endScale;
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
        }

        private static IEnumerator PanelCloseRoutine(RectTransform rt, float duration, System.Action onComplete)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            var startScale = rt.localScale;
            var endScale = Vector3.one * 0.88f;
            var startAlpha = cg != null ? cg.alpha : 1f;

            if (cg != null)
            {
                cg.blocksRaycasts = false;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = EaseInCubic(elapsed / duration);
                rt.localScale = Vector3.Lerp(startScale, endScale, t);
                if (cg != null)
                {
                    cg.alpha = Mathf.Lerp(startAlpha, 0f, t);
                }

                yield return null;
            }

            onComplete?.Invoke();
        }

        private static IEnumerator FadeRoutine(CanvasGroup cg, float targetAlpha, float duration)
        {
            var startAlpha = cg.alpha;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            cg.alpha = targetAlpha;
        }

        private static float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private static float EaseInCubic(float t)
        {
            return t * t * t;
        }
    }
}
