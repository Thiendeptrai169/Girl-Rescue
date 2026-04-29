using UnityEngine;

namespace DragonRescue.Data
{
    /// <summary>
    /// Single source of truth for a level's configuration.
    /// Create one asset per level via: Assets > Create > DragonRescue > Level Definition
    /// </summary>
    [CreateAssetMenu(fileName = "Level_New", menuName = "DragonRescue/Level Definition")]
    public class LevelDefinition : ScriptableObject
    {
        [SerializeField] private string _levelName = "Level 1";

        [Header("Slot Config")]
        [SerializeField] [Range(1, 7)] private int _slotCount = 2;

        [Header("Dragon Config")]
        [SerializeField] private float _dragonSpeed = 1f;
        [SerializeField] private DragonSegmentDefinition[] _dragonSegments;

        [Header("Cannon Tray")]
        [SerializeField] private CannonDefinition[] _availableCannons;

        // TODO: [Header("Boosters")]
        // [SerializeField] private BoosterDefinition[] _boosters;

        public string LevelName => _levelName;
        public int SlotCount => _slotCount;
        public float DragonSpeed => _dragonSpeed;
        public DragonSegmentDefinition[] DragonSegments => _dragonSegments;
        public CannonDefinition[] AvailableCannons => _availableCannons;
    }
}
