using UnityEngine;
using UnityEngine.Pool;

namespace TD
{
    /// <summary>
    /// Centralized object pool for transient gameplay objects (projectiles, FX).
    /// Eliminates per-frame Instantiate/Destroy GC churn during dense combat.
    ///
    /// Usage:
    ///   var projectile = TDObjectPool.Instance.GetProjectile();
    ///   projectile.Initialize(...);
    ///   // ... when done, projectile calls ReturnToPool() instead of Destroy()
    /// </summary>
    public sealed class TDObjectPool : MonoBehaviour
    {
        public static TDObjectPool Instance { get; private set; }

        [SerializeField] private int _projectileDefaultCapacity = 32;
        [SerializeField] private int _projectileMaxSize = 128;
        [SerializeField] private int _fxDefaultCapacity = 48;
        [SerializeField] private int _fxMaxSize = 192;

        private IObjectPool<TDProjectile> _projectilePool;
        private IObjectPool<TDTransientSpriteFx> _fxPool;

        // Track whether Initialize has been called (domain reload resets static Instance)
        private bool _initialized;

        private void Awake()
        {
            Instance = this;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _projectilePool = new ObjectPool<TDProjectile>(
                CreatePooledProjectile,
                OnGetProjectile,
                OnReleaseProjectile,
                OnDestroyPooledObject,
                collectionCheck: false,
                _projectileDefaultCapacity,
                _projectileMaxSize);

            _fxPool = new ObjectPool<TDTransientSpriteFx>(
                CreatePooledFx,
                OnGetFx,
                OnReleaseFx,
                OnDestroyPooledObject,
                collectionCheck: false,
                _fxDefaultCapacity,
                _fxMaxSize);
        }

        // ─── Public API ───────────────────────────────────────────────

        /// <summary>
        /// Get a projectile from the pool. The returned GameObject is active,
        /// has a SpriteRenderer + TDProjectile, and is parented to this pool's transform.
        /// Call projectile.Initialize(...) right after to configure it.
        /// </summary>
        public TDProjectile GetProjectile()
        {
            EnsureInitialized();
            return _projectilePool.Get();
        }

        /// <summary>Return a projectile to the pool for reuse.</summary>
        public void ReleaseProjectile(TDProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            _projectilePool.Release(projectile);
        }

        /// <summary>
        /// Get an FX sprite object from the pool. Returns an active GameObject
        /// with SpriteRenderer + TDTransientSpriteFx. Call fx.Configure(...) after.
        /// </summary>
        public TDTransientSpriteFx GetFx()
        {
            EnsureInitialized();
            return _fxPool.Get();
        }

        /// <summary>Return an FX object to the pool for reuse.</summary>
        public void ReleaseFx(TDTransientSpriteFx fx)
        {
            if (fx == null)
            {
                return;
            }

            _fxPool.Release(fx);
        }

        // ─── Projectile pool callbacks ─────────────────────────────────

        private TDProjectile CreatePooledProjectile()
        {
            var go = new GameObject("Pooled_Projectile");
            go.transform.SetParent(transform, false);
            go.AddComponent<SpriteRenderer>();
            var projectile = go.AddComponent<TDProjectile>();
            return projectile;
        }

        private void OnGetProjectile(TDProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            var go = projectile.gameObject;
            go.SetActive(true);
        }

        private void OnReleaseProjectile(TDProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            // The projectile's own ResetForPool clears its state.
            projectile.ResetForPool();
            projectile.gameObject.SetActive(false);
        }

        // ─── FX pool callbacks ─────────────────────────────────────────

        private TDTransientSpriteFx CreatePooledFx()
        {
            var go = new GameObject("Pooled_Fx");
            go.transform.SetParent(transform, false);
            go.AddComponent<SpriteRenderer>();
            var fx = go.AddComponent<TDTransientSpriteFx>();
            return fx;
        }

        private void OnGetFx(TDTransientSpriteFx fx)
        {
            if (fx == null)
            {
                return;
            }

            fx.gameObject.SetActive(true);
        }

        private void OnReleaseFx(TDTransientSpriteFx fx)
        {
            if (fx == null)
            {
                return;
            }

            fx.ResetForPool();
            fx.gameObject.SetActive(false);
        }

        // ─── Shared ────────────────────────────────────────────────────

        /// <summary>
        /// Helper for FX spawners: gets an FX from the pool (or creates a new one
        /// if pool is unavailable), parents it, positions it, and returns both the
        /// GameObject and its TDTransientSpriteFx for configuration.
        /// </summary>
        public static (GameObject go, TDTransientSpriteFx fx, SpriteRenderer renderer) GetFxObject(
            Transform parent,
            Vector3 worldPosition,
            string name)
        {
            if (Instance != null)
            {
                var fx = Instance.GetFx();
                var go = fx.gameObject;
                go.name = name;
                go.transform.SetParent(parent, true);
                go.transform.position = worldPosition;
                var renderer = fx.GetComponent<SpriteRenderer>();
                return (go, fx, renderer);
            }

            // Fallback: no pool available (e.g. outside of play mode)
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, true);
            obj.transform.position = worldPosition;
            var rend = obj.AddComponent<SpriteRenderer>();
            var transientFx = obj.AddComponent<TDTransientSpriteFx>();
            return (obj, transientFx, rend);
        }

        private static void OnDestroyPooledObject(Object obj)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
    }
}
