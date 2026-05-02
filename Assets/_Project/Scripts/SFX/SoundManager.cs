using DragonRescue.Core;
using DragonRescue.Data;
using UnityEngine;

namespace DragonRescue.SFX
{
    [RequireComponent(typeof(AudioSource))]
    public class SoundManager : Singleton<SoundManager>
    {
        private const string FireProjectilePath = "SFX/Fire projectile sound";
        private const string ProjectileHitTargetPath = "SFX/projectile hit target sound";
        private const string UnlockBoosterPath = "SFX/unlock booster sound";
        private const string SortBoosterPath = "SFX/sort booster sound";
        private const string RemoveBoosterPath = "SFX/remove booster sound";
        private const string FurtherBoosterPath = "SFX/further booster sound";

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;

        [Header("Projectile")]
        [SerializeField] private AudioClip _fireProjectileClip;
        [SerializeField] private AudioClip _projectileHitTargetClip;

        [Header("Boosters")]
        [SerializeField] private AudioClip _unlockBoosterClip;
        [SerializeField] private AudioClip _sortBoosterClip;
        [SerializeField] private AudioClip _removeBoosterClip;
        [SerializeField] private AudioClip _furtherBoosterClip;

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
                return;

            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();

            if (_audioSource != null)
                _audioSource.playOnAwake = false;

            LoadMissingClips();
        }

        public static void PlayFireProjectile()
        {
            if (Instance == null) return;
            Instance.PlayClip(Instance._fireProjectileClip);
        }

        public static void PlayProjectileHitTarget()
        {
            if (Instance == null) return;
            Instance.PlayClip(Instance._projectileHitTargetClip);
        }

        public static void PlayBooster(BoosterType type)
        {
            if (Instance == null) return;

            SoundManager manager = Instance;
            AudioClip clip = type switch
            {
                BoosterType.Unlock => manager._unlockBoosterClip,
                BoosterType.Sort => manager._sortBoosterClip,
                BoosterType.Remove => manager._removeBoosterClip,
                BoosterType.Further => manager._furtherBoosterClip,
                _ => null
            };

            manager.PlayClip(clip);
        }

        [ContextMenu("Debug / Play Fire Projectile")]
        private void DebugPlayFireProjectile()
        {
            PlayClip(_fireProjectileClip);
        }

        [ContextMenu("Debug / Play Projectile Hit Target")]
        private void DebugPlayProjectileHitTarget()
        {
            PlayClip(_projectileHitTargetClip);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null)
                return;

            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();

            if (_audioSource == null)
                return;

            _audioSource.PlayOneShot(clip, _sfxVolume);
        }

        private void LoadMissingClips()
        {
            _fireProjectileClip = LoadIfMissing(_fireProjectileClip, FireProjectilePath);
            _projectileHitTargetClip = LoadIfMissing(_projectileHitTargetClip, ProjectileHitTargetPath);
            _unlockBoosterClip = LoadIfMissing(_unlockBoosterClip, UnlockBoosterPath);
            _sortBoosterClip = LoadIfMissing(_sortBoosterClip, SortBoosterPath);
            _removeBoosterClip = LoadIfMissing(_removeBoosterClip, RemoveBoosterPath);
            _furtherBoosterClip = LoadIfMissing(_furtherBoosterClip, FurtherBoosterPath);
        }

        private static AudioClip LoadIfMissing(AudioClip clip, string resourcePath)
        {
            return clip != null ? clip : Resources.Load<AudioClip>(resourcePath);
        }
    }
}
