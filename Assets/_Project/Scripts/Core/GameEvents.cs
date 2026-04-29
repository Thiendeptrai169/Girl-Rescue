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
        }
    }

    // ── Payloads ──────────────────────────────────────────────────────────────
    public class SegmentDestroyedPayload
    {
        public CannonColor Color;
        public Vector3 Position;
    }
}
