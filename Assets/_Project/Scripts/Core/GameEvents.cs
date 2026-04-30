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

        public static void FireLevelWin()  => OnLevelWin?.Invoke();
        public static void FireLevelLose() => OnLevelLose?.Invoke();

        // ── Dragon Events ────────────────────────────────────────────────────
        public static event Action<SegmentDestroyedPayload> OnSegmentDestroyed;

        public static void FireSegmentDestroyed(SegmentDestroyedPayload payload)
            => OnSegmentDestroyed?.Invoke(payload);

        public static event Action<BlockEscapedPayload> OnBlockEscaped;

        public static void FireBlockEscaped(BlockEscapedPayload payload)
            => OnBlockEscaped?.Invoke(payload);

        // ── Cannon Events ────────────────────────────────────────────────────
        public static event Action<CannonLoadedPayload> OnCannonLoaded;
        public static event Action<CannonDepletedPayload> OnCannonDepleted;

        public static void FireCannonLoaded(CannonLoadedPayload payload) => OnCannonLoaded?.Invoke(payload);
        public static void FireCannonDepleted(CannonDepletedPayload payload) => OnCannonDepleted?.Invoke(payload);

        // ── Projectile Events ────────────────────────────────────────────────
        public static event Action<ProjectileHitPayload> OnProjectileHit;

        public static void FireProjectileHit(ProjectileHitPayload payload) => OnProjectileHit?.Invoke(payload);

        // ── Cleanup ──────────────────────────────────────────────────────────
        /// <summary>
        /// Call when unloading a level to prevent stale subscriptions.
        /// Does NOT clear OnGameStateChanged — that persists across levels.
        /// </summary>
        public static void ClearLevelEvents()
        {
            OnLevelWin        = null;
            OnLevelLose       = null;
            OnSegmentDestroyed = null;
            OnBlockEscaped    = null;
            OnCannonLoaded    = null;
            OnCannonDepleted  = null;
            OnProjectileHit   = null;
        }
    }

    // ── Payloads ──────────────────────────────────────────────────────────────
    public class SegmentDestroyedPayload
    {
        public CannonColor Color;
        public Vector3 Position;
    }

    public class BlockEscapedPayload
    {
        public CannonColor Color;
        public int Ammo;
        public Vector3 ExitPosition;
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

    public class ProjectileHitPayload
    {
        public CannonColor Color;
        public int Damage;
        public Vector3 HitPosition;
    }
}
