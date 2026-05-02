using System;
using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Core
{
    /// <summary>
    /// Central static event bus for decoupled communication.
    /// All game-wide events live here. No local events on entities.
    /// </summary>
    public static class GameEvents
    {
        // ── Game State ───────────────────────────────────────────────────────
        public static event Action<GameState> OnGameStateChanged;

        public static void FireGameStateChanged(GameState state)
            => OnGameStateChanged?.Invoke(state);

        // ── Level Events ─────────────────────────────────────────────────────
        public static event Action OnLevelWin;
        public static event Action OnLevelLose;
        public static event Action<LevelConfig> OnLevelStarted;

        public static void FireLevelWin()  => OnLevelWin?.Invoke();
        public static void FireLevelLose() => OnLevelLose?.Invoke();
        public static void FireLevelStarted(LevelConfig config) => OnLevelStarted?.Invoke(config);

        public static event Action<float> OnProgressUpdated;
        public static void FireProgressUpdated(float percent) => OnProgressUpdated?.Invoke(percent);

        // ── Dragon Events ────────────────────────────────────────────────────
        public static event Action<SegmentDestroyedPayload> OnSegmentDestroyed;
        public static event Action<DragonSegmentsSortedPayload> OnDragonSegmentsSorted;

        public static void FireSegmentDestroyed(SegmentDestroyedPayload payload)
            => OnSegmentDestroyed?.Invoke(payload);
        public static void FireDragonSegmentsSorted(DragonSegmentsSortedPayload payload)
            => OnDragonSegmentsSorted?.Invoke(payload);

        public static event Action<BlockEscapedPayload> OnBlockEscaped;
        public static event Action<BlockSpawnedPayload> OnBlockSpawned;
        public static event Action<BlockFeedbackPayload> OnBlockBlocked;
        public static event Action<BlockFeedbackPayload> OnBlockSlotFull;

        public static void FireBlockEscaped(BlockEscapedPayload payload)
            => OnBlockEscaped?.Invoke(payload);
        public static void FireBlockSpawned(BlockSpawnedPayload payload)
            => OnBlockSpawned?.Invoke(payload);
        public static void FireBlockBlocked(BlockFeedbackPayload payload)
            => OnBlockBlocked?.Invoke(payload);
        public static void FireBlockSlotFull(BlockFeedbackPayload payload)
            => OnBlockSlotFull?.Invoke(payload);

        // ── Cannon Events ────────────────────────────────────────────────────
        public static Func<int, bool> RequestSlotCapacity;
        public static event Action<CannonLoadedPayload> OnCannonLoaded;
        public static event Action<CannonDepletedPayload> OnCannonDepleted;
        public static event Action<CannonSlotStatePayload> OnCannonSlotStateChanged;
        public static event Action<CannonAmmoChangedPayload> OnCannonAmmoChanged;

        public static void FireCannonLoaded(CannonLoadedPayload payload) => OnCannonLoaded?.Invoke(payload);
        public static void FireCannonDepleted(CannonDepletedPayload payload) => OnCannonDepleted?.Invoke(payload);
        public static void FireCannonSlotStateChanged(CannonSlotStatePayload payload) => OnCannonSlotStateChanged?.Invoke(payload);
        public static void FireCannonAmmoChanged(CannonAmmoChangedPayload payload) => OnCannonAmmoChanged?.Invoke(payload);

        // ── Projectile Events ────────────────────────────────────────────────
        public static event Action<ProjectileHitPayload> OnProjectileHit;

        public static void FireProjectileHit(ProjectileHitPayload payload) => OnProjectileHit?.Invoke(payload);

        // ── Booster Events ───────────────────────────────────────────────────
        public static event Action<BoosterType, int> OnBoosterChargeChanged;
        public static event Action<BoosterType?> OnBoosterSelectionModeChanged;

        public static void FireBoosterChargeChanged(BoosterType type, int charges) => OnBoosterChargeChanged?.Invoke(type, charges);
        public static void FireBoosterSelectionModeChanged(BoosterType? type) => OnBoosterSelectionModeChanged?.Invoke(type);

        // ── Cleanup ──────────────────────────────────────────────────────────
        /// <summary>
        /// Call when unloading a level to prevent stale subscriptions.
        /// Does NOT clear OnGameStateChanged — that persists across levels.
        /// </summary>
        public static void ClearLevelEvents()
        {
            OnSegmentDestroyed = null;
            OnDragonSegmentsSorted = null;
            OnBlockEscaped    = null;
            OnBlockSpawned    = null;
            OnBlockBlocked    = null;
            OnBlockSlotFull   = null;
            OnCannonLoaded    = null;
            OnCannonDepleted  = null;
            OnCannonSlotStateChanged = null;
            OnCannonAmmoChanged = null;
            OnProjectileHit   = null;
            RequestSlotCapacity = null;
        }
    }

    // ── Payloads ──────────────────────────────────────────────────────────────
    public class SegmentDestroyedPayload
    {
        public CannonColor Color;
        public Vector3 Position;
    }

    public class DragonSegmentsSortedPayload
    {
        public DragonRescue.Entities.Dragon.DragonManager Manager;
        public DragonRescue.Entities.Dragon.DragonSegmentIdentity[] OrderedSegments;
    }

    public class BlockEscapedPayload
    {
        public CannonColor Color;
        public int Ammo;
        public Vector3 ExitPosition;
    }

    public class BlockSpawnedPayload
    {
        public DragonRescue.Entities.Board.BlockIdentity Block;
        public CannonColor Color;
        public Direction Direction;
        public Vector2Int Size;
        public float CellSize;
    }

    public class BlockFeedbackPayload
    {
        public DragonRescue.Entities.Board.BlockIdentity Block;
        public float Duration;
    }

    public class CannonLoadedPayload
    {
        public CannonColor Color;
        public int SlotIndex;
    }

    public class CannonDepletedPayload
    {
        public int SlotIndex;
    }

    public class CannonSlotStatePayload
    {
        public int SlotIndex;
        public bool IsUnlocked;
        public bool IsLoaded;
        public CannonColor Color;
    }

    public class CannonAmmoChangedPayload
    {
        public int SlotIndex;
        public int Ammo;
        public bool IsLoaded;
    }

    public class ProjectileHitPayload
    {
        public CannonColor Color;
        public int Damage;
        public Vector3 HitPosition;
    }
}
