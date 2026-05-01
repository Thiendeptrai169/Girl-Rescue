using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DragonRescue.Data;
using DragonRescue.Core;
using DragonRescue.Entities.Dragon;
using DragonRescue.Entities.Cannon;

namespace DragonRescue.Booster
{
    public class BoosterManager : Singleton<BoosterManager>
    {
        private Dictionary<BoosterType, int> _charges = new();
        private Dictionary<BoosterType, BoosterData> _boosterData = new();
        private CancellationTokenSource _furtherCts;
        
        public BoosterType? ActiveSelectionMode { get; private set; } = null;
        public float FireRangeMultiplier { get; private set; } = 1f;

        public void Init(LevelConfig config)
        {
            _charges.Clear();
            _boosterData.Clear();
            ActiveSelectionMode = null;
            FireRangeMultiplier = 1f;
            StopFurtherEffect();

            if (config == null || config.boosters == null)
            {
                GameEvents.FireBoosterSelectionModeChanged(null);
                return;
            }

            foreach (var booster in config.boosters)
            {
                if (booster.enabled)
                {
                    _charges[booster.type] = booster.charges;
                    _boosterData[booster.type] = booster;
                    GameEvents.FireBoosterChargeChanged(booster.type, booster.charges);
                }
            }
            GameEvents.FireBoosterSelectionModeChanged(null);
        }

        public int GetCharge(BoosterType type)
        {
            return _charges.TryGetValue(type, out int charge) ? charge : 0;
        }

        public void TryActivateBooster(BoosterType type)
        {
            if (!_charges.ContainsKey(type) || _charges[type] <= 0)
            {
                DebugSystem.Warning(DebugCategory.Booster, $"No charges left for {type}", this);
                return;
            }

            if (ActiveSelectionMode == type)
            {
                // Toggle off if already selected
                ActiveSelectionMode = null;
                GameEvents.FireBoosterSelectionModeChanged(null);
                return;
            }

            switch (type)
            {
                case BoosterType.Remove:
                    ActiveSelectionMode = type;
                    GameEvents.FireBoosterSelectionModeChanged(type);
                    break;

                case BoosterType.Unlock:
                    // Instant
                    if (SlotBarManager.Instance != null && SlotBarManager.Instance.UnlockNextSlot())
                    {
                        ConsumeCharge(type);
                    }
                    else
                    {
                        DebugSystem.Log(DebugCategory.Booster, "No locked slots available to unlock. Charge saved.", this);
                    }
                    break;

                case BoosterType.Sort:
                    // Instant
                    if (DragonManager.Instance != null && DragonManager.Instance.SortSegmentsByColor())
                    {
                        ConsumeCharge(type);
                    }
                    else
                    {
                        DebugSystem.Log(DebugCategory.Booster, "Sorting did not apply (e.g. only 1 color left). Charge saved.", this);
                    }
                    break;

                case BoosterType.Further:
                    // Instant
                    ActivateFurther(type);
                    break;
            }
        }

        public void ConsumeCharge(BoosterType type)
        {
            if (_charges.ContainsKey(type) && _charges[type] > 0)
            {
                _charges[type]--;
                GameEvents.FireBoosterChargeChanged(type, _charges[type]);

                if (ActiveSelectionMode == type)
                {
                    ActiveSelectionMode = null;
                    GameEvents.FireBoosterSelectionModeChanged(null);
                }
            }
        }

        public void CancelSelectionMode()
        {
            if (ActiveSelectionMode.HasValue)
            {
                ActiveSelectionMode = null;
                GameEvents.FireBoosterSelectionModeChanged(null);
            }
        }

        private void ActivateFurther(BoosterType type)
        {
            if (!_boosterData.TryGetValue(type, out var data)) return;

            ConsumeCharge(type);
            
            float duration = data.duration > 0 ? data.duration : 10f; // Default to 10s if not set
            float multiplier = data.multiplier > 1f ? data.multiplier : 2f; // Default 2x range

            ApplyFurtherEffectAsync(duration, multiplier).Forget();
        }

        private async UniTaskVoid ApplyFurtherEffectAsync(float duration, float multiplier)
        {
            StopFurtherEffect();
            _furtherCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            DebugSystem.Log(DebugCategory.Booster, $"Further activated: Range x{multiplier} for {duration}s", this);
            FireRangeMultiplier = multiplier;

            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(duration), ignoreTimeScale: false, cancellationToken: _furtherCts.Token);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            DebugSystem.Log(DebugCategory.Booster, "Further expired: Range back to normal", this);
            FireRangeMultiplier = 1f;
        }

        private void StopFurtherEffect()
        {
            if (_furtherCts == null) return;

            _furtherCts.Cancel();
            _furtherCts.Dispose();
            _furtherCts = null;
        }

        protected override void OnDestroy()
        {
            StopFurtherEffect();
            base.OnDestroy();
        }

        [ContextMenu("Debug / +10 All Charges")]
        private void DebugAddCharges()
        {
            foreach (var type in System.Enum.GetValues(typeof(BoosterType)))
            {
                BoosterType bt = (BoosterType)type;
                if (!_charges.ContainsKey(bt)) _charges[bt] = 0;
                _charges[bt] += 10;
                GameEvents.FireBoosterChargeChanged(bt, _charges[bt]);
            }
        }
    }
}
