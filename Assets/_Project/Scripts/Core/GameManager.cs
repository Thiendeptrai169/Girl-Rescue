using System;
using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Core
{
    /// <summary>
    /// Single source of truth for the current game state.
    /// Owns the state machine and notifies listeners via OnGameStateChanged.
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [SerializeField] private LevelDefinition _currentLevel;

        // ── State ─────────────────────────────────────────────────────────────
        public GameState CurrentState { get; private set; } = GameState.Loading;

        // ── Events (Observer Pattern) ─────────────────────────────────────────
        /// <summary>Fired whenever the game state changes.</summary>
        public event Action<GameState> OnGameStateChanged;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Start()
        {
            if (_currentLevel == null)
            {
                Debug.LogError("[GameManager] No LevelDefinition assigned! Drag a Level asset into the Inspector.");
                return;
            }

            StartLevel();
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void StartLevel()
        {
            SetState(GameState.Playing);
            LevelManager.Instance.InitLevel(_currentLevel);
        }

        public void WinLevel()
        {
            SetState(GameState.Won);
            // TODO: trigger win screen
        }

        public void LoseLevel()
        {
            SetState(GameState.Lost);
            // TODO: trigger lose screen
        }

        public void PauseLevel()
        {
            if (CurrentState != GameState.Playing) return;
            SetState(GameState.Paused);
        }

        public void ResumeLevel()
        {
            if (CurrentState != GameState.Paused) return;
            SetState(GameState.Playing);
        }

        // ── Private ───────────────────────────────────────────────────────────
        private void SetState(GameState newState)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            Debug.Log($"[GameManager] State → {newState}");
            OnGameStateChanged?.Invoke(newState);
        }

        // ── Debug ─────────────────────────────────────────────────────────────
        [ContextMenu("Debug / Force Win")]
        private void DebugForceWin() => WinLevel();

        [ContextMenu("Debug / Force Lose")]
        private void DebugForceLose() => LoseLevel();

        [ContextMenu("Debug / Reload Level")]
        private void DebugReloadLevel()
        {
            LevelManager.Instance.ClearLevel();
            StartLevel();
        }
    }
}
