using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TDSpriteAnimator : MonoBehaviour
    {
        private readonly List<Sprite> _frames = new();
        private SpriteRenderer _renderer;
        private float _timer;
        private int _index;
        private float _fps = 8f;
        private bool _loop = true;

        public void Configure(string resourcePrefix, int frameCount, float fps, bool loop = true, bool randomStart = false)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _frames.Clear();
            _fps = Mathf.Max(1f, fps);
            _loop = loop;
            _timer = 0f;
            _index = 0;

            for (var i = 0; i < frameCount; i++)
            {
                var sprite = Resources.Load<Sprite>($"{resourcePrefix}_{i:00}");
                if (sprite != null)
                {
                    _frames.Add(sprite);
                }
            }

            if (_frames.Count == 0)
            {
                enabled = false;
                return;
            }

            if (randomStart)
            {
                _index = Random.Range(0, _frames.Count);
            }

            _renderer.sprite = _frames[_index];
            enabled = _frames.Count > 1;
        }

        private void Update()
        {
            if (_frames.Count <= 1 || _renderer == null)
            {
                return;
            }

            _timer += Time.deltaTime;
            var frameDuration = 1f / _fps;
            if (_timer < frameDuration)
            {
                return;
            }

            var steps = Mathf.FloorToInt(_timer / frameDuration);
            _timer -= steps * frameDuration;

            // Cap catch-up so large frame hitches do not skip too many poses.
            steps = Mathf.Clamp(steps, 1, 3);
            _index += steps;

            if (_index >= _frames.Count)
            {
                if (_loop)
                {
                    _index %= _frames.Count;
                }
                else
                {
                    _index = _frames.Count - 1;
                    enabled = false;
                }
            }

            _renderer.sprite = _frames[_index];
        }
    }
}
