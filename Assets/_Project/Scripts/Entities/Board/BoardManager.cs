using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DragonRescue.Data;
using DragonRescue.Core;
using DragonRescue.Entities.Cannon;
using DragonRescue.Entities.Dragon;
using DragonRescue.Booster;
using DragonRescue.Animation;

namespace DragonRescue.Entities.Board
{
    public class BoardManager : MonoBehaviour
    {
        private enum BoardInputState
        {
            Ready,
            ResolvingMove,
            BoosterSelecting,
            LevelEnding
        }

        private enum BoardEdgeSide
        {
            Top,
            Right,
            Bottom,
            Left
        }

        [SerializeField] private GameObject _blockPrefab;
        [SerializeField] private BoardInputReceiver _inputReceiver;
        
        public static BoardManager ActiveInstance { get; private set; }

        private BlockIdentity[,] _grid;
        private BoardWorldLayout _layout;
        private Camera _mainCamera;
        private Vector2Int _boardSize;
        private bool _acceptsInput;
        private BoardInputState _boardState = BoardInputState.Ready;
        private float _nextInputAllowedTime;
        private float _transactionStartTime = -999f;
        private float _lastInputGateLogTime = -999f;
        private float _lastBoardHeartbeatTime = -999f;
        private float _lastIntegrityLogTime = -999f;
        private float _lastMovementTraceLogTime = -999f;
        private float _lastSlotFullFeedbackTime = -999f;
        private float _lastSlotFullLogTime = -999f;
        private const float InputDebounceSeconds = 0.07f;
        private const float MaxInputDebounceSeconds = 0.25f;
        private const float StuckTransactionSeconds = 1f;
        private const float InputGateLogCooldown = 0.5f;
        private const float BoardHeartbeatCooldown = 1.5f;
        private const float BoardIntegrityLogCooldown = 1f;
        private const float MovementTraceLogCooldown = 0.25f;
        private const float BlockedFeedbackDuration = 0.15f;
        private const float SlotFullFeedbackCooldown = 0.22f;
        private const float SlotFullLogCooldown = 1f;
        private const float EscapeLaneOffsetCells = 0.12f;
        private const float SlotApproachOffsetCells = 0.18f;
        private const float BlockedImpactOffsetCells = 0.08f;
        private const string DiagnosticVersion = "v3-input-receiver";

        public void Init(LevelConfig config, BoardWorldLayout layout, Camera mainCam)
        {
            if (config == null)
            {
                DebugSystem.AlwaysError(DebugCategory.Board, "Board Init failed: LevelConfig is null.", this);
                _acceptsInput = false;
                return;
            }

            if (!ValidateSetup())
            {
                DebugSystem.AlwaysError(DebugCategory.Board, "Board Init aborted because required prefab references/components are invalid.", this);
                _acceptsInput = false;
                return;
            }

            ActiveInstance = this;
            _acceptsInput = true;
            _boardState = BoardInputState.Ready;
            _nextInputAllowedTime = 0f;
            _transactionStartTime = -999f;
            _lastBoardHeartbeatTime = -999f;
            _lastIntegrityLogTime = -999f;
            _lastMovementTraceLogTime = -999f;
            _layout = layout;
            _mainCamera = mainCam;
            _boardSize = config.boardSize;
            SetupInputReceiver();

            _grid = new BlockIdentity[_boardSize.x, _boardSize.y];
            DebugSystem.Log(DebugCategory.Board, $"Init board level={config.levelNumber} size={_boardSize} blocks={config.blocks.Count}", this);

            foreach (var blockData in config.blocks)
            {
                if (IsBlockWithinBounds(blockData.position, blockData.size))
                {
                    var blockGO = PoolManager.Instance.Get(_blockPrefab, transform);
                    if (blockGO == null)
                    {
                        DebugSystem.AlwaysError(DebugCategory.Board, $"Pool returned null block for id={blockData.id} position={blockData.position}.", this);
                        continue;
                    }

                    blockGO.transform.position = _layout.GetBlockCenter(blockData.position, blockData.size);
                    blockGO.name = $"Block_{blockData.position.x}_{blockData.position.y}";

                    var identity = blockGO.GetComponent<BlockIdentity>();
                    if (identity == null)
                    {
                        DebugSystem.AlwaysError(DebugCategory.Board, $"Spawned block is missing BlockIdentity. Check block prefab '{_blockPrefab.name}' for missing scripts.", blockGO);
                        PoolManager.Instance.Release(_blockPrefab, blockGO);
                        continue;
                    }

                    if (blockGO.GetComponentInChildren<BlockVisual>(true) == null)
                    {
                        DebugSystem.AlwaysError(DebugCategory.Board, $"Spawned block '{blockGO.name}' is missing BlockVisual.", blockGO);
                        PoolManager.Instance.Release(_blockPrefab, blockGO);
                        continue;
                    }

                    if (blockGO.GetComponent<BlockMoveAnimator>() == null)
                        blockGO.AddComponent<BlockMoveAnimator>();

                    identity.Init(blockData, blockData.position, this);
                    DebugSystem.Log(DebugCategory.Board, $"Spawn block id={blockData.id} color={blockData.color} ammo={blockData.ammo} pos={blockData.position} size={blockData.size} dir={blockData.direction}", identity);
                    GameEvents.FireBlockSpawned(new BlockSpawnedPayload
                    {
                        Block = identity,
                        Color = blockData.color,
                        Direction = blockData.direction,
                        Size = blockData.size,
                        CellSize = _layout.CellSize
                    });

                    // Register in ALL cells it occupies
                    for (int x = 0; x < blockData.size.x; x++)
                    {
                        for (int y = 0; y < blockData.size.y; y++)
                        {
                            _grid[blockData.position.x + x, blockData.position.y + y] = identity;
                        }
                    }
                }
            }
        }

