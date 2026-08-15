using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    /// <summary>
    /// Animation state for a sprite-based actor (tower or enemy).
    /// Idle loops continuously; Fire/Death play once then return to Idle.
    /// </summary>
    public enum TDAnimationState
    {
        Idle,
        Fire,
        Death
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TDSpriteAnimator : MonoBehaviour
    {
        private readonly List<Sprite> _idleFrames = new();
        private readonly List<Sprite> _fireFrames = new();
        private readonly List<Sprite> _deathFrames = new();
        private SpriteRenderer _renderer;
        private float _timer;
        private int _index;
        private float _fps = 8f;
        private TDAnimationState _state = TDAnimationState.Idle;

        // Per-state FPS overrides (0 = use default _fps)
        private float _fireFps;
        private float _deathFps;

        public bool IsConfigured => _idleFrames.Count > 0 && _renderer != null;
        public int FrameCount => _idleFrames.Count;
        public int CurrentFrame => _index;
        public TDAnimationState CurrentState => _state;
        public bool HasFireAnimation => _fireFrames.Count > 0;
        public bool HasDeathAnimation => _deathFrames.Count > 0;

        /// <summary>Raised after every sprite swap so owners can re-anchor (e.g. feet).</summary>
        public event System.Action OnFrameSwapped;

        public void Configure(string resourcePrefix, int frameCount, float fps, bool loop = true, bool randomStart = false)
        {
            _renderer = GetComponent<SpriteRenderer>();
            _idleFrames.Clear();
            _fireFrames.Clear();
            _deathFrames.Clear();
            _fps = Mathf.Max(1f, fps);
            _fireFps = 0f;
            _deathFps = 0f;
            _timer = 0f;
            _index = 0;
            _state = TDAnimationState.Idle;

            LoadFrames(_idleFrames, resourcePrefix, frameCount);

            if (_idleFrames.Count == 0)
            {
                enabled = false;
                return;
            }

            if (randomStart)
            {
                _index = Random.Range(0, _idleFrames.Count);
            }

            SetFrameSprite(_idleFrames[_index]);
            enabled = _idleFrames.Count > 1;
        }

        /// <summary>
        /// Load a secondary fire animation. Frames are loaded from
        /// "{resourcePrefix}_fire_{00,01,02...}". If no frames exist, fire
        /// is a no-op (the idle loop continues).
        /// </summary>
        public void ConfigureFire(string resourcePrefix, int frameCount, float fps)
        {
            _fireFrames.Clear();
            LoadFrames(_fireFrames, $"{resourcePrefix}_fire", frameCount);
            _fireFps = Mathf.Max(1f, fps);
        }

        /// <summary>
        /// Load a death animation. Frames are loaded from
        /// "{resourcePrefix}_death_{00,01,02...}".
        /// </summary>
        public void ConfigureDeath(string resourcePrefix, int frameCount, float fps)
        {
            _deathFrames.Clear();
            LoadFrames(_deathFrames, $"{resourcePrefix}_death", frameCount);
            _deathFps = Mathf.Max(1f, fps);
        }

        /// <summary>Trigger the fire animation. Returns to idle when complete.</summary>
        public void PlayFire()
        {
            if (_fireFrames.Count == 0 || _renderer == null)
            {
                return;
            }

            _state = TDAnimationState.Fire;
            _index = 0;
            _timer = 0f;
            SetFrameSprite(_fireFrames[0]);
            enabled = true;
        }

        /// <summary>Trigger the death animation. Does not return to idle.</summary>
        public void PlayDeath()
        {
            if (_deathFrames.Count == 0 || _renderer == null)
            {
                return;
            }

            _state = TDAnimationState.Death;
            _index = 0;
            _timer = 0f;
            SetFrameSprite(_deathFrames[0]);
            enabled = true;
        }

        public void Restart(int frameIndex = 0)
        {
            if (_idleFrames.Count == 0 || _renderer == null)
            {
                return;
            }

            _state = TDAnimationState.Idle;
            _index = Mathf.Clamp(frameIndex, 0, _idleFrames.Count - 1);
            _timer = 0f;
            SetFrameSprite(_idleFrames[_index]);
            enabled = _idleFrames.Count > 1;
        }

        private static void LoadFrames(List<Sprite> target, string prefix, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var sprite = Resources.Load<Sprite>($"{prefix}_{i:00}");
                if (sprite != null)
                {
                    target.Add(sprite);
                }
            }
        }

        private void Update()
        {
            var frames = _state switch
            {
                TDAnimationState.Fire => _fireFrames,
                TDAnimationState.Death => _deathFrames,
                _ => _idleFrames,
            };

            if (frames.Count <= 1 || _renderer == null)
            {
                return;
            }

            var effectiveFps = _state switch
            {
                TDAnimationState.Fire => _fireFps > 0 ? _fireFps : _fps,
                TDAnimationState.Death => _deathFps > 0 ? _deathFps : _fps,
                _ => _fps,
            };

            _timer += Time.deltaTime;
            var frameDuration = 1f / effectiveFps;
            if (_timer < frameDuration)
            {
                return;
            }

            var steps = Mathf.FloorToInt(_timer / frameDuration);
            _timer -= steps * frameDuration;
            steps = Mathf.Clamp(steps, 1, 3);
            _index += steps;

            if (_index >= frames.Count)
            {
                if (_state == TDAnimationState.Idle)
                {
                    // Loop idle.
                    _index %= frames.Count;
                }
                else
                {
                    // Fire/Death: return to idle (or hold last frame for death).
                    if (_state == TDAnimationState.Death)
                    {
                        _index = frames.Count - 1;
                        enabled = false;
                    }
                    else
                    {
                        ReturnToIdle();
                    }
                }
            }

            var currentFrames = _state switch
            {
                TDAnimationState.Fire => _fireFrames,
                TDAnimationState.Death => _deathFrames,
                _ => _idleFrames,
            };
            if (_index < currentFrames.Count)
            {
                SetFrameSprite(currentFrames[_index]);
            }
        }

        private void ReturnToIdle()
        {
            _state = TDAnimationState.Idle;
            _index = _idleFrames.Count > 0 ? Random.Range(0, _idleFrames.Count) : 0;
            _timer = 0f;
            if (_idleFrames.Count > 0)
            {
                SetFrameSprite(_idleFrames[_index]);
            }

            enabled = _idleFrames.Count > 1;
        }

        private void SetFrameSprite(Sprite sprite)
        {
            if (_renderer == null || sprite == null || _renderer.sprite == sprite)
            {
                return;
            }

            _renderer.sprite = sprite;
            OnFrameSwapped?.Invoke();
        }
    }
}
