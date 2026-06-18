using UnityEngine;

namespace TD
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TDTransientLineFx : MonoBehaviour
    {
        private static Material _sharedLineMaterial;

        private LineRenderer _lineRenderer;
        private float _duration;
        private float _timer;
        private float _startWidth;
        private float _endWidth;
        private Color _startColor;
        private Color _endColor;

        public void Configure(
            Vector3 startPoint,
            Vector3 endPoint,
            float duration,
            float startWidth,
            float endWidth,
            Color startColor,
            Color endColor,
            int sortingOrder)
        {
            _lineRenderer = GetComponent<LineRenderer>();
            var lineMaterial = GetSharedLineMaterial();
            if (lineMaterial != null)
            {
                _lineRenderer.sharedMaterial = lineMaterial;
            }
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.loop = false;
            _lineRenderer.positionCount = 2;
            _lineRenderer.numCapVertices = 4;
            _lineRenderer.numCornerVertices = 2;
            _lineRenderer.textureMode = LineTextureMode.Stretch;
            _lineRenderer.alignment = LineAlignment.View;
            _lineRenderer.sortingOrder = sortingOrder;
            _lineRenderer.SetPosition(0, startPoint);
            _lineRenderer.SetPosition(1, endPoint);

            _duration = Mathf.Max(0.01f, duration);
            _startWidth = Mathf.Max(0.001f, startWidth);
            _endWidth = Mathf.Max(0.001f, endWidth);
            _startColor = startColor;
            _endColor = endColor;
            _timer = 0f;

            _lineRenderer.startWidth = _startWidth;
            _lineRenderer.endWidth = _startWidth;
            _lineRenderer.startColor = _startColor;
            _lineRenderer.endColor = _startColor;
        }

        private static Material GetSharedLineMaterial()
        {
            if (_sharedLineMaterial != null)
            {
                return _sharedLineMaterial;
            }

            var shader = Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("UI/Default");
            if (shader == null)
            {
                return null;
            }

            _sharedLineMaterial = new Material(shader);
            _sharedLineMaterial.hideFlags = HideFlags.HideAndDontSave;
            return _sharedLineMaterial;
        }

        private void Update()
        {
            if (_lineRenderer == null)
            {
                Destroy(gameObject);
                return;
            }

            _timer += Time.deltaTime;
            var t = Mathf.Clamp01(_timer / _duration);

            var width = Mathf.Lerp(_startWidth, _endWidth, t);
            var color = Color.Lerp(_startColor, _endColor, t);

            _lineRenderer.startWidth = width;
            _lineRenderer.endWidth = width;
            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
