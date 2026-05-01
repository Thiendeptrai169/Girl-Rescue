using UnityEngine;

namespace DragonRescue.Core
{
    /// <summary>
    /// Generic Singleton base class.
    /// Inherit from this to guarantee exactly one instance of a Manager exists.
    /// Only use for true global managers (GameManager, LevelManager).
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }

        [SerializeField] private bool _persistent = false;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DebugSystem.Warning(DebugCategory.Game, $"Duplicate instance of {typeof(T).Name} detected. Destroying duplicate.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this as T;    

            if (_persistent)
                DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
