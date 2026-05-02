using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DragonRescue.Core;
using DragonRescue.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DragonRescue.EditorScripts
{
    public class LevelEditorWindow : EditorWindow
    {
        private const string DefaultLevelFolder = "Assets/_Project/ScriptableObjects/Levels";
        private const string PlaytestLevelGuidKey = "DragonRescue.LevelEditor.PlaytestLevelGuid";
        private const string PlaytestExpectedHashKey = "DragonRescue.LevelEditor.ExpectedHash";

        private enum ToolMode
        {
            Paint,
            Erase,
            Select,
            Move
        }

        private enum ValidationSeverity
        {
            Error,
            Warning,
            Info
        }

        private readonly List<int> _selectedBlocks = new();
        private readonly List<ValidationMessage> _validationMessages = new();

        private LevelConfig _level;
        private SerializedObject _serializedLevel;
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private Vector2 _bottomScroll;
        private ToolMode _toolMode = ToolMode.Paint;
        private CannonColor _paintColor = CannonColor.Blue;
        private Direction _paintDirection = Direction.Right;
        private Vector2Int _paintSize = Vector2Int.one;
        private int _paintAmmo = 1;
        private int _batchAmmo = 1;
        private CannonColor _batchColor = CannonColor.Blue;
        private Direction _batchDirection = Direction.Right;
        private bool _advancedMode;
        private string _lastRoundTripResult = "Round-trip not run.";
        private double _lastValidationTime;

        [MenuItem("Tools/Level Editor")]
        public static void Open()
        {
            LevelEditorWindow window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(1120f, 720f);
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += Repaint;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            TryUseSelection();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnGUI()
        {
            DrawAssetBar();

            if (_level == null)
            {
                EditorGUILayout.HelpBox("Create or open a LevelConfig to begin editing.", MessageType.Info);
                return;
            }

            EnsureCollections();
            EnsureSerializedLevel();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMetadataPanel(GUILayout.Width(280f));
                DrawBoardPanel();
                DrawGameplayPanel(GUILayout.Width(330f));
            }

            DrawValidationPanel();
        }

        private void DrawAssetBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("New Level", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    CreateNewLevel("Level_New");

                if (GUILayout.Button("Open Selection", EditorStyles.toolbarButton, GUILayout.Width(105f)))
                    TryUseSelection();

                LevelConfig picked = (LevelConfig)EditorGUILayout.ObjectField(_level, typeof(LevelConfig), false, GUILayout.Width(250f));
                if (picked != _level)
                    SetLevel(picked);

                using (new EditorGUI.DisabledScope(_level == null))
                {
                    if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                        DuplicateLevel();

                    if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                        SaveLevel();

                    if (GUILayout.Button("Save As", EditorStyles.toolbarButton, GUILayout.Width(68f)))
                        SaveLevelAs();

                    if (GUILayout.Button("Play This Level", EditorStyles.toolbarButton, GUILayout.Width(112f)))
                        PlayThisLevel();
                }

                GUILayout.FlexibleSpace();

                _advancedMode = GUILayout.Toggle(_advancedMode, "Advanced", EditorStyles.toolbarButton, GUILayout.Width(78f));

                if (_level != null)
                {
                    string dirty = EditorUtility.IsDirty(_level) ? "Unsaved changes" : "Saved";
                    GUILayout.Label(dirty, EditorStyles.miniLabel, GUILayout.Width(110f));
                }
            }
        }

        private void DrawMetadataPanel(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, options))
            {
                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
                EditorGUILayout.LabelField("Level Metadata", EditorStyles.boldLabel);
                DrawProperty("levelId", "Level Id");
                DrawProperty("levelNumber", "Level Number");
                DrawProperty("tags", "Tags");
                DrawProperty("designerNotes", "Designer Notes");
                DrawProperty("changelog", "Changelog");

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Templates", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Easy")) ApplyTemplate(LevelTemplate.Easy);
                    if (GUILayout.Button("Medium")) ApplyTemplate(LevelTemplate.Medium);
                    if (GUILayout.Button("Hard")) ApplyTemplate(LevelTemplate.Hard);
                }

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Board Shape", EditorStyles.boldLabel);
                DrawProperty("boardSize", "Board Size");
                DrawProperty("boardViewport", "Viewport");
                DrawProperty("boardWidthRatio", "Width Ratio");
                DrawProperty("boardHeightRatio", "Height Ratio");

                if (_advancedMode)
                {
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField("Safe Zones", EditorStyles.boldLabel);
                    DrawProperty("slotBarViewportY", "Slot Bar Y");
                    DrawProperty("slotBarBottomPaddingViewport", "Slot Padding");
                    DrawProperty("boosterBarHeightPixels", "Booster Height");
                    DrawProperty("uiReferenceHeightPixels", "UI Ref Height");
                    DrawProperty("boosterBarTopPaddingViewport", "Booster Padding");
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawBoardPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawBoardToolbar();

                Rect canvasRect = GUILayoutUtility.GetRect(480f, 480f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawBoardCanvas(canvasRect);
            }
        }

        private void DrawBoardToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _toolMode = (ToolMode)GUILayout.Toolbar((int)_toolMode, Enum.GetNames(typeof(ToolMode)), GUILayout.Width(260f));
                GUILayout.Space(8f);
                _paintColor = (CannonColor)EditorGUILayout.EnumPopup(_paintColor, GUILayout.Width(95f));
                _paintDirection = (Direction)EditorGUILayout.EnumPopup(_paintDirection, GUILayout.Width(105f));
                _paintSize = EditorGUILayout.Vector2IntField(GUIContent.none, _paintSize, GUILayout.Width(120f));
                _paintAmmo = EditorGUILayout.IntField(_paintAmmo, GUILayout.Width(45f));
                _paintSize.x = Mathf.Max(1, _paintSize.x);
                _paintSize.y = Mathf.Max(1, _paintSize.y);
                _paintAmmo = Mathf.Max(0, _paintAmmo);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Selected: {_selectedBlocks.Count}", EditorStyles.miniLabel, GUILayout.Width(82f));
            }
        }

        private void DrawBoardCanvas(Rect canvasRect)
        {
            GUI.Box(canvasRect, GUIContent.none);

            Vector2Int boardSize = _level.boardSize;
            if (boardSize.x <= 0 || boardSize.y <= 0)
                return;

            float cell = Mathf.Min(canvasRect.width / boardSize.x, canvasRect.height / boardSize.y);
            Vector2 boardPixelSize = new Vector2(cell * boardSize.x, cell * boardSize.y);
            Rect boardRect = new Rect(
                canvasRect.x + (canvasRect.width - boardPixelSize.x) * 0.5f,
                canvasRect.y + (canvasRect.height - boardPixelSize.y) * 0.5f,
                boardPixelSize.x,
                boardPixelSize.y);

            EditorGUI.DrawRect(boardRect, new Color(0.1f, 0.11f, 0.12f));
            DrawGrid(boardRect, boardSize, cell);
            DrawBlocks(boardRect, cell);
            HandleBoardInput(boardRect, cell);
        }

        private void DrawGrid(Rect boardRect, Vector2Int boardSize, float cell)
        {
            Color oldColor = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.18f);

            for (int x = 0; x <= boardSize.x; x++)
            {
                float px = boardRect.x + x * cell;
                Handles.DrawLine(new Vector3(px, boardRect.y), new Vector3(px, boardRect.yMax));
            }

            for (int y = 0; y <= boardSize.y; y++)
            {
                float py = boardRect.y + y * cell;
                Handles.DrawLine(new Vector3(boardRect.x, py), new Vector3(boardRect.xMax, py));
            }

            Handles.color = oldColor;
        }

        private void DrawBlocks(Rect boardRect, float cell)
        {
            for (int i = 0; i < _level.blocks.Count; i++)
            {
                NormalArrowBlockData block = _level.blocks[i];
                if (block == null)
                    continue;

                Rect blockRect = new Rect(
                    boardRect.x + block.position.x * cell + 2f,
                    boardRect.y + block.position.y * cell + 2f,
                    block.size.x * cell - 4f,
                    block.size.y * cell - 4f);

                Color color = ColorPalette.GetColor(block.color);
                color.a = _selectedBlocks.Contains(i) ? 0.95f : 0.72f;
                EditorGUI.DrawRect(blockRect, color);

                Color border = _selectedBlocks.Contains(i) ? Color.white : new Color(0f, 0f, 0f, 0.55f);
                DrawRectOutline(blockRect, border, _selectedBlocks.Contains(i) ? 3f : 1f);

                string label = $"{DirectionGlyph(block.direction)}\n{block.color}\nA:{block.ammo}";
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    normal = { textColor = Color.white },
                    fontSize = Mathf.RoundToInt(Mathf.Clamp(cell * 0.18f, 9f, 13f))
                };
                GUI.Label(blockRect, label, style);
            }
        }

        private void HandleBoardInput(Rect boardRect, float cell)
        {
            Event current = Event.current;
            if (!boardRect.Contains(current.mousePosition) || current.type != EventType.MouseDown || current.button != 0)
                return;

            Vector2 local = current.mousePosition - boardRect.position;
            Vector2Int cellPos = new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(local.x / cell), 0, _level.boardSize.x - 1),
                Mathf.Clamp(Mathf.FloorToInt(local.y / cell), 0, _level.boardSize.y - 1));

            int hitIndex = FindBlockAt(cellPos);

            switch (_toolMode)
            {
                case ToolMode.Paint:
                    if (hitIndex >= 0)
                    {
                        SelectBlock(hitIndex, current.shift);
                    }
                    else
                    {
                        AddBlock(cellPos);
                    }
                    break;
                case ToolMode.Erase:
                    if (hitIndex >= 0)
                        RemoveBlock(hitIndex);
                    break;
                case ToolMode.Select:
                    if (hitIndex >= 0)
                        SelectBlock(hitIndex, current.shift);
                    else if (!current.shift)
                        _selectedBlocks.Clear();
                    break;
                case ToolMode.Move:
                    if (hitIndex >= 0)
                    {
                        SelectBlock(hitIndex, current.shift);
                    }
                    else if (_selectedBlocks.Count > 0)
                    {
                        MoveSelectedTo(cellPos);
                    }
                    break;
            }

            current.Use();
            Repaint();
        }

        private void DrawGameplayPanel(params GUILayoutOption[] options)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, options))
            {
                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

                EditorGUILayout.LabelField("Selected Blocks", EditorStyles.boldLabel);
                DrawSelectedBlockTools();

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Princess", EditorStyles.boldLabel);
                DrawProperty("princessViewport", "Viewport");
                DrawProperty("princessHearts", "Hearts");

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Dragon", EditorStyles.boldLabel);
                DrawProperty("dragonMovementType", "Movement");
                DrawProperty("dragonSpawnViewport", "Spawn");
                if (_level.dragonMovementType == DragonMovementType.Linear)
                {
                    DrawProperty("dragonStartViewport", "Start");
                    DrawProperty("dragonEndViewport", "End");
                }
                else
                {
                    DrawProperty("dragonPathWaypointsViewport", "Waypoints");
                }
                DrawProperty("dragonMoveSpeed", "Speed");
                DrawProperty("loseDistance", "Lose Distance");
                DrawProperty("dragonSegments", "Segments");

                if (_advancedMode)
                {
                    DrawProperty("dragonRecoilProgress", "Recoil");
                    DrawProperty("dragonRecoilPauseSeconds", "Recoil Pause");
                }

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Slots & Cannon", EditorStyles.boldLabel);
                DrawProperty("totalSlotCount", "Total Slots");
                DrawProperty("unlockedSlotCount", "Unlocked");
                DrawProperty("defaultFireRate", "Fire Rate");
                DrawProperty("defaultDamage", "Damage");
                DrawProperty("defaultProjectileSpeed", "Projectile Speed");
                DrawProperty("defaultFireRange", "Fire Range");

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Boosters", EditorStyles.boldLabel);
                DrawProperty("boosters", "Boosters");

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSelectedBlockTools()
        {
            if (_selectedBlocks.Count == 0)
            {
                EditorGUILayout.HelpBox("Select blocks on the board to edit or batch modify them.", MessageType.None);
                return;
            }

            int firstIndex = _selectedBlocks[0];
            if (!IsValidBlockIndex(firstIndex))
                return;

            if (_selectedBlocks.Count == 1)
            {
                NormalArrowBlockData block = _level.blocks[firstIndex];
                EditorGUI.BeginChangeCheck();
                string id = EditorGUILayout.TextField("Id", block.id);
                Vector2Int position = EditorGUILayout.Vector2IntField("Position", block.position);
                Vector2Int size = EditorGUILayout.Vector2IntField("Size", block.size);
                CannonColor color = (CannonColor)EditorGUILayout.EnumPopup("Color", block.color);
                Direction direction = (Direction)EditorGUILayout.EnumPopup("Direction", block.direction);
                int ammo = EditorGUILayout.IntField("Ammo", block.ammo);

                if (EditorGUI.EndChangeCheck())
                {
                    Record("Edit Block");
                    block.id = id;
                    block.position = position;
                    block.size = size;
                    block.color = color;
                    block.direction = direction;
                    block.ammo = ammo;
                    EnsureDragonColor(color, Mathf.Max(1, ammo));
                    NormalizeAndValidate();
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Batch", EditorStyles.miniBoldLabel);
            _batchColor = (CannonColor)EditorGUILayout.EnumPopup("Color", _batchColor);
            _batchDirection = (Direction)EditorGUILayout.EnumPopup("Direction", _batchDirection);
            _batchAmmo = Mathf.Max(0, EditorGUILayout.IntField("Ammo", _batchAmmo));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Color")) ApplyBatch(BlockBatchField.Color);
                if (GUILayout.Button("Apply Direction")) ApplyBatch(BlockBatchField.Direction);
                if (GUILayout.Button("Apply Ammo")) ApplyBatch(BlockBatchField.Ammo);
            }

            if (GUILayout.Button("Delete Selected"))
                DeleteSelectedBlocks();
        }

        private void DrawValidationPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(150f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Validation & Simulation", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Validate", GUILayout.Width(80f)))
                        ValidateNow();
                    if (GUILayout.Button("Auto-Fix", GUILayout.Width(80f)))
                        AutoFix();
                    if (GUILayout.Button("Round-Trip Test", GUILayout.Width(120f)))
                        RunRoundTripTest();
                }

                _bottomScroll = EditorGUILayout.BeginScrollView(_bottomScroll);
                EditorGUILayout.LabelField(_lastRoundTripResult, EditorStyles.miniLabel);

                if (_validationMessages.Count == 0)
                    EditorGUILayout.HelpBox("No validation issues found.", MessageType.Info);

                foreach (ValidationMessage message in _validationMessages)
                {
                    MessageType type = message.Severity == ValidationSeverity.Error
                        ? MessageType.Error
                        : message.Severity == ValidationSeverity.Warning ? MessageType.Warning : MessageType.Info;
                    EditorGUILayout.HelpBox(message.Text, type);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawProperty(string propertyName, string label)
        {
            SerializedProperty property = _serializedLevel.FindProperty(propertyName);
            if (property == null)
                return;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            if (EditorGUI.EndChangeCheck())
            {
                _serializedLevel.ApplyModifiedProperties();
                NormalizeAndValidate();
            }
        }

        private void AddBlock(Vector2Int position)
        {
            Record("Paint Block");
            EnsureDragonColor(_paintColor, Mathf.Max(1, _paintAmmo));
            _level.blocks.Add(new NormalArrowBlockData
            {
                id = $"Block_{_level.blocks.Count + 1:00}",
                position = position,
                size = _paintSize,
                color = _paintColor,
                direction = _paintDirection,
                ammo = _paintAmmo
            });
            _selectedBlocks.Clear();
            _selectedBlocks.Add(_level.blocks.Count - 1);
            NormalizeAndValidate();
        }

        private void RemoveBlock(int index)
        {
            if (!IsValidBlockIndex(index))
                return;

            Record("Erase Block");
            _level.blocks.RemoveAt(index);
            _selectedBlocks.Clear();
            NormalizeAndValidate();
        }

        private void MoveSelectedTo(Vector2Int position)
        {
            if (_selectedBlocks.Count == 0)
                return;

            int first = _selectedBlocks[0];
            if (!IsValidBlockIndex(first))
                return;

            Vector2Int delta = position - _level.blocks[first].position;
            Record("Move Blocks");

            foreach (int index in _selectedBlocks)
            {
                if (IsValidBlockIndex(index))
                    _level.blocks[index].position += delta;
            }

            NormalizeAndValidate();
        }

        private void SelectBlock(int index, bool additive)
        {
            if (!IsValidBlockIndex(index))
                return;

            if (!additive)
                _selectedBlocks.Clear();

            if (_selectedBlocks.Contains(index))
            {
                if (additive)
                    _selectedBlocks.Remove(index);
            }
            else
            {
                _selectedBlocks.Add(index);
            }
        }

        private void DeleteSelectedBlocks()
        {
            Record("Delete Blocks");
            _selectedBlocks.Sort();
            for (int i = _selectedBlocks.Count - 1; i >= 0; i--)
            {
                int index = _selectedBlocks[i];
                if (IsValidBlockIndex(index))
                    _level.blocks.RemoveAt(index);
            }
            _selectedBlocks.Clear();
            NormalizeAndValidate();
        }

        private void ApplyBatch(BlockBatchField field)
        {
            Record("Batch Edit Blocks");
            foreach (int index in _selectedBlocks)
            {
                if (!IsValidBlockIndex(index))
                    continue;

                NormalArrowBlockData block = _level.blocks[index];
                if (field == BlockBatchField.Color)
                {
                    block.color = _batchColor;
                    EnsureDragonColor(_batchColor, Mathf.Max(1, block.ammo));
                }
                else if (field == BlockBatchField.Direction)
                {
                    block.direction = _batchDirection;
                }
                else if (field == BlockBatchField.Ammo)
                {
                    block.ammo = _batchAmmo;
                    EnsureDragonColor(block.color, Mathf.Max(1, _batchAmmo));
                }
            }
            NormalizeAndValidate();
        }

        private void ApplyTemplate(LevelTemplate template)
        {
            Record($"Apply {template} Template");

            _level.blocks.Clear();
            _level.dragonSegments.Clear();
            _level.boosters.Clear();

            switch (template)
            {
                case LevelTemplate.Easy:
                    _level.boardSize = new Vector2Int(8, 4);
                    _level.totalSlotCount = 6;
                    _level.unlockedSlotCount = 4;
                    AddSegment(CannonColor.Blue, 4);
                    AddSegment(CannonColor.Green, 3);
                    AddTemplateBlock("B01", 0, 0, 2, 1, CannonColor.Blue, Direction.Right, 2);
                    AddTemplateBlock("G01", 2, 1, 1, 2, CannonColor.Green, Direction.Down, 2);
                    AddTemplateBlock("B02", 5, 2, 2, 1, CannonColor.Blue, Direction.Left, 2);
                    AddTemplateBlock("G02", 6, 0, 1, 1, CannonColor.Green, Direction.Right, 1);
                    break;
                case LevelTemplate.Medium:
                    _level.boardSize = new Vector2Int(10, 6);
                    _level.totalSlotCount = 6;
                    _level.unlockedSlotCount = 3;
                    AddSegment(CannonColor.Blue, 4);
                    AddSegment(CannonColor.Green, 4);
                    AddSegment(CannonColor.Red, 3);
                    AddTemplateBlock("B01", 0, 1, 2, 1, CannonColor.Blue, Direction.Right, 2);
                    AddTemplateBlock("G01", 3, 0, 1, 2, CannonColor.Green, Direction.Down, 2);
                    AddTemplateBlock("R01", 5, 1, 2, 1, CannonColor.Red, Direction.Left, 2);
                    AddTemplateBlock("B02", 1, 4, 1, 2, CannonColor.Blue, Direction.Up, 2);
                    AddTemplateBlock("G02", 7, 3, 2, 1, CannonColor.Green, Direction.Right, 2);
                    AddTemplateBlock("R02", 8, 5, 1, 1, CannonColor.Red, Direction.UpLeft, 1);
                    break;
                case LevelTemplate.Hard:
                    _level.boardSize = new Vector2Int(12, 8);
                    _level.totalSlotCount = 7;
                    _level.unlockedSlotCount = 3;
                    AddSegment(CannonColor.Blue, 5);
                    AddSegment(CannonColor.Green, 4);
                    AddSegment(CannonColor.Red, 4);
                    AddSegment(CannonColor.Yellow, 4);
                    AddTemplateBlock("B01", 0, 0, 2, 1, CannonColor.Blue, Direction.Right, 2);
                    AddTemplateBlock("G01", 2, 2, 1, 2, CannonColor.Green, Direction.DownRight, 2);
                    AddTemplateBlock("R01", 5, 0, 3, 1, CannonColor.Red, Direction.Left, 3);
                    AddTemplateBlock("Y01", 8, 2, 1, 3, CannonColor.Yellow, Direction.Down, 2);
                    AddTemplateBlock("B02", 1, 5, 2, 1, CannonColor.Blue, Direction.Up, 3);
                    AddTemplateBlock("G02", 4, 5, 2, 1, CannonColor.Green, Direction.Right, 2);
                    AddTemplateBlock("R02", 7, 6, 2, 1, CannonColor.Red, Direction.UpLeft, 1);
                    AddTemplateBlock("Y02", 10, 5, 1, 2, CannonColor.Yellow, Direction.Left, 2);
                    break;
            }

            _selectedBlocks.Clear();
            NormalizeAndValidate();
        }

        private void AddTemplateBlock(string id, int x, int y, int width, int height, CannonColor color, Direction direction, int ammo)
        {
            _level.blocks.Add(new NormalArrowBlockData
            {
                id = id,
                position = new Vector2Int(x, y),
                size = new Vector2Int(width, height),
                color = color,
                direction = direction,
                ammo = ammo
            });
        }

        private void AddSegment(CannonColor color, int count)
        {
            _level.dragonSegments.Add(new DragonSegmentData { color = color, count = count });
        }

        private void EnsureDragonColor(CannonColor color, int minimumCount)
        {
            if (_level.dragonSegments == null)
                _level.dragonSegments = new List<DragonSegmentData>();

            for (int i = 0; i < _level.dragonSegments.Count; i++)
            {
                DragonSegmentData segment = _level.dragonSegments[i];
                if (segment != null && segment.color == color)
                {
                    segment.count = Mathf.Max(segment.count, minimumCount);
                    return;
                }
            }

            _level.dragonSegments.Add(new DragonSegmentData { color = color, count = Mathf.Max(1, minimumCount) });
        }

        private void ValidateNow()
        {
            _validationMessages.Clear();

            if (_level == null)
                return;

            EnsureCollections();
            ValidateStructuralRules();
            ValidateGameplayRules();
            _lastValidationTime = EditorApplication.timeSinceStartup;
        }

        private void ValidateStructuralRules()
        {
            if (string.IsNullOrWhiteSpace(_level.levelId))
                _validationMessages.Add(ValidationMessage.Warning("Level Id is empty."));

            if (_level.boardSize.x <= 0 || _level.boardSize.y <= 0)
                _validationMessages.Add(ValidationMessage.Error("Board size must be at least 1x1."));

            HashSet<Vector2Int> occupied = new();
            for (int i = 0; i < _level.blocks.Count; i++)
            {
                NormalArrowBlockData block = _level.blocks[i];
                if (block == null)
                {
                    _validationMessages.Add(ValidationMessage.Warning($"Block {i} is null."));
                    continue;
                }

                if (block.ammo < 0)
                    _validationMessages.Add(ValidationMessage.Error($"{BlockLabel(block, i)} has negative ammo."));

                if (block.size.x <= 0 || block.size.y <= 0)
                    _validationMessages.Add(ValidationMessage.Error($"{BlockLabel(block, i)} has invalid size."));

                if (block.position.x < 0 || block.position.y < 0 ||
                    block.position.x + block.size.x > _level.boardSize.x ||
                    block.position.y + block.size.y > _level.boardSize.y)
                {
                    _validationMessages.Add(ValidationMessage.Error($"{BlockLabel(block, i)} is outside the board."));
                }

                for (int x = 0; x < Mathf.Max(1, block.size.x); x++)
                {
                    for (int y = 0; y < Mathf.Max(1, block.size.y); y++)
                    {
                        Vector2Int cell = new(block.position.x + x, block.position.y + y);
                        if (!occupied.Add(cell))
                            _validationMessages.Add(ValidationMessage.Error($"{BlockLabel(block, i)} overlaps another block at {cell}."));
                    }
                }
            }

            if (_level.dragonMovementType == DragonMovementType.Waypoint &&
                (_level.dragonPathWaypointsViewport == null || _level.dragonPathWaypointsViewport.Length < 2))
            {
                _validationMessages.Add(ValidationMessage.Error("Waypoint dragon movement needs at least 2 waypoints."));
            }
        }

        private void ValidateGameplayRules()
        {
            Dictionary<CannonColor, int> dragonCounts = BuildDragonCounts();
            Dictionary<CannonColor, int> ammoCounts = new();

            foreach (NormalArrowBlockData block in _level.blocks)
            {
                if (block == null)
                    continue;

                if (!ammoCounts.ContainsKey(block.color))
                    ammoCounts[block.color] = 0;
                ammoCounts[block.color] += Mathf.Max(0, block.ammo);

                if (!dragonCounts.ContainsKey(block.color))
                    _validationMessages.Add(ValidationMessage.Error($"Board has {block.color} ammo but dragon has no matching segment."));
            }

            foreach (KeyValuePair<CannonColor, int> pair in dragonCounts)
            {
                ammoCounts.TryGetValue(pair.Key, out int ammo);
                if (ammo < pair.Value)
                    _validationMessages.Add(ValidationMessage.Warning($"{pair.Key} ammo {ammo} is below dragon segment count {pair.Value}."));
                else if (ammo == pair.Value)
                    _validationMessages.Add(ValidationMessage.Info($"{pair.Key} ammo exactly matches dragon segment count."));
            }

            int totalAmmo = 0;
            foreach (NormalArrowBlockData block in _level.blocks)
                totalAmmo += block != null ? Mathf.Max(0, block.ammo) : 0;

            int totalSegments = 0;
            foreach (DragonSegmentData segment in _level.dragonSegments)
                totalSegments += segment != null ? Mathf.Max(1, segment.count) : 0;

            if (totalSegments > 0 && totalAmmo <= totalSegments + 1)
                _validationMessages.Add(ValidationMessage.Warning("Total ammo is very close to required dragon hits; difficulty may spike."));

            if (_level.boardHeightRatio > 0.55f)
                _validationMessages.Add(ValidationMessage.Warning("Board is tall; verify it stays clear of UI safe zones on device."));
        }

        private void AutoFix()
        {
            Record("Auto-Fix Level");
            _level.EditorNormalize();
            EditorUtility.SetDirty(_level);
            _serializedLevel?.Update();
            ValidateNow();
            Repaint();
        }

        private void RunRoundTripTest()
        {
            if (_level == null)
                return;

            SaveLevel();
            string before = LevelConfigFingerprint.Build(_level);
            string path = AssetDatabase.GetAssetPath(_level);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            LevelConfig reloaded = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
            string after = LevelConfigFingerprint.Build(reloaded);

            bool passed = before == after;
            _lastRoundTripResult = passed
                ? $"Round-trip passed. Hash: {before}"
                : $"Round-trip failed. Before: {before} After: {after}";

            if (!passed)
                _validationMessages.Add(ValidationMessage.Error(_lastRoundTripResult));
        }

        private void PlayThisLevel()
        {
            if (_level == null)
                return;

            SaveLevel();
            ValidateNow();

            bool hasError = _validationMessages.Exists(message => message.Severity == ValidationSeverity.Error);
            if (hasError && !EditorUtility.DisplayDialog("Play Level With Errors?", "Validation found hard errors. Play anyway?", "Play", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string path = AssetDatabase.GetAssetPath(_level);
            string guid = AssetDatabase.AssetPathToGUID(path);
            SessionState.SetString(PlaytestLevelGuidKey, guid);
            SessionState.SetString(PlaytestExpectedHashKey, LevelConfigFingerprint.Build(_level));

            EditorApplication.isPlaying = true;
        }

        private void SaveLevel()
        {
            if (_level == null)
                return;

            _serializedLevel?.ApplyModifiedProperties();
            _level.EditorNormalize();
            EditorUtility.SetDirty(_level);
            _serializedLevel?.Update();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateNow();
        }

        private void SaveLevelAs()
        {
            if (_level == null)
                return;

            string sourcePath = AssetDatabase.GetAssetPath(_level);
            string folder = string.IsNullOrWhiteSpace(sourcePath) ? DefaultLevelFolder : Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            string target = EditorUtility.SaveFilePanelInProject("Save Level As", $"{_level.name}_Copy", "asset", "Choose a LevelConfig asset path.", folder);
            if (string.IsNullOrWhiteSpace(target))
                return;

            AssetDatabase.CopyAsset(sourcePath, target);
            AssetDatabase.SaveAssets();
            SetLevel(AssetDatabase.LoadAssetAtPath<LevelConfig>(target));
        }

        private void CreateNewLevel(string baseName)
        {
            EnsureDefaultFolder();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultLevelFolder}/{baseName}.asset");
            LevelConfig config = CreateInstance<LevelConfig>();
            config.levelId = Path.GetFileNameWithoutExtension(path);
            config.levelNumber = FindNextLevelNumber();
            config.dragonSegments.Add(new DragonSegmentData { color = CannonColor.Blue, count = 4 });
            config.blocks.Add(new NormalArrowBlockData
            {
                id = "Block_01",
                position = Vector2Int.zero,
                size = Vector2Int.one,
                color = CannonColor.Blue,
                direction = Direction.Right,
                ammo = 1
            });
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            SetLevel(config);
        }

        private void DuplicateLevel()
        {
            if (_level == null)
                return;

            string sourcePath = AssetDatabase.GetAssetPath(_level);
            string folder = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            string copyPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{_level.name}_v2.asset");
            AssetDatabase.CopyAsset(sourcePath, copyPath);
            AssetDatabase.SaveAssets();
            SetLevel(AssetDatabase.LoadAssetAtPath<LevelConfig>(copyPath));
        }

        private void SetLevel(LevelConfig level)
        {
            _level = level;
            EnsureCollections();
            _serializedLevel = level != null ? new SerializedObject(level) : null;
            _selectedBlocks.Clear();
            ValidateNow();
            Repaint();
        }

        private void TryUseSelection()
        {
            if (Selection.activeObject is LevelConfig config)
                SetLevel(config);
        }

        private void EnsureSerializedLevel()
        {
            if (_serializedLevel == null || _serializedLevel.targetObject != _level)
                _serializedLevel = new SerializedObject(_level);

            _serializedLevel.UpdateIfRequiredOrScript();
        }

        private void NormalizeAndValidate()
        {
            EnsureCollections();
            _level.EditorNormalize();
            EditorUtility.SetDirty(_level);
            _serializedLevel?.Update();

            if (EditorApplication.timeSinceStartup - _lastValidationTime > 0.2f)
                ValidateNow();
        }

        private void EnsureCollections()
        {
            if (_level == null)
                return;

            _level.blocks ??= new List<NormalArrowBlockData>();
            _level.dragonSegments ??= new List<DragonSegmentData>();
            _level.boosters ??= new List<BoosterData>();
        }

        private void Record(string label)
        {
            Undo.RecordObject(_level, label);
        }

        private int FindBlockAt(Vector2Int cell)
        {
            for (int i = _level.blocks.Count - 1; i >= 0; i--)
            {
                NormalArrowBlockData block = _level.blocks[i];
                if (block == null)
                    continue;

                if (cell.x >= block.position.x && cell.x < block.position.x + block.size.x &&
                    cell.y >= block.position.y && cell.y < block.position.y + block.size.y)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsValidBlockIndex(int index)
        {
            return index >= 0 && index < _level.blocks.Count;
        }

        private Dictionary<CannonColor, int> BuildDragonCounts()
        {
            Dictionary<CannonColor, int> counts = new();
            foreach (DragonSegmentData segment in _level.dragonSegments)
            {
                if (segment == null)
                    continue;

                if (!counts.ContainsKey(segment.color))
                    counts[segment.color] = 0;
                counts[segment.color] += Mathf.Max(1, segment.count);
            }

            return counts;
        }

        private static void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static string DirectionGlyph(Direction direction)
        {
            return direction switch
            {
                Direction.Up => "^",
                Direction.Down => "v",
                Direction.Left => "<",
                Direction.Right => ">",
                Direction.UpLeft => "<^",
                Direction.UpRight => "^>",
                Direction.DownLeft => "<v",
                Direction.DownRight => "v>",
                _ => "?"
            };
        }

        private static string BlockLabel(NormalArrowBlockData block, int index)
        {
            return !string.IsNullOrWhiteSpace(block.id) ? block.id : $"Block {index}";
        }

        private static void EnsureDefaultFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets/_Project", "ScriptableObjects");

            if (!AssetDatabase.IsValidFolder(DefaultLevelFolder))
                AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects", "Levels");
        }

        private static int FindNextLevelNumber()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelConfig", new[] { DefaultLevelFolder });
            int max = 0;
            foreach (string guid in guids)
            {
                LevelConfig config = AssetDatabase.LoadAssetAtPath<LevelConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (config != null)
                    max = Mathf.Max(max, config.levelNumber);
            }

            return max + 1;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetString(PlaytestLevelGuidKey, string.Empty);
                SessionState.SetString(PlaytestExpectedHashKey, string.Empty);
            }
        }

        private enum BlockBatchField
        {
            Color,
            Direction,
            Ammo
        }

        private enum LevelTemplate
        {
            Easy,
            Medium,
            Hard
        }

        private readonly struct ValidationMessage
        {
            public readonly ValidationSeverity Severity;
            public readonly string Text;

            private ValidationMessage(ValidationSeverity severity, string text)
            {
                Severity = severity;
                Text = text;
            }

            public static ValidationMessage Error(string text) => new(ValidationSeverity.Error, text);
            public static ValidationMessage Warning(string text) => new(ValidationSeverity.Warning, text);
            public static ValidationMessage Info(string text) => new(ValidationSeverity.Info, text);
        }

        internal static class LevelConfigFingerprint
        {
            public static string Build(LevelConfig config)
            {
                if (config == null)
                    return "null";

                StringBuilder builder = new();
                builder.Append(config.levelId).Append('|');
                builder.Append(config.levelNumber).Append('|');
                builder.Append(config.princessViewport).Append('|');
                builder.Append(config.princessHearts).Append('|');
                builder.Append(config.dragonMovementType).Append('|');
                builder.Append(config.dragonSpawnViewport).Append('|');
                builder.Append(config.dragonStartViewport).Append('|');
                builder.Append(config.dragonEndViewport).Append('|');
                builder.Append(config.dragonMoveSpeed).Append('|');
                builder.Append(config.loseDistance).Append('|');
                builder.Append(config.totalSlotCount).Append('|');
                builder.Append(config.unlockedSlotCount).Append('|');
                builder.Append(config.boardViewport).Append('|');
                builder.Append(config.boardWidthRatio).Append('|');
                builder.Append(config.boardHeightRatio).Append('|');
                builder.Append(config.boardSize).Append('|');
                builder.Append(config.defaultFireRate).Append('|');
                builder.Append(config.defaultDamage).Append('|');
                builder.Append(config.defaultProjectileSpeed).Append('|');
                builder.Append(config.defaultFireRange).Append('|');

                AppendWaypoints(builder, config.dragonPathWaypointsViewport);
                AppendSegments(builder, config.dragonSegments);
                AppendBlocks(builder, config.blocks);
                AppendBoosters(builder, config.boosters);

                return Hash(builder.ToString());
            }

            private static void AppendWaypoints(StringBuilder builder, Vector2[] waypoints)
            {
                if (waypoints == null)
                {
                    builder.Append("waypoints:null|");
                    return;
                }

                builder.Append("waypoints:");
                for (int i = 0; i < waypoints.Length; i++)
                    builder.Append(waypoints[i]).Append(',');
                builder.Append('|');
            }

            private static void AppendSegments(StringBuilder builder, List<DragonSegmentData> segments)
            {
                builder.Append("segments:");
                if (segments != null)
                {
                    foreach (DragonSegmentData segment in segments)
                    {
                        if (segment == null)
                            continue;
                        builder.Append(segment.color).Append(':').Append(segment.count).Append(',');
                    }
                }
                builder.Append('|');
            }

            private static void AppendBlocks(StringBuilder builder, List<NormalArrowBlockData> blocks)
            {
                builder.Append("blocks:");
                if (blocks != null)
                {
                    foreach (NormalArrowBlockData block in blocks)
                    {
                        if (block == null)
                            continue;
                        builder.Append(block.id).Append(':')
                            .Append(block.position).Append(':')
                            .Append(block.size).Append(':')
                            .Append(block.color).Append(':')
                            .Append(block.direction).Append(':')
                            .Append(block.ammo).Append(',');
                    }
                }
                builder.Append('|');
            }

            private static void AppendBoosters(StringBuilder builder, List<BoosterData> boosters)
            {
                builder.Append("boosters:");
                if (boosters != null)
                {
                    foreach (BoosterData booster in boosters)
                    {
                        if (booster == null)
                            continue;
                        builder.Append(booster.type).Append(':')
                            .Append(booster.charges).Append(':')
                            .Append(booster.enabled).Append(':')
                            .Append(booster.amount).Append(':')
                            .Append(booster.duration).Append(':')
                            .Append(booster.multiplier).Append(',');
                    }
                }
            }

            private static string Hash(string value)
            {
                unchecked
                {
                    uint hash = 2166136261;
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619;
                    }

                    return hash.ToString("X8");
                }
            }
        }
    }

    [InitializeOnLoad]
    internal static class LevelEditorPlaytestMonitor
    {
        private const string PlaytestExpectedHashKey = "DragonRescue.LevelEditor.ExpectedHash";
        private static bool _waitingForRuntimeLevel;

        static LevelEditorPlaytestMonitor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode &&
                !string.IsNullOrWhiteSpace(SessionState.GetString(PlaytestExpectedHashKey, string.Empty)))
            {
                _waitingForRuntimeLevel = true;
                EditorApplication.update += CheckRuntimeLevel;
            }

            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                _waitingForRuntimeLevel = false;
                EditorApplication.update -= CheckRuntimeLevel;
                SessionState.SetString(PlaytestExpectedHashKey, string.Empty);
            }
        }

        private static void CheckRuntimeLevel()
        {
            if (!_waitingForRuntimeLevel || GameManager.Instance == null || GameManager.Instance.CurrentLevelConfig == null)
                return;

            _waitingForRuntimeLevel = false;
            EditorApplication.update -= CheckRuntimeLevel;

            string expected = SessionState.GetString(PlaytestExpectedHashKey, string.Empty);
            string actual = LevelEditorWindow.LevelConfigFingerprint.Build(GameManager.Instance.CurrentLevelConfig);
            SessionState.SetString(PlaytestExpectedHashKey, string.Empty);

            if (expected == actual)
                Debug.Log($"Level Editor runtime round-trip passed. Hash: {actual}");
            else
                Debug.LogError($"Level Editor runtime round-trip failed. Expected: {expected} Actual: {actual}");
        }
    }
}