        private void Update()
        {
            RecoverActiveInstanceIfNeeded();
            RecoverIfTransactionStuck();
            LogBoardHeartbeatOnce();
        }

        private void OnBoardTapPressed(BoardInputSnapshot input)
        {
            if (!_acceptsInput || ActiveInstance != this || _grid == null || _mainCamera == null)
            {
                LogInputGateOnce($"Input gate closed accepts={_acceptsInput} activeMatch={ActiveInstance == this} gridNull={_grid == null} cameraNull={_mainCamera == null} state={_boardState} pointer={input.Source} pressed={input.IsPressed} edge={input.PressedThisFrame}");
                return;
            }

            RefreshIdleBoardState();
            RecoverStaleInputDebounce();

            if (!CanAcceptTapNow())
            {
                LogInputGateOnce($"Tap ignored by debounce/state state={_boardState} now={Time.unscaledTime:0.000} next={_nextInputAllowedTime:0.000} pointer={input.Source} pressed={input.IsPressed} edge={input.PressedThisFrame}");
                return;
            }

            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(input.ScreenPosition);
            worldPos.z = 0; // Ensure 2D
            DebugSystem.Log(DebugCategory.Input, $"Tap source={input.Source} screen={input.ScreenPosition} world={worldPos} state={_boardState} overUI={input.IsOverUI}", this);

            BlockIdentity identity = FindOwnedBlockAt(worldPos);
            if (identity != null)
                ResolveTapTransactionAsync(identity, this.GetCancellationTokenOnDestroy()).Forget();
            else
                DebugSystem.Log(DebugCategory.Input, "Tap hit no owned block.", this);
        }

        private void OnEnable()
        {
            if (_grid != null)
            {
                if (_inputReceiver != null && _acceptsInput)
                    _inputReceiver.SetInputEnabled(true);

                DebugSystem.Log(DebugCategory.Board, "BoardManager enabled while grid is initialized.", this);
            }
        }

        private void OnDisable()
        {
            if (_inputReceiver != null)
                _inputReceiver.SetInputEnabled(false);
        }

        private void SetupInputReceiver()
        {
            if (_inputReceiver == null)
                _inputReceiver = GetComponent<BoardInputReceiver>();

            if (_inputReceiver == null)
                _inputReceiver = gameObject.AddComponent<BoardInputReceiver>();

            _inputReceiver.TapPressed -= OnBoardTapPressed;
            _inputReceiver.TapPressed += OnBoardTapPressed;
            _inputReceiver.Init(_mainCamera, CanRecoverStuckTouchAtScreen);
        }

        private bool CanRecoverStuckTouchAtScreen(Vector2 screenPosition)
        {
            if (_mainCamera == null)
                return false;

            if (_boardState != BoardInputState.Ready &&
                _boardState != BoardInputState.BoosterSelecting)
            {
                return false;
            }

            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(screenPosition);
            worldPos.z = 0f;
            BlockIdentity block = FindOwnedBlockAtQuiet(worldPos);
            return block != null && !block.IsMoving;
        }

        public bool ValidateSetup()
        {
            if (_blockPrefab == null)
            {
                DebugSystem.AlwaysError(DebugCategory.Board, "BoardManager missing Block Prefab reference.", this);
                return false;
            }

            var identity = _blockPrefab.GetComponent<BlockIdentity>();
            bool isValid = true;

            if (identity == null)
            {
                DebugSystem.AlwaysError(DebugCategory.Board, $"Block prefab '{_blockPrefab.name}' is missing BlockIdentity. Check for missing script on the block prefab.", _blockPrefab);
                isValid = false;
            }
            else if (_blockPrefab.GetComponentInChildren<BlockVisual>(true) == null)
            {
                DebugSystem.AlwaysError(DebugCategory.Board, $"Block prefab '{_blockPrefab.name}' is missing BlockVisual.", _blockPrefab);
                isValid = false;
            }

            if (_blockPrefab.GetComponentInChildren<Collider2D>(true) == null)
            {
                DebugSystem.AlwaysError(DebugCategory.Board, $"Block prefab '{_blockPrefab.name}' has no Collider2D in its hierarchy. Board raycasts cannot hit blocks.", _blockPrefab);
                isValid = false;
            }

            return isValid;
        }

        private bool CanAcceptTapNow()
        {
            if (Time.unscaledTime < _nextInputAllowedTime)
                return false;

            return _boardState == BoardInputState.Ready ||
                   _boardState == BoardInputState.BoosterSelecting;
        }

        private void RefreshIdleBoardState()
        {
            if (_boardState == BoardInputState.ResolvingMove ||
                _boardState == BoardInputState.LevelEnding)
            {
                return;
            }

            _boardState = IsRemoveBoosterSelecting()
                ? BoardInputState.BoosterSelecting
                : BoardInputState.Ready;
        }

        private void RecoverStaleInputDebounce()
        {
            if (_boardState != BoardInputState.Ready &&
                _boardState != BoardInputState.BoosterSelecting)
            {
                return;
            }

            float remaining = _nextInputAllowedTime - Time.unscaledTime;
            if (remaining <= MaxInputDebounceSeconds)
                return;

            DebugSystem.AlwaysWarning(
                DebugCategory.Input,
                $"Input debounce was stuck too far in the future. Recovering. state={_boardState} now={Time.unscaledTime:0.000} next={_nextInputAllowedTime:0.000} remaining={remaining:0.000}",
                this);

            _nextInputAllowedTime = Time.unscaledTime;
        }

        private BlockIdentity FindOwnedBlockAt(Vector3 worldPos)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(worldPos, Vector2.zero);
            DebugSystem.Log(DebugCategory.Input, $"RaycastAll hits={hits.Length} at={worldPos}", this);

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;

