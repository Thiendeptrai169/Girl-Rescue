using UnityEngine;
using UnityEngine.SceneManagement;
using DragonRescue.Data;
using DragonRescue.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        [SerializeField] private ResultPopupView _resultPopup;
        [SerializeField] private string _homeSceneName;

        // ── State ─────────────────────────────────────────────────────────────
        public GameState CurrentState { get; private set; } = GameState.Loading;
        public LevelConfig CurrentLevelConfig { get; private set; }

        private int _currentLevelIndex;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Start()
        {
            if (_resultPopup != null)
                _resultPopup.Init(HomeLevel);

#if UNITY_EDITOR
            LevelConfig playtestOverride = ConsumeEditorPlaytestOverride();
            if (playtestOverride != null)
            {
                CurrentLevelConfig = playtestOverride;
                _currentLevelIndex = -1;
                DebugSystem.Log(DebugCategory.Game, $"Start editor playtest levelNumber={CurrentLevelConfig.levelNumber} id={CurrentLevelConfig.levelId}", this);
                HideResultScreen();
                SetState(GameState.Playing);
                LevelManager.Instance.InitLevel(CurrentLevelConfig);
                return;
            }
#endif

            if (_allLevels == null || _allLevels.Length == 0)
            {
                DebugSystem.Error(DebugCategory.Game, "No LevelConfig assets assigned!", this);
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
#if UNITY_EDITOR
            if (_currentLevelIndex < 0 && CurrentLevelConfig != null)
            {
                DebugSystem.Log(DebugCategory.Game, $"Restart editor playtest levelNumber={CurrentLevelConfig.levelNumber} id={CurrentLevelConfig.levelId}", this);
                HideResultScreen();
                SetState(GameState.Playing);
                LevelManager.Instance.InitLevel(CurrentLevelConfig);
                return;
            }
#endif

            CurrentLevelConfig = _allLevels[_currentLevelIndex];
            DebugSystem.Log(DebugCategory.Game, $"StartLevel index={_currentLevelIndex} levelNumber={CurrentLevelConfig.levelNumber} id={CurrentLevelConfig.levelId}", this);
            HideResultScreen();
            SetState(GameState.Playing);
            LevelManager.Instance.InitLevel(CurrentLevelConfig);
        }

        public void WinLevel()
        {
            if (CurrentState != GameState.Playing) return;
            DebugSystem.Log(DebugCategory.Game, $"WinLevel index={_currentLevelIndex}", this);
            SetState(GameState.Won);
            ShowResultScreen(true);
        }

        public void LoseLevel()
        {
            if (CurrentState != GameState.Playing) return;
            DebugSystem.Log(DebugCategory.Game, $"LoseLevel index={_currentLevelIndex}", this);
            SetState(GameState.Lost);
            ShowResultScreen(false);
        }

        public void NextLevel()
        {
#if UNITY_EDITOR
            if (_currentLevelIndex < 0)
            {
                DebugSystem.Log(DebugCategory.Game, "Editor playtest level completed. Reloading current scene instead of advancing production level list.", this);
                HomeLevel();
                return;
            }
#endif

            int previousIndex = _currentLevelIndex;
            _currentLevelIndex++;
            if (_currentLevelIndex >= _allLevels.Length)
            {
                DebugSystem.Log(DebugCategory.Game, "All levels completed — looping.", this);
                _currentLevelIndex = 0;
            }
            DebugSystem.Log(DebugCategory.Game, $"NextLevel previousIndex={previousIndex} nextIndex={_currentLevelIndex}", this);
            LevelManager.Instance.ClearLevel();
            StartLevel();
        }

        public void HomeLevel()
        {
            if (!string.IsNullOrWhiteSpace(_homeSceneName))
            {
                SceneManager.LoadScene(_homeSceneName);
                return;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void RetryLevel()
        {
            DebugSystem.Log(DebugCategory.Game, $"RetryLevel index={_currentLevelIndex}", this);
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
            DebugSystem.Log(DebugCategory.Game, $"State → {newState}", this);
            GameEvents.FireGameStateChanged(newState);
        }

        private void ShowResultScreen(bool won)
        {
            if (_resultPopup == null)
            {
                DebugSystem.Warning(DebugCategory.Game, "Cannot show result screen: no ResultPopupView assigned.", this);
                return;
            }

            if (won)
                _resultPopup.ShowWin(NextLevel);
            else
                _resultPopup.ShowLose(RetryLevel);
        }

        private void HideResultScreen()
        {
            if (_resultPopup != null)
            {
                DebugSystem.Log(DebugCategory.UI, "Hide result screen.", this);
                _resultPopup.Hide();
            }
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

#if UNITY_EDITOR
        private const string EditorPlaytestLevelGuidKey = "DragonRescue.LevelEditor.PlaytestLevelGuid";

        private static LevelConfig ConsumeEditorPlaytestOverride()
        {
            string guid = SessionState.GetString(EditorPlaytestLevelGuidKey, string.Empty);
            SessionState.SetString(EditorPlaytestLevelGuidKey, string.Empty);

            if (string.IsNullOrWhiteSpace(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path))
                return null;

            return AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
        }
#endif
    }
}
