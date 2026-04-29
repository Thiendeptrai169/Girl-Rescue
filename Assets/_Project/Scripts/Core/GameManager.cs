using System;
using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Core
{
    /// <summary>
    /// Single source of truth for the current game state.
    /// Listens to GameEvents for win/lose — no duplicate local events.
    /// Fires OnGameStateChanged via GameEvents for UI/systems to react.
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        // ── Inspector ────────────────────────────────────────────────────────
        [Header("Level Progression")]
        [SerializeField] private LevelConfig[] _allLevels;

        // ── State ─────────────────────────────────────────────────────────────
        public GameState CurrentState { get; private set; } = GameState.Loading;
        public LevelConfig CurrentLevelConfig { get; private set; }

        private int _currentLevelIndex;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Start()
        {
            if (_allLevels == null || _allLevels.Length == 0)
            {
                Debug.LogError("[GameManager] No LevelConfig assets assigned!");
                return;
            }

            _currentLevelIndex = 0;
            StartLevel();
        }

        private void OnEnable()
        {
            GameEvents.OnLevelWin  += WinLevel;
            GameEvents.OnLevelLose += LoseLevel;
        }

        private void OnDisable()
        {
            GameEvents.OnLevelWin  -= WinLevel;
            GameEvents.OnLevelLose -= LoseLevel;
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void StartLevel()
        {
            CurrentLevelConfig = _allLevels[_currentLevelIndex];
            SetState(GameState.Playing);
            LevelManager.Instance.InitLevel(CurrentLevelConfig);
        }

        public void WinLevel()
        {
            if (CurrentState != GameState.Playing) return;
            SetState(GameState.Won);
        }

        public void LoseLevel()
        {
            if (CurrentState != GameState.Playing) return;
            SetState(GameState.Lost);
        }

        public void NextLevel()
        {
            _currentLevelIndex++;
            if (_currentLevelIndex >= _allLevels.Length)
            {
                Debug.Log("[GameManager] All levels completed — looping.");
                _currentLevelIndex = 0;
            }
            LevelManager.Instance.ClearLevel();
            StartLevel();
        }

        public void RetryLevel()
        {
            LevelManager.Instance.ClearLevel();
            StartLevel();
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
            GameEvents.FireGameStateChanged(newState);
        }

        // ── Debug ─────────────────────────────────────────────────────────────
        [ContextMenu("Debug / Force Win")]
        private void DebugForceWin() => GameEvents.FireLevelWin();

        [ContextMenu("Debug / Force Lose")]
        private void DebugForceLose() => GameEvents.FireLevelLose();

        [ContextMenu("Debug / Reload Level")]
        private void DebugReloadLevel() => RetryLevel();

        [ContextMenu("Debug / Next Level")]
        private void DebugNextLevel() => NextLevel();
    }
}
