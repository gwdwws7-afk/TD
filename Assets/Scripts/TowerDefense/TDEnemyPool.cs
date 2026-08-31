using System.Collections.Generic;
using UnityEngine;

namespace TD
{
    /// <summary>
    /// Enemy object pool (freeze-period, between S4 and S5 per the split
    /// design's sequencing ruling). SpawnEnemy used to build root+Shadow+
    /// Visual GameObjects and 5+ components per enemy and Destroy them on
    /// death — the single largest spawn-stall source in swarm/split waves.
    ///
    /// Pooling contract (the four destruction paths all funnel here):
    ///   kill (death reel + fade), escape, level switch clear, defeat sweep.
    /// A released enemy is deactivated and re-parented under this component;
    /// TDEnemy.Initialize fully resets it on the next Get.
    ///
    /// Per-kind pools: the enemy hierarchy shape (shadow offset, visual
    /// prefab layout, foot anchors) is kind-specific enough that mixing
    /// kinds in one pool would fight the art pipeline — each kind gets its
    /// own stack. Pop beyond the warm count instantiates; the pool never
    /// destroys (a leaked enemy just becomes available next level).
    /// </summary>
    public sealed class TDEnemyPool : MonoBehaviour
    {
        public static TDEnemyPool Instance { get; private set; }

        [SerializeField] private int warmCountPerKind = 4;

        private readonly Dictionary<string, Stack<GameObject>> _pools = new();
        private readonly Dictionary<GameObject, string> _kindByInstance = new();

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Take an enemy hierarchy for the given kind. The template is the
        /// freshly-built hierarchy SpawnEnemy constructs on a miss — the
        /// pool stores the first instance per kind and clones further warm
        /// entries from it, so the art pipeline keeps exactly one authority.
        /// </summary>
        public GameObject Get(string enemyId, Transform parent)
        {
            if (_pools.TryGetValue(enemyId, out var stack))
            {
                while (stack.Count > 0)
                {
                    var candidate = stack.Pop();
                    if (candidate != null)
                    {
                        candidate.transform.SetParent(parent, true);
                        candidate.SetActive(true);
                        return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>Return a dead/escaped enemy for reuse. Safe on null or
        /// foreign objects (non-pooled instances are ignored).</summary>
        public void Release(GameObject enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (!_kindByInstance.TryGetValue(enemy, out var kind))
            {
                // Not one of ours (built before the pool existed this level):
                // destroy as before.
                Destroy(enemy);
                return;
            }

            enemy.SetActive(false);
            enemy.transform.SetParent(transform, false);
            if (!_pools.TryGetValue(kind, out var stack))
            {
                stack = new Stack<GameObject>();
                _pools[kind] = stack;
            }

            stack.Push(enemy);
        }

        /// <summary>
        /// Register a freshly-built enemy hierarchy as the kind's pool
        /// member (called by SpawnEnemy on every build; registration is
        /// idempotent). Unregistered builds behave exactly like the pre-pool
        /// world — they Destroy on death.
        /// </summary>
        public void Register(GameObject enemy, string enemyId)
        {
            if (enemy == null || string.IsNullOrEmpty(enemyId))
            {
                return;
            }

            _kindByInstance[enemy] = enemyId;
        }

        public void Clear()
        {
            foreach (var pair in _pools)
            {
                while (pair.Value.Count > 0)
                {
                    var candidate = pair.Value.Pop();
                    if (candidate != null)
                    {
                        Destroy(candidate);
                    }
                }
            }

            _pools.Clear();
            _kindByInstance.Clear();
        }
    }
}
