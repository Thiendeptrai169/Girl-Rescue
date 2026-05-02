using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DragonRescue.Data;
using DragonRescue.Core;
using DragonRescue.Entities.Dragon;
using DragonRescue.Entities.Cannon;
using DragonRescue.Entities.Board;

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
                    int charges = Mathf.Max(0, booster.charges);
                    if (booster.type == BoosterType.Unlock)
                    {
                        int maxUnlocks = SlotBarManager.Instance != null
                            ? SlotBarManager.Instance.GetLockedSlotCount()
                            : Mathf.Max(0, config.totalSlotCount - config.unlockedSlotCount);
                        charges = Mathf.Min(charges, maxUnlocks);
                    }

                    _charges[booster.type] = charges;
                    _boosterData[booster.type] = booster;
                    GameEvents.FireBoosterChargeChanged(booster.type, charges);
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
            if (!IsGameplayAcceptingBoosters())
            {
                RejectBooster(type, "Booster not available now");
                return;
            }

            if (type == BoosterType.Unlock && !CanActivateUnlockBooster(out string unlockRejectReason))
            {
                RejectBooster(type, unlockRejectReason);
                return;
            }

            if (!_charges.ContainsKey(type) || _charges[type] <= 0)
            {
                DebugSystem.Warning(DebugCategory.Booster, $"No charges left for {type}", this);
                return;
            }

            if (type == BoosterType.Remove && !CanActivateRemoveBooster(out string removeRejectReason))
            {
                RejectBooster(type, removeRejectReason);
                return;
            }

            if (ActiveSelectionMode == type)
            {
                if (type == BoosterType.Remove)
                {
                    DebugSystem.Log(DebugCategory.Booster, "Remove selection is committed until a block is selected.", this);
                    return;
                }

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
                        RejectBooster(type, "Cannon slots not available");
                    }
                    break;

                case BoosterType.Sort:
                    // Instant
                    List<CannonColor> loadedSlotColors = SlotBarManager.Instance != null
                        ? SlotBarManager.Instance.GetLoadedColorsInSlotOrder()
                        : new List<CannonColor>();

                    DebugSystem.Log(
                        DebugCategory.Booster,
                        $"Sort requested loadedSlots={loadedSlotColors.Count} slotState={(SlotBarManager.Instance != null ? SlotBarManager.Instance.BuildDebugState() : "slotBar=null")}",
                        this);

                    if (DragonManager.Instance != null && DragonManager.Instance.SortSegmentsByCannonPriority(loadedSlotColors))
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

            if (SlotBarManager.Instance == null || !SlotBarManager.Instance.HasLoadedCannon())
            {
                DebugSystem.Log(DebugCategory.Booster, "Further rejected: no loaded cannon is available.", this);
                GameEvents.FireGameplayPrompt(new GameplayPromptPayload
                {
                    Message = "Cannon not available in the slot",
                    FlashScreen = false
                });
                return;
            }

            ConsumeCharge(type);
            
            float duration = data.duration > 0 ? data.duration : 10f; // Default to 10s if not set
            float multiplier = data.multiplier > 1f ? data.multiplier : 2f; // Default 2x range

            GameEvents.FireFurtherBuffStarted(new FurtherBuffPayload
            {
                Duration = duration,
                Multiplier = multiplier,
                EndTime = Time.time + duration
            });

            ApplyFurtherEffectAsync(duration, multiplier).Forget();
        }

        private bool IsGameplayAcceptingBoosters()
        {
            return GameManager.Instance == null ||
                   GameManager.Instance.CurrentState == GameState.Playing;
        }

        private bool CanActivateRemoveBooster(out string rejectionMessage)
        {
            rejectionMessage = "No block available to remove";
            return BoardManager.ActiveInstance != null &&
                   BoardManager.ActiveInstance.CanActivateRemoveBooster(out rejectionMessage);
        }

        private bool CanActivateUnlockBooster(out string rejectionMessage)
        {
            rejectionMessage = "All cannon slots are unlocked";

            if (SlotBarManager.Instance == null)
            {
                rejectionMessage = "Cannon slots not available";
                return false;
            }

            return SlotBarManager.Instance.GetLockedSlotCount() > 0;
        }

        private void RejectBooster(BoosterType type, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                message = $"{type} booster not available";

            if (ActiveSelectionMode == type)
            {
                ActiveSelectionMode = null;
                GameEvents.FireBoosterSelectionModeChanged(null);
            }

            DebugSystem.Log(DebugCategory.Booster, $"{type} rejected: {message}", this);
            GameEvents.FireGameplayPrompt(new GameplayPromptPayload
            {
                Message = message,
                FlashScreen = false
            });
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
