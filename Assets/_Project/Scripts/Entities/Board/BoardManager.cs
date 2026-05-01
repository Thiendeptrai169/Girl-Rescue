using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using DragonRescue.Data;
using DragonRescue.Core;
using DragonRescue.Entities.Cannon;
using DragonRescue.Booster;

namespace DragonRescue.Entities.Board
{
    public class BoardManager : MonoBehaviour
    {
        [SerializeField] private GameObject _blockPrefab;
        
        public static BoardManager ActiveInstance { get; private set; }

        private BlockIdentity[,] _grid;
        private BoardWorldLayout _layout;
        private Camera _mainCamera;
        private Vector2Int _boardSize;
        private bool _acceptsInput;
        private float _lastSlotFullLogTime = -999f;
        private const float SlotFullLogCooldown = 1f;

        public void Init(LevelConfig config, BoardWorldLayout layout, Camera mainCam)
        {
            ActiveInstance = this;
            _acceptsInput = true;
            _layout = layout;
            _mainCamera = mainCam;
            _boardSize = config.boardSize;

            _grid = new BlockIdentity[_boardSize.x, _boardSize.y];

            foreach (var blockData in config.blocks)
            {
                if (IsBlockWithinBounds(blockData.position, blockData.size))
                {
                    var blockGO = PoolManager.Instance.Get(_blockPrefab, transform);
                    blockGO.transform.position = _layout.GetBlockCenter(blockData.position, blockData.size);
                    blockGO.name = $"Block_{blockData.position.x}_{blockData.position.y}";

                    var identity = blockGO.GetComponent<BlockIdentity>();
                    identity.Init(blockData, blockData.position, this);

                    // Scale block to fit its total cell area precisely
                    Vector2 spriteSize = identity.Visual.GetSpriteSize();
                    if (spriteSize.x > 0 && spriteSize.y > 0)
                    {
                        float targetWidth = _layout.CellSize * blockData.size.x;
                        float targetHeight = _layout.CellSize * blockData.size.y;

                        // 0.95f leaves a tiny 5% gap so they don't visually bleed into each other
                        float scaleX = (targetWidth / spriteSize.x) * 0.95f;
                        float scaleY = (targetHeight / spriteSize.y) * 0.95f;
                        blockGO.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                    }

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
            if (!_acceptsInput || ActiveInstance != this || _grid == null || _mainCamera == null)
                return;

            bool isPressed = false;
            Vector2 screenPosition = Vector2.zero;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                isPressed = true;
                screenPosition = Mouse.current.position.ReadValue();
            }
            else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                isPressed = true;
                screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            }

            if (isPressed)
            {
                Vector3 worldPos = _mainCamera.ScreenToWorldPoint(screenPosition);
                worldPos.z = 0; // Ensure 2D
                
                BlockIdentity identity = FindOwnedBlockAt(worldPos);
                if (identity != null && !identity.IsMoving)
                {
                    if (TryHandleBoosterSelection(identity))
                    {
                        return;
                    }

                    TryMoveBlock(identity);
                }
            }
        }

        private BlockIdentity FindOwnedBlockAt(Vector3 worldPos)
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

        private bool TryHandleBoosterSelection(BlockIdentity block)
        {
            if (BoosterManager.Instance == null ||
                BoosterManager.Instance.ActiveSelectionMode != BoosterType.Remove)
            {
                return false;
            }

            RemoveBlock(block);
            BoosterManager.Instance.ConsumeCharge(BoosterType.Remove);
            return true;
        }

        private void RemoveBlock(BlockIdentity block)
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

            block.ResetData();
            PoolManager.Instance.Release(_blockPrefab, block.gameObject);
        }

        private void TryMoveBlock(BlockIdentity block)
        {
            if (IsPathClear(block))
            {
                if (!TryCommitBlockToSlot(block))
                {
                    PlaySlotFullFeedback(block);
                    return;
                }

                RemoveBlock(block);
            }
            else
            {
                block.Visual.PlayBlockedFeedback();
            }
        }

        private bool IsPathClear(BlockIdentity block)
        {
            Vector2Int currentPos = block.GridPos;

            while (true)
            {
                // Move the entire footprint one step in the direction
                currentPos = GetNextPos(currentPos, block.Direction);
                bool fullyEscaped = true;

                // Check every cell in the new footprint
                for (int x = 0; x < block.Size.x; x++)
                {
                    for (int y = 0; y < block.Size.y; y++)
                    {
                        Vector2Int checkCell = new Vector2Int(currentPos.x + x, currentPos.y + y);

                        if (IsCellWithinBounds(checkCell))
                        {
                            fullyEscaped = false; // At least one cell is still on the board

                            BlockIdentity occupant = _grid[checkCell.x, checkCell.y];
                            // If there is an occupant and it's NOT this same block itself, we hit something!
                            if (occupant != null && occupant != block)
                            {
                                return false;
                            }
                        }
                    }
                }

                // If all cells checked were outside bounds, the block successfully left the board
                if (fullyEscaped) return true;
            }
        }

        private bool TryCommitBlockToSlot(BlockIdentity block)
        {
            if (block.Ammo <= 0) return true;

            SlotBarManager slotBar = SlotBarManager.Instance;
            if (slotBar == null)
            {
                LogSlotFullOnce("[BoardManager] Cannot release block: SlotBarManager.Instance is null.");
                return false;
            }

            return slotBar.TryLoadBlock(block.Color, block.Ammo);
        }

        private void PlaySlotFullFeedback(BlockIdentity block)
        {
            block.Visual.PlayBlockedFeedback();
            LogSlotFullOnce("[BoardManager] Cannon slots are full. Block stays on the board.");
        }

        private void LogSlotFullOnce(string message)
        {
            if (Time.unscaledTime - _lastSlotFullLogTime < SlotFullLogCooldown)
                return;

            _lastSlotFullLogTime = Time.unscaledTime;
            Debug.Log(message);
        }

        private Vector2Int GetNextPos(Vector2Int pos, Direction dir)
        {
            return dir switch
            {
                Direction.Up => new Vector2Int(pos.x, pos.y - 1),    // Y decreases going up
                Direction.Down => new Vector2Int(pos.x, pos.y + 1),  // Y increases going down
                Direction.Left => new Vector2Int(pos.x - 1, pos.y),
                Direction.Right => new Vector2Int(pos.x + 1, pos.y),
                _ => pos
            };
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
            Debug.Log(output);
        }
        
        [ContextMenu("Debug / Clear All Blocks")]
        private void DebugClearAllBlocks()
        {
            ClearBoard();
        }
    }
}
