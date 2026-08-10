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
                ReturnToPool();
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

        /// <summary>
        /// Called by the object pool when this FX is returned.
        /// Clears animation state and releases the budget lease.
        /// </summary>
        public void ResetForPool()
        {
            if (_budgetLease)
            {
                _budgetLease = false;
                TDCombatFxBudget.Release(_budgetClass);
            }

            _timer = 0f;
            _duration = 0f;
            _startScale = Vector3.one;
            _endScale = Vector3.one;
            _startColor = Color.white;
            _endColor = Color.white;
            transform.localScale = Vector3.one;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (_renderer != null)
            {
                _renderer.color = Color.white;
                _renderer.sprite = null;
            }
        }

        private void ReturnToPool()
        {
            var pool = TDObjectPool.Instance;
            if (pool != null)
            {
                transform.SetParent(pool.transform, false);
                pool.ReleaseFx(this);
            }
            else
            {
                Destroy(gameObject);
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
                ReturnToPool();
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
