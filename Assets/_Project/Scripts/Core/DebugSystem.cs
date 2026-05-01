using UnityEngine;

namespace DragonRescue.Core
{
    [AddComponentMenu("Dragon Rescue/Core/Debug System")]
    public class DebugSystem : Singleton<DebugSystem>
    {
        [Header("Master")]
        [SerializeField] private bool _enableLogs = true;

        [Header("Categories")]
        [SerializeField] private bool _debugBoard;
        [SerializeField] private bool _debugCannon;
        [SerializeField] private bool _debugProjectile;
        [SerializeField] private bool _debugDragon;
        [SerializeField] private bool _debugBooster;
        [SerializeField] private bool _debugInput;
        [SerializeField] private bool _debugUI;
        [SerializeField] private bool _debugPooling;
        [SerializeField] private bool _debugGame;
        [SerializeField] private bool _debugLevel;
        [SerializeField] private bool _debugData;

        public static void Log(DebugCategory category, string message, Object context = null)
        {
            if (!IsEnabled(category)) return;
            Debug.Log(Format(category, message), context);
        }

        public static void Warning(DebugCategory category, string message, Object context = null)
        {
            if (!IsEnabled(category)) return;
            Debug.LogWarning(Format(category, message), context);
        }

        public static void Error(DebugCategory category, string message, Object context = null)
        {
            if (!IsEnabled(category)) return;
            Debug.LogError(Format(category, message), context);
        }

        public static void AlwaysLog(DebugCategory category, string message, Object context = null)
        {
            Debug.Log(Format(category, message), context);
        }

        public static void AlwaysWarning(DebugCategory category, string message, Object context = null)
        {
            Debug.LogWarning(Format(category, message), context);
        }

        public static void AlwaysError(DebugCategory category, string message, Object context = null)
        {
            Debug.LogError(Format(category, message), context);
        }

        public static void Exception(DebugCategory category, System.Exception exception, Object context = null)
        {
            if (!IsEnabled(category)) return;
            Debug.LogException(exception, context);
        }

        public static bool IsEnabled(DebugCategory category)
        {
            DebugSystem system = Instance != null ? Instance : Object.FindFirstObjectByType<DebugSystem>();
            return system != null && system._enableLogs && system.IsCategoryEnabled(category);
        }

        private bool IsCategoryEnabled(DebugCategory category)
        {
            return category switch
            {
                DebugCategory.Board => _debugBoard,
                DebugCategory.Cannon => _debugCannon,
                DebugCategory.Projectile => _debugProjectile,
                DebugCategory.Dragon => _debugDragon,
                DebugCategory.Booster => _debugBooster,
                DebugCategory.Input => _debugInput,
                DebugCategory.UI => _debugUI,
                DebugCategory.Pooling => _debugPooling,
                DebugCategory.Game => _debugGame,
                DebugCategory.Level => _debugLevel,
                DebugCategory.Data => _debugData,
                _ => false
            };
        }

        private static string Format(DebugCategory category, string message)
        {
            return $"[{category}] {message}";
        }
    }
}
