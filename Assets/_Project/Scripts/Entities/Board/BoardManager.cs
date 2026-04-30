using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using DragonRescue.Data;
using DragonRescue.Core;

namespace DragonRescue.Entities.Board
{
    public class BoardManager : MonoBehaviour
    {
        [SerializeField] private GameObject _blockPrefab;
        
        private BlockIdentity[,] _grid;
        private BoardWorldLayout _layout;
        private Camera _mainCamera;
        private Vector2Int _boardSize;

        public void Init(LevelConfig config, BoardWorldLayout layout, Camera mainCam)
        {
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
                    identity.Init(blockData, blockData.position);

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
                
                // Physics2D raycast to detect tapped block
                RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
                if (hit.collider != null)
                {
                    var identity = hit.collider.GetComponent<BlockIdentity>();
                    if (identity != null && !identity.IsMoving)
                    {
                        TryMoveBlock(identity);
                    }
                }
            }
        }

        private void TryMoveBlock(BlockIdentity block)
        {
            if (IsPathClear(block))
            {
                // Remove from ALL occupied grid cells
                for (int x = 0; x < block.Size.x; x++)
                {
                    for (int y = 0; y < block.Size.y; y++)
                    {
                        _grid[block.GridPos.x + x, block.GridPos.y + y] = null;
                    }
                }
                
                // Fire escape event
                GameEvents.FireBlockEscaped(new BlockEscapedPayload
                {
                    Color = block.Color,
                    Ammo = block.Ammo,
                    ExitPosition = block.transform.position
                });

                // Release to pool immediately
                block.ResetData();
                PoolManager.Instance.Release(_blockPrefab, block.gameObject);
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
