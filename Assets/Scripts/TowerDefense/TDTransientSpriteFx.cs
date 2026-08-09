using UnityEngine;

namespace TD
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TDTransientSpriteFx : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private float _duration;
        private float _timer;
        private Vector3 _startScale = Vector3.one;
        private Vector3 _endScale = Vector3.one;
        private Color _startColor = Color.white;
        private Color _endColor = Color.white;
        private TDCombatFxClass _budgetClass;
        private bool _budgetLease;

        public void Configure(float duration, Vector3 startScale, Vector3 endScale, Color startColor, Color endColor)
        {
            _budgetClass = TDCombatFxBudget.Classify(gameObject.name);
            _budgetLease = TDCombatFxBudget.TryAcquire(_budgetClass);
            if (!_budgetLease)
            {
                Destroy(gameObject);
                return;
            }

            _renderer = GetComponent<SpriteRenderer>();
            _duration = TDCombatFxBudget.ClampDuration(_budgetClass, duration);
            _startScale = startScale;
            _endScale = endScale;
            _startColor = TDCombatFxBudget.ClampColor(_budgetClass, startColor);
            _endColor = TDCombatFxBudget.ClampColor(_budgetClass, endColor);
            _timer = 0f;

            transform.localScale = _startScale;
            if (_renderer != null)
            {
                _renderer.color = _startColor;
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            var t = Mathf.Clamp01(_timer / _duration);

            transform.localScale = Vector3.Lerp(_startScale, _endScale, t);
            if (_renderer != null)
            {
                _renderer.color = Color.Lerp(_startColor, _endColor, t);
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_budgetLease)
            {
                _budgetLease = false;
                TDCombatFxBudget.Release(_budgetClass);
            }
        }
    }
}
