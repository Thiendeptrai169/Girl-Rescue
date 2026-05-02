using UnityEngine;
using DragonRescue.Data;
using DragonRescue.Core;
using DragonRescue.VFX;

namespace DragonRescue.Entities.Dragon
{
    /// <summary>
    /// EC Identity component for a dragon segment.
    /// Holds data (color, HP, size) and runtime state (alive/dead).
    /// HP logic lives here per EC convention.
    /// Fires directly into GameEvents — no local events.
    /// </summary>
    public class DragonSegmentIdentity : MonoBehaviour
    {
        // ── Sibling Wiring (EC) ──────────────────────────────────────────────
        [SerializeField] private DragonSegmentVisual _visual;
        [SerializeField] private DragonSegmentHitVFX _hitVFX;

        // ── Runtime State ────────────────────────────────────────────────────
        public CannonColor Color     { get; private set; }
        public int         MaxHp    { get; private set; }
        public int         CurrentHp { get; private set; }
        public int         Count => CurrentHp;
        public int         IncomingDamage { get; private set; }
        public bool        IsAlive  => Count > 0;

        // ── Public API ───────────────────────────────────────────────────────
        public void Init(CannonColor color, int count)
        {
            Color     = color;
            MaxHp     = Mathf.Max(1, count);
            CurrentHp = MaxHp;
            IncomingDamage = 0;

            if (_visual != null)
                _visual.Init(Color);

            if (_hitVFX != null)
                _hitVFX.CaptureBaseState(ColorPalette.GetColor(Color));
        }

        public void SetColor(CannonColor color)
        {
            Color = color;

            if (_visual != null)
                _visual.Init(Color);

            if (_hitVFX != null)
                _hitVFX.CaptureBaseState(ColorPalette.GetColor(Color));
        }

        public bool IsTargetable(int damage)
        {
            return CanAcceptIncomingDamage(damage);
        }

        public bool CanAcceptIncomingDamage(int damage)
        {
            return IsAlive && Count - IncomingDamage >= damage;
        }

        public void AddIncomingDamage(int damage)
        {
            IncomingDamage += damage;
        }

        public void ReleaseIncomingDamage(int damage)
        {
            IncomingDamage = Mathf.Max(0, IncomingDamage - damage);
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive) return;

            CurrentHp -= damage;
            bool destroyed = CurrentHp <= 0;

            if (destroyed)
            {
                CurrentHp = 0;
                if (_hitVFX != null)
                    _hitVFX.PlayHit(true, Die);
                else
                    Die();

                return;
            }

            if (_hitVFX != null)
                _hitVFX.PlayHit(false);
        }

        public void ResetData()
        {
            Color     = CannonColor.Blue;
            MaxHp     = 0;
            CurrentHp = 0;
            IncomingDamage = 0;
        }

        // ── Private ──────────────────────────────────────────────────────────
        private void Die()
        {
            DebugSystem.Log(DebugCategory.Dragon, $"{Color} segment destroyed.", this);

            GameEvents.FireSegmentDestroyed(new SegmentDestroyedPayload
            {
                Color    = this.Color,
                Position = transform.position
            });

            gameObject.SetActive(false);
        }
    }
}