                var identity = hits[i].collider.GetComponentInParent<BlockIdentity>();
                DebugSystem.Log(
                    DebugCategory.Input,
                    $"Hit[{i}] collider={hits[i].collider.name} block={(identity != null ? identity.name : "null")} ownerMatch={(identity != null && identity.Owner == this)} moving={(identity != null && identity.IsMoving)} active={(identity != null && identity.gameObject.activeInHierarchy)}",
                    this);

                if (identity != null && identity.Owner == this)
                    return identity;
            }

            return null;
        }

        private BlockIdentity FindOwnedBlockAtQuiet(Vector3 worldPos)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(worldPos, Vector2.zero);

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;

                var identity = hits[i].collider.GetComponentInParent<BlockIdentity>();
                if (identity != null && identity.Owner == this)
                    return identity;
            }

            return null;
        }

        private async UniTaskVoid ResolveTapTransactionAsync(BlockIdentity block, CancellationToken ct)
        {
            if (!CanBeginTransaction(block))
            {
                DebugSystem.Log(DebugCategory.Input, $"Transaction rejected block={(block != null ? block.name : "null")} state={_boardState} moving={(block != null && block.IsMoving)} valid={IsValidOwnedBlock(block)}", this);
                return;
            }

            _boardState = BoardInputState.ResolvingMove;
            _transactionStartTime = Time.unscaledTime;
            _nextInputAllowedTime = Time.unscaledTime + InputDebounceSeconds;
            block.SetIsMoving(true);
            DebugSystem.Log(DebugCategory.Board, $"Transaction begin block={block.name} color={block.Color} ammo={block.Ammo} pos={block.GridPos} state={_boardState}", block);

            try
            {
                ct.ThrowIfCancellationRequested();

                if (!IsValidOwnedBlock(block))
                {
                    DebugSystem.Warning(DebugCategory.Board, $"Transaction block became invalid before move block={block.name}", block);
                    return;
                }

                if (TryHandleBoosterSelection(block))
                {
                    DebugSystem.Log(DebugCategory.Board, $"Transaction consumed by remove booster block={block.name}", this);
                    return;
                }

                await TryMoveBlockAsync(block, ct);
            }
            catch (OperationCanceledException)
            {
                DebugSystem.Log(DebugCategory.Board, $"Transaction cancelled block={(block != null ? block.name : "null")}", this);
            }
            catch (Exception ex)
            {
                DebugSystem.Exception(DebugCategory.Board, ex, this);
            }
            finally
            {
                bool canUnlockBlock = block != null && IsValidOwnedBlock(block);
                if (canUnlockBlock)
                    block.SetIsMoving(false);

                if (_boardState != BoardInputState.LevelEnding && ActiveInstance == this)
                    _boardState = IsRemoveBoosterSelecting() ? BoardInputState.BoosterSelecting : BoardInputState.Ready;

                _transactionStartTime = -999f;
                _nextInputAllowedTime = Mathf.Max(_nextInputAllowedTime, Time.unscaledTime + InputDebounceSeconds);
                DebugSystem.Log(DebugCategory.Board, $"Transaction end block={(block != null ? block.name : "null")} blockUnlocked={canUnlockBlock} state={_boardState} nextInput={_nextInputAllowedTime:0.000}", this);
            }
        }

        private void RecoverIfTransactionStuck()
        {
            if (_boardState != BoardInputState.ResolvingMove)
                return;

            if (Time.unscaledTime - _transactionStartTime < StuckTransactionSeconds)
                return;

            DebugSystem.Warning(DebugCategory.Board, $"Board transaction watchdog fired state={_boardState} elapsed={Time.unscaledTime - _transactionStartTime:0.000}. Forcing board ready and clearing block moving flags.", this);
            ResetMovingFlagsForOwnedBlocks();
            _boardState = IsRemoveBoosterSelecting() ? BoardInputState.BoosterSelecting : BoardInputState.Ready;
            _transactionStartTime = -999f;
            _nextInputAllowedTime = Time.unscaledTime + InputDebounceSeconds;
        }

        private void RecoverActiveInstanceIfNeeded()
        {
            if (!_acceptsInput || _grid == null)
                return;

            bool activeInstanceInvalid = ActiveInstance == null ||
                                         !ActiveInstance.gameObject.activeInHierarchy ||
                                         !ActiveInstance._acceptsInput ||
                                         ActiveInstance._grid == null;

            if (ActiveInstance != this && activeInstanceInvalid)
            {
                ActiveInstance = this;
                DebugSystem.AlwaysWarning(DebugCategory.Board, "Board ActiveInstance was missing or stale while this board was still initialized. Recovered ActiveInstance to this board.", this);
            }
        }

        private void LogBoardHeartbeatOnce()
        {
            if (!DebugSystem.IsEnabled(DebugCategory.Input) && !DebugSystem.IsEnabled(DebugCategory.Board))
                return;

            if (Time.unscaledTime - _lastBoardHeartbeatTime < BoardHeartbeatCooldown)
                return;

            _lastBoardHeartbeatTime = Time.unscaledTime;
            CountGridBlocks(out int blockCount, out int movingCount, out int inactiveCount, out int ownerMismatchCount);
            BoardInputSnapshot input = _inputReceiver != null
                ? _inputReceiver.LastSnapshot
                : new BoardInputSnapshot("None", Vector2.zero, false, false, false);

            DebugCategory category = DebugSystem.IsEnabled(DebugCategory.Input)
                ? DebugCategory.Input
                : DebugCategory.Board;

            DebugSystem.Log(
                category,
                $"Board heartbeat diag={DiagnosticVersion} now={Time.unscaledTime:0.000} activeHierarchy={gameObject.activeInHierarchy} enabled={enabled} accepts={_acceptsInput} activeMatch={ActiveInstance == this} activeNull={ActiveInstance == null} gridNull={_grid == null} cameraNull={_mainCamera == null} state={_boardState} nextInput={_nextInputAllowedTime:0.000} pointer={input.Source} pressed={input.IsPressed} edge={input.PressedThisFrame} overUI={input.IsOverUI} blocks={blockCount} moving={movingCount} inactive={inactiveCount} ownerMismatch={ownerMismatchCount}",
                this);
        }

        private void CountGridBlocks(out int blockCount, out int movingCount, out int inactiveCount, out int ownerMismatchCount)
        {
            blockCount = 0;
            movingCount = 0;
            inactiveCount = 0;
            ownerMismatchCount = 0;

            if (_grid == null) return;

            HashSet<BlockIdentity> visited = new HashSet<BlockIdentity>();

            for (int x = 0; x < _boardSize.x; x++)
            {
                for (int y = 0; y < _boardSize.y; y++)
                {
                    BlockIdentity block = _grid[x, y];
                    if (block == null || !visited.Add(block))
                        continue;

                    blockCount++;

                    if (block.IsMoving)
                        movingCount++;

                    if (!block.gameObject.activeInHierarchy)
                        inactiveCount++;

                    if (block.Owner != this)
                        ownerMismatchCount++;
                }
            }
        }

        private void ResetMovingFlagsForOwnedBlocks()
        {
            if (_grid == null) return;

            HashSet<BlockIdentity> visited = new HashSet<BlockIdentity>();

            for (int x = 0; x < _boardSize.x; x++)
            {
                for (int y = 0; y < _boardSize.y; y++)
                {
                    BlockIdentity block = _grid[x, y];
                    if (block != null && visited.Add(block))
                    {
                        block.SetIsMoving(false);
                        DebugSystem.Log(DebugCategory.Board, $"Watchdog reset moving flag block={block.name}", block);
                    }
                }
            }
        }

        private void LogInputGateOnce(string message)
        {
            if (Time.unscaledTime - _lastInputGateLogTime < InputGateLogCooldown)
                return;

            _lastInputGateLogTime = Time.unscaledTime;
            DebugSystem.Log(DebugCategory.Input, message, this);
        }

        private void LogMovementTraceOnce(string message, UnityEngine.Object context)
        {
            if (Time.unscaledTime - _lastMovementTraceLogTime < MovementTraceLogCooldown)
                return;

            _lastMovementTraceLogTime = Time.unscaledTime;
            DebugSystem.Log(DebugCategory.Board, message, context);
        }

        private bool CanBeginTransaction(BlockIdentity block)
        {
            bool accepts = _acceptsInput;
            bool active = ActiveInstance == this;
            bool canTap = CanAcceptTapNow();
            bool valid = IsValidOwnedBlock(block);
            bool notMoving = block != null && !block.IsMoving;

            if (!accepts || !active || !canTap || !valid || !notMoving)
            {
                DebugSystem.Log(DebugCategory.Input, $"CanBegin=false accepts={accepts} active={active} canTap={canTap} valid={valid} notMoving={notMoving} state={_boardState}", this);
            }

            return accepts && active && canTap && valid && notMoving;
        }

        private bool IsValidOwnedBlock(BlockIdentity block)
        {
            return block != null &&
                   block.Owner == this &&
                   block.gameObject.activeInHierarchy &&
                   IsBlockRegistered(block);
        }

        private bool IsBlockRegistered(BlockIdentity block)
        {
            if (_grid == null) return false;

            for (int x = 0; x < block.Size.x; x++)
            {
                for (int y = 0; y < block.Size.y; y++)
                {
                    Vector2Int cell = new Vector2Int(block.GridPos.x + x, block.GridPos.y + y);
                    if (!IsCellWithinBounds(cell) || _grid[cell.x, cell.y] != block)
                    {
                        DebugSystem.Log(DebugCategory.Board, $"Block registration invalid block={block.name} cell={cell} inBounds={IsCellWithinBounds(cell)} gridBlock={(IsCellWithinBounds(cell) && _grid[cell.x, cell.y] != null ? _grid[cell.x, cell.y].name : "null")}", this);
                        return false;
                    }
                }
            }

            return true;
        }

        private bool IsRemoveBoosterSelecting()
        {
            return BoosterManager.Instance != null &&
                   BoosterManager.Instance.ActiveSelectionMode == BoosterType.Remove;
        }

        private bool TryHandleBoosterSelection(BlockIdentity block)
        {
            if (!IsRemoveBoosterSelecting())
            {
                return false;
            }

            CannonColor removedColor = block.Color;
            int removedAmmo = block.Ammo;

            RemoveBlock(block);

            if (DragonManager.Instance != null)
            {
                int destroyed = DragonManager.Instance.DestroyLeadingSegmentsByColor(removedColor, removedAmmo);
                DebugSystem.Log(DebugCategory.Booster, $"Remove booster selected color={removedColor} ammo={removedAmmo} destroyedDragonSegments={destroyed}.", this);
            }
            else
            {
                DebugSystem.Warning(DebugCategory.Booster, "Remove booster could not destroy matching dragon segments because DragonManager.Instance is null.", this);
            }

            BoosterManager.Instance.ConsumeCharge(BoosterType.Remove);
            return true;
        }

        private void RemoveBlock(BlockIdentity block)
        {
            RemoveBlockFromGrid(block);

            block.ResetData();
            PoolManager.Instance.Release(_blockPrefab, block.gameObject);
        }

        private void RemoveBlockFromGrid(BlockIdentity block)
        {
            for (int x = 0; x < block.Size.x; x++)
            {
                for (int y = 0; y < block.Size.y; y++)
                {
                    Vector2Int cell = new Vector2Int(block.GridPos.x + x, block.GridPos.y + y);
                    if (IsCellWithinBounds(cell) && _grid[cell.x, cell.y] == block)
                    {
                        _grid[cell.x, cell.y] = null;
                    }
                }
            }
        }

        private async UniTask TryMoveBlockAsync(BlockIdentity block, CancellationToken ct)
        {
            BoardEscapeResult escapeResult = BoardEscapeResolver.Resolve(block, _grid, _boardSize);
            if (escapeResult.CanEscape)
            {
                LogMovementTraceOnce($"Path clear block={block.name} dir={block.Direction} ammo={block.Ammo} lastPos={escapeResult.LastPosition}", block);
                if (!TryGetTargetSlot(block, out CannonSlot targetSlot))
                {
                    LogSlotFullOnce($"[BoardManager] Slot commit failed block={block.name}; keeping on board.");
                    FireSlotFullFeedback(block);
                    return;
                }

                BlockMoveAnimator animator = GetBlockAnimator(block);
                if (animator != null)
                {
                    Vector3 slotPosition = targetSlot != null
                        ? targetSlot.transform.position
                        : _layout.GetBlockCenter(escapeResult.LastPosition, block.Size);
                    List<Vector3> escapePath = BuildWorldPath(block.GridPos, escapeResult.LastPosition, block.Size, block.Direction, slotPosition);

                    RemoveBlockFromGrid(block);
                    PlayEscapeAndLoadSlotAsync(block, animator, escapePath, slotPosition, targetSlot, ct).Forget();
                    return;
                }

                if (!TryLoadTargetSlot(block, targetSlot))
                {
                    LogSlotFullOnce($"[BoardManager] Slot load failed after escape animation block={block.name}; returning block to original board position.");
                    if (animator != null)
                        animator.ResetVisualState();

                    block.transform.position = _layout.GetBlockCenter(block.GridPos, block.Size);
                    FireSlotFullFeedback(block);
                    return;
                }

                DebugSystem.Log(DebugCategory.Board, $"Slot commit success block={block.name}; removing from board after animation.", block);
                RemoveBlock(block);
            }
            else
            {
                string blocker = escapeResult.BlockingBlock != null ? escapeResult.BlockingBlock.name : "none";
                LogMovementTraceOnce($"Path blocked block={block.name} dir={block.Direction} checked={escapeResult.CheckedDirection} blocker={blocker} cell={escapeResult.BlockingCell}", block);
                BlockMoveAnimator animator = GetBlockAnimator(block);
                if (animator != null)
                {
                    Vector3 originalPosition = _layout.GetBlockCenter(block.GridPos, block.Size);
                    Vector3 impactPosition = GetBlockedImpactPosition(block, escapeResult);
                    await animator.PlayBlockedReturnAsync(impactPosition, originalPosition, _layout.CellSize, ct);
                }
            }
        }

        private bool TryGetTargetSlot(BlockIdentity block, out CannonSlot targetSlot)
        {
            targetSlot = null;
            if (block.Ammo <= 0) return true;

            SlotBarManager slotBar = SlotBarManager.Instance;
            if (slotBar == null)
            {
                LogSlotFullOnce("[BoardManager] Cannot release block: SlotBarManager.Instance is null.");
                return false;
            }

            if (!slotBar.TryReserveAvailableSlot(block.Ammo, out targetSlot))
            {
                LogSlotFullOnce($"[BoardManager] Slot capacity precheck failed block={block.name} color={block.Color} ammo={block.Ammo}. Commit skipped; block stays on board.");
                return false;
            }

            return true;
        }

        private bool TryLoadTargetSlot(BlockIdentity block, CannonSlot targetSlot)
        {
            if (block.Ammo <= 0) return true;

            SlotBarManager slotBar = SlotBarManager.Instance;
            if (slotBar == null)
            {
                LogSlotFullOnce("[BoardManager] Cannot load escaped block: SlotBarManager.Instance is null.");
                return false;
            }

            bool loaded = slotBar.TryLoadReservedBlockIntoSlot(targetSlot, block.Color, block.Ammo);
            DebugSystem.Log(DebugCategory.Board, $"TryLoadTargetSlot block={block.name} color={block.Color} ammo={block.Ammo} loaded={loaded}", block);
            if (!loaded)
            {
                loaded = slotBar.TryLoadBlock(block.Color, block.Ammo);
                if (!loaded)
                    LogSlotFullOnce($"[BoardManager] Slot commit failed after capacity precheck. SlotBar state: {slotBar.BuildDebugState()}");
            }

            return loaded;
        }

        private async UniTaskVoid PlayEscapeAndLoadSlotAsync(
            BlockIdentity block,
            BlockMoveAnimator animator,
            List<Vector3> escapePath,
            Vector3 slotPosition,
            CannonSlot targetSlot,
            CancellationToken ct)
        {
            try
            {
                await animator.PlayEscapeToSlotAsync(escapePath, slotPosition, _layout.CellSize, ct);

                if (block == null || block.Owner != this)
                    return;

                if (!TryLoadTargetSlot(block, targetSlot))
                {
                    if (SlotBarManager.Instance != null)
                        SlotBarManager.Instance.ReleaseSlotReservation(targetSlot);

                    DebugSystem.Warning(DebugCategory.Board, $"Escaped block could not load after animation block={block.name}. Releasing it to avoid blocking the board.", block);
                }

                DebugSystem.Log(DebugCategory.Board, $"Slot commit success block={block.name}; removing visual after animation.", block);
                block.ResetData();
                PoolManager.Instance.Release(_blockPrefab, block.gameObject);
            }
            catch (OperationCanceledException)
            {
                if (SlotBarManager.Instance != null)
                    SlotBarManager.Instance.ReleaseSlotReservation(targetSlot);
            }
            catch (Exception ex)
            {
                if (SlotBarManager.Instance != null)
                    SlotBarManager.Instance.ReleaseSlotReservation(targetSlot);

                DebugSystem.Exception(DebugCategory.Board, ex, this);
            }
        }

        private BlockMoveAnimator GetBlockAnimator(BlockIdentity block)
        {
            if (block == null)
                return null;

            BlockMoveAnimator animator = block.GetComponent<BlockMoveAnimator>();
            return animator != null ? animator : block.gameObject.AddComponent<BlockMoveAnimator>();
        }

        private List<Vector3> BuildWorldPath(Vector2Int startPos, Vector2Int endPos, Vector2Int blockSize, Direction direction, Vector3 slotPosition)
        {
            var path = new List<Vector3>();
            Vector2Int current = startPos;
            int guard = Mathf.Max(1, _boardSize.x + _boardSize.y + blockSize.x + blockSize.y + 4);

            for (int i = 0; i < guard && current != endPos; i++)
            {
                current = BoardEscapeResolver.GetNextPos(current, direction);
                path.Add(_layout.GetBlockCenter(current, blockSize));
            }

            if (path.Count == 0 || current != endPos)
                path.Add(_layout.GetBlockCenter(endPos, blockSize));

            AddOutsideLaneRoute(path, endPos, blockSize, direction, slotPosition);
            return path;
        }

        private Vector3 GetBlockedImpactPosition(BlockIdentity block, BoardEscapeResult escapeResult)
        {
            Vector3 originalPosition = _layout.GetBlockCenter(block.GridPos, block.Size);
            Vector2Int safePos = GetPreviousPos(escapeResult.LastPosition, escapeResult.CheckedDirection);
            Vector3 safeCenter = _layout.GetBlockCenter(safePos, block.Size);
            Vector3 candidateCenter = _layout.GetBlockCenter(escapeResult.LastPosition, block.Size);
            Vector3 direction = candidateCenter - safeCenter;

            if (direction.sqrMagnitude <= 0.0001f)
                direction = GetWorldDirection(escapeResult.CheckedDirection);

            if (direction.sqrMagnitude <= 0.0001f)
                return originalPosition;

            float nudgeDistance = _layout.CellSize * BlockedImpactOffsetCells;
            return safeCenter + direction.normalized * nudgeDistance;
        }

        private Vector2Int GetPreviousPos(Vector2Int pos, Direction direction)
        {
            return direction switch
            {
                Direction.Up => new Vector2Int(pos.x, pos.y + 1),
                Direction.Down => new Vector2Int(pos.x, pos.y - 1),
                Direction.Left => new Vector2Int(pos.x + 1, pos.y),
                Direction.Right => new Vector2Int(pos.x - 1, pos.y),
                Direction.UpLeft => new Vector2Int(pos.x + 1, pos.y + 1),
                Direction.UpRight => new Vector2Int(pos.x - 1, pos.y + 1),
                Direction.DownLeft => new Vector2Int(pos.x + 1, pos.y - 1),
                Direction.DownRight => new Vector2Int(pos.x - 1, pos.y - 1),
                _ => pos
            };
        }

        private Vector3 GetWorldDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Up => Vector3.up,
                Direction.Down => Vector3.down,
                Direction.Left => Vector3.left,
                Direction.Right => Vector3.right,
                Direction.UpLeft => (Vector3.up + Vector3.left).normalized,
                Direction.UpRight => (Vector3.up + Vector3.right).normalized,
                Direction.DownLeft => (Vector3.down + Vector3.left).normalized,
                Direction.DownRight => (Vector3.down + Vector3.right).normalized,
                _ => Vector3.zero
            };
        }

        private void AddOutsideLaneRoute(List<Vector3> path, Vector2Int escapedPos, Vector2Int blockSize, Direction direction, Vector3 slotPosition)
        {
            BoardEdgeSide escapeSide = GetEscapeSide(escapedPos, blockSize, direction);
            BoardEdgeSide targetSide = GetSlotApproachSide(slotPosition);
            GetLaneBounds(blockSize, out float left, out float right, out float top, out float bottom);

            Vector3 escapeCenter = _layout.GetBlockCenter(escapedPos, blockSize);
            Vector3 laneStart = ProjectToLane(escapeCenter, escapeSide, left, right, top, bottom);
            Vector3 laneTarget = ProjectToLane(slotPosition, targetSide, left, right, top, bottom);
            AddPointIfDifferent(path, laneStart);
            AddPerimeterRoute(path, laneStart, escapeSide, laneTarget, targetSide, left, right, top, bottom);

            Vector3 slotApproach = GetSlotApproachPoint(slotPosition, targetSide);
            AddPointIfDifferent(path, slotApproach);
        }

        private BoardEdgeSide GetEscapeSide(Vector2Int escapedPos, Vector2Int blockSize, Direction direction)
        {
            bool outsideLeft = escapedPos.x + blockSize.x <= 0;
            bool outsideRight = escapedPos.x >= _boardSize.x;
            bool outsideTop = escapedPos.y + blockSize.y <= 0;
            bool outsideBottom = escapedPos.y >= _boardSize.y;

            if (outsideTop && !outsideBottom && !outsideLeft && !outsideRight) return BoardEdgeSide.Top;
            if (outsideBottom && !outsideTop && !outsideLeft && !outsideRight) return BoardEdgeSide.Bottom;
            if (outsideLeft && !outsideRight) return BoardEdgeSide.Left;
            if (outsideRight && !outsideLeft) return BoardEdgeSide.Right;

            return direction switch
            {
                Direction.Up => BoardEdgeSide.Top,
                Direction.Down => BoardEdgeSide.Bottom,
                Direction.Left => BoardEdgeSide.Left,
                Direction.Right => BoardEdgeSide.Right,
                Direction.UpLeft => BoardEdgeSide.Top,
                Direction.UpRight => BoardEdgeSide.Top,
                Direction.DownLeft => BoardEdgeSide.Bottom,
                Direction.DownRight => BoardEdgeSide.Bottom,
                _ => BoardEdgeSide.Bottom
            };
        }

        private BoardEdgeSide GetSlotApproachSide(Vector3 slotPosition)
        {
            GetBoardBounds(out float boardLeft, out float boardRight, out float boardTop, out float boardBottom);

            if (slotPosition.y <= boardBottom) return BoardEdgeSide.Bottom;
            if (slotPosition.y >= boardTop) return BoardEdgeSide.Top;
            if (slotPosition.x <= boardLeft) return BoardEdgeSide.Left;
            if (slotPosition.x >= boardRight) return BoardEdgeSide.Right;

            return BoardEdgeSide.Bottom;
        }

        private void GetBoardBounds(out float left, out float right, out float top, out float bottom)
        {
            left = _layout.Origin.x;
            right = _layout.Origin.x + _boardSize.x * _layout.CellSize;
            top = _layout.Origin.y;
            bottom = _layout.Origin.y - _boardSize.y * _layout.CellSize;
        }

        private void GetLaneBounds(Vector2Int blockSize, out float left, out float right, out float top, out float bottom)
        {
            GetBoardBounds(out float boardLeft, out float boardRight, out float boardTop, out float boardBottom);

            float halfWidth = blockSize.x * _layout.CellSize * 0.5f;
            float halfHeight = blockSize.y * _layout.CellSize * 0.5f;
            float clearance = _layout.CellSize * EscapeLaneOffsetCells;

            left = boardLeft - halfWidth - clearance;
            right = boardRight + halfWidth + clearance;
            top = boardTop + halfHeight + clearance;
            bottom = boardBottom - halfHeight - clearance;
        }

        private Vector3 ProjectToLane(Vector3 point, BoardEdgeSide side, float left, float right, float top, float bottom)
        {
            return side switch
            {
                BoardEdgeSide.Top => new Vector3(Mathf.Clamp(point.x, left, right), top, point.z),
                BoardEdgeSide.Bottom => new Vector3(Mathf.Clamp(point.x, left, right), bottom, point.z),
                BoardEdgeSide.Left => new Vector3(left, Mathf.Clamp(point.y, bottom, top), point.z),
                BoardEdgeSide.Right => new Vector3(right, Mathf.Clamp(point.y, bottom, top), point.z),
                _ => point
            };
        }

        private void AddPerimeterRoute(
            List<Vector3> path,
            Vector3 start,
            BoardEdgeSide startSide,
            Vector3 target,
            BoardEdgeSide targetSide,
            float left,
            float right,
            float top,
            float bottom)
        {
            if (startSide == targetSide)
            {
                AddPointIfDifferent(path, target);
                return;
            }

            float clockwiseDistance = GetClockwiseDistance(start, startSide, target, targetSide, left, right, top, bottom);
            int step = clockwiseDistance <= GetCounterClockwiseDistance(start, startSide, target, targetSide, left, right, top, bottom) ? 1 : -1;
            BoardEdgeSide side = startSide;

            for (int i = 0; i < 4 && side != targetSide; i++)
            {
                AddPointIfDifferent(path, GetCornerForExit(side, step, left, right, top, bottom));
                side = StepSide(side, step);
            }

            AddPointIfDifferent(path, target);
        }

        private float GetClockwiseDistance(Vector3 start, BoardEdgeSide startSide, Vector3 target, BoardEdgeSide targetSide, float left, float right, float top, float bottom)
        {
            float distance = 0f;
            Vector3 current = start;
            BoardEdgeSide side = startSide;

            for (int i = 0; i < 4 && side != targetSide; i++)
            {
                Vector3 corner = GetCornerForExit(side, 1, left, right, top, bottom);
                distance += Vector3.Distance(current, corner);
                current = corner;
                side = StepSide(side, 1);
            }

            distance += Vector3.Distance(current, target);
            return distance;
        }

        private float GetCounterClockwiseDistance(Vector3 start, BoardEdgeSide startSide, Vector3 target, BoardEdgeSide targetSide, float left, float right, float top, float bottom)
        {
            float distance = 0f;
            Vector3 current = start;
            BoardEdgeSide side = startSide;

            for (int i = 0; i < 4 && side != targetSide; i++)
            {
                Vector3 corner = GetCornerForExit(side, -1, left, right, top, bottom);
                distance += Vector3.Distance(current, corner);
                current = corner;
                side = StepSide(side, -1);
            }

            distance += Vector3.Distance(current, target);
            return distance;
        }

        private BoardEdgeSide StepSide(BoardEdgeSide side, int step)
        {
            int count = 4;
            int index = ((int)side + step + count) % count;
            return (BoardEdgeSide)index;
        }

        private Vector3 GetCornerForExit(BoardEdgeSide side, int step, float left, float right, float top, float bottom)
        {
            return side switch
            {
                BoardEdgeSide.Top => step > 0 ? new Vector3(right, top, 0f) : new Vector3(left, top, 0f),
                BoardEdgeSide.Right => step > 0 ? new Vector3(right, bottom, 0f) : new Vector3(right, top, 0f),
                BoardEdgeSide.Bottom => step > 0 ? new Vector3(left, bottom, 0f) : new Vector3(right, bottom, 0f),
                BoardEdgeSide.Left => step > 0 ? new Vector3(left, top, 0f) : new Vector3(left, bottom, 0f),
                _ => Vector3.zero
            };
        }

        private Vector3 GetSlotApproachPoint(Vector3 slotPosition, BoardEdgeSide targetSide)
        {
            float approach = _layout.CellSize * SlotApproachOffsetCells;

            return targetSide switch
            {
                BoardEdgeSide.Top => slotPosition + Vector3.up * approach,
                BoardEdgeSide.Right => slotPosition + Vector3.right * approach,
                BoardEdgeSide.Bottom => slotPosition + Vector3.down * approach,
                BoardEdgeSide.Left => slotPosition + Vector3.left * approach,
                _ => slotPosition
            };
        }

        private void AddPointIfDifferent(List<Vector3> path, Vector3 point)
        {
            point.z = 0f;

            if (path.Count > 0 && Vector3.SqrMagnitude(path[path.Count - 1] - point) < 0.0001f)
                return;

            path.Add(point);
        }

        private void FireSlotFullFeedback(BlockIdentity block)
        {
            ValidateSlotFullState(block);

            if (Time.unscaledTime - _lastSlotFullFeedbackTime >= SlotFullFeedbackCooldown)
            {
                _lastSlotFullFeedbackTime = Time.unscaledTime;
                GameEvents.FireBlockSlotFull(new BlockFeedbackPayload
                {
                    Block = block,
                    Duration = BlockedFeedbackDuration
                });
            }

            LogSlotFullOnce("[BoardManager] Cannon slots are full. Block stays on the board.");
        }

        private void ValidateSlotFullState(BlockIdentity block)
        {
            if (Time.unscaledTime - _lastIntegrityLogTime < BoardIntegrityLogCooldown)
                return;

            _lastIntegrityLogTime = Time.unscaledTime;

            bool activeMatch = ActiveInstance == this;
            bool validBlock = IsValidOwnedBlock(block);
            CountGridBlocks(out int blockCount, out int movingCount, out int inactiveCount, out int ownerMismatchCount);

            DebugSystem.Log(
                DebugCategory.Board,
                $"SlotFull integrity block={(block != null ? block.name : "null")} validBlock={validBlock} accepts={_acceptsInput} activeMatch={activeMatch} gridNull={_grid == null} blocks={blockCount} moving={movingCount} inactive={inactiveCount} ownerMismatch={ownerMismatchCount} state={_boardState}",
                this);

            if (validBlock && activeMatch && _acceptsInput)
                return;

            DebugSystem.AlwaysError(
                DebugCategory.Board,
                $"SlotFull left board in unsafe state. Recovering input. block={(block != null ? block.name : "null")} validBlock={validBlock} accepts={_acceptsInput} activeMatch={activeMatch} gridNull={_grid == null}",
                this);

            _acceptsInput = true;
            if (!activeMatch)
                ActiveInstance = this;

            if (_boardState != BoardInputState.LevelEnding)
                _boardState = IsRemoveBoosterSelecting() ? BoardInputState.BoosterSelecting : BoardInputState.Ready;

            ResetMovingFlagsForOwnedBlocks();
            _transactionStartTime = -999f;
            _nextInputAllowedTime = Time.unscaledTime + InputDebounceSeconds;
        }

        private void LogSlotFullOnce(string message)
        {
            if (Time.unscaledTime - _lastSlotFullLogTime < SlotFullLogCooldown)
                return;

            _lastSlotFullLogTime = Time.unscaledTime;
            DebugSystem.Log(DebugCategory.Board, message, this);
        }

        private bool IsCellWithinBounds(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < _boardSize.x && pos.y >= 0 && pos.y < _boardSize.y;
        }

        private bool IsBlockWithinBounds(Vector2Int pos, Vector2Int size)
        {
            return pos.x >= 0 && pos.x + size.x <= _boardSize.x && 
                   pos.y >= 0 && pos.y + size.y <= _boardSize.y;
        }

        public void ClearBoard()
        {
            _acceptsInput = false;
            _boardState = BoardInputState.LevelEnding;
            if (_inputReceiver != null)
                _inputReceiver.SetInputEnabled(false);

            DebugSystem.Log(DebugCategory.Board, "ClearBoard begin.", this);

            if (_grid == null) return;

            HashSet<BlockIdentity> releasedBlocks = new HashSet<BlockIdentity>();

            for (int x = 0; x < _boardSize.x; x++)
            {
                for (int y = 0; y < _boardSize.y; y++)
                {
                    BlockIdentity block = _grid[x, y];

                    if (block != null && releasedBlocks.Add(block))
                    {
                        block.ResetData();
                        PoolManager.Instance.Release(_blockPrefab, block.gameObject);
                    }

                    _grid[x, y] = null;
                }
            }
        }

        private void OnDestroy()
        {
            _acceptsInput = false;
            _boardState = BoardInputState.LevelEnding;
            if (_inputReceiver != null)
                _inputReceiver.TapPressed -= OnBoardTapPressed;

            DebugSystem.Log(DebugCategory.Board, "Board destroyed.", this);

            if (ActiveInstance == this)
                ActiveInstance = null;
        }
        
        [ContextMenu("Debug / Print Grid")]
        private void PrintGrid()
        {
            string output = "Grid State:\n";
            for (int y = 0; y < _boardSize.y; y++)
            {
                for (int x = 0; x < _boardSize.x; x++)
                {
                    output += _grid[x, y] != null ? $"[{_grid[x, y].Color}]" : "[ -- ]";
                }
                output += "\n";
            }
            DebugSystem.Log(DebugCategory.Board, output, this);
        }
        
        [ContextMenu("Debug / Clear All Blocks")]
        private void DebugClearAllBlocks()
        {
            ClearBoard();
        }

        [ContextMenu("Debug / Dump Runtime State")]
        private void DebugDumpRuntimeState()
        {
            CountGridBlocks(out int blockCount, out int movingCount, out int inactiveCount, out int ownerMismatchCount);
            string slotState = SlotBarManager.Instance != null ? SlotBarManager.Instance.BuildDebugState() : "slotBar=null";

            DebugSystem.AlwaysLog(
                DebugCategory.Board,
                $"Board dump activeHierarchy={gameObject.activeInHierarchy} enabled={enabled} accepts={_acceptsInput} activeMatch={ActiveInstance == this} activeNull={ActiveInstance == null} gridNull={_grid == null} cameraNull={_mainCamera == null} state={_boardState} nextInput={_nextInputAllowedTime:0.000} blocks={blockCount} moving={movingCount} inactive={inactiveCount} ownerMismatch={ownerMismatchCount} slots={slotState}",
                this);
        }
    }
}
