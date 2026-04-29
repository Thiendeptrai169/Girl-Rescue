using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DragonRescue.Core
{
    /// <summary>
    /// Generic Object Pool Manager.
    /// Handles creation and caching of Unity ObjectPools to keep LevelManager clean (SRP).
    /// </summary>
    public class PoolManager : Singleton<PoolManager>
    {
        private readonly Dictionary<string, ObjectPool<GameObject>> _pools = new();

        /// <summary>
        /// Gets an object from the pool for the specified prefab.
        /// If the pool doesn't exist, it creates one.
        /// </summary>
        public GameObject Get(GameObject prefab, Transform parent = null)
        {
            var pool = GetOrCreatePool(prefab);
            var instance = pool.Get();
            if (parent != null) instance.transform.SetParent(parent, false);
            return instance;
        }

        /// <summary>
        /// Releases an object back to its pool.
        /// </summary>
        public void Release(GameObject prefab, GameObject instance)
        {
            var pool = GetOrCreatePool(prefab);
            pool.Release(instance);
        }

        private ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
        {
            string key = prefab.name;

            if (_pools.TryGetValue(key, out var existingPool))
                return existingPool;

            var newPool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    var go = Instantiate(prefab);
                    go.name = key;
                    return go;
                },
                actionOnGet: go => go.SetActive(true),
                actionOnRelease: go =>
                {
                    go.SetActive(false);
                    go.transform.SetParent(transform); // Parent to PoolManager for clean hierarchy
                },
                actionOnDestroy: go => Destroy(go),
                collectionCheck: true,
                defaultCapacity: 10,
                maxSize: 50
            );

            _pools.Add(key, newPool);
            return newPool;
        }
    }
}
