using UnityEngine;
using DragonRescue.Data;

namespace DragonRescue.Entities.Board
{
    public class BlockIdentity : MonoBehaviour
    {
        [SerializeField] private BlockVisual _visual;

        public CannonColor Color { get; private set; }
        public Direction Direction { get; private set; }
        public int Ammo { get; private set; }
        public Vector2Int GridPos { get; private set; }
        public Vector2Int Size { get; private set; }
        public bool IsMoving { get; private set; }
        public BoardManager Owner { get; private set; }

        public BlockVisual Visual => _visual;

        public void Init(NormalArrowBlockData data, Vector2Int gridPos, BoardManager owner)
        {
            Color = data.color;
            Direction = data.direction;
            Ammo = data.ammo;
            GridPos = gridPos;
            Size = data.size;
            IsMoving = false;
            Owner = owner;

            _visual.Init(Color, Direction);
        }

        public void SetGridPos(Vector2Int newPos)
        {
            GridPos = newPos;
        }

        public void SetIsMoving(bool isMoving)
        {
            IsMoving = isMoving;
        }

        public void ResetData()
        {
            IsMoving = false;
            Owner = null;
        }
    }
}
