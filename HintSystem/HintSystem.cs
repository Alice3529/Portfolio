using System;
using System.Diagnostics;
using CorgiPlay.PuzzleGame.App.SaveSystem;
using CorgiPlay.Unity.Core.Analytics;

namespace CorgiPlay.PuzzleGame.Hints
{
    public class HintSystem
    {
        private const string SettingKey = "HintSystem";
        private int hintAmount = 5;
        private bool endlessHintsEnabled;
        private readonly IAnalyticsService analyticsService = default;
        private ISaveSystem saveSystem = default;

        public event Action<int> HintsChanged;

        public HintSystem(IAnalyticsService analyticsService = null)
        {
            this.analyticsService = analyticsService;
        }

        public void SetSaveSystem(ISaveSystem saveSystem)
        {
            this.saveSystem = saveSystem;
        }

        public void AddHints(int amount)
        {
            int balanceBefore = this.hintAmount;
            this.hintAmount += amount;
            this.saveSystem?.MarkDirty("hints", syncCloud: false);
            this.HintsChanged?.Invoke(this.hintAmount);
            this.TrackHintsEvent("add_hints", amount, balanceBefore, this.hintAmount);
        }

        public bool UseHint()
        {
            if (this.EndlessHintsEnabled)
            {
                return true;
            }

            if (this.hintAmount <= 0)
            {
                return false;
            }

            int balanceBefore = this.hintAmount;
            this.hintAmount -= 1;
            this.saveSystem?.MarkDirty("hints", syncCloud: false);
            this.HintsChanged?.Invoke(this.hintAmount);
            this.TrackHintsEvent("use_hint", 1, balanceBefore, this.hintAmount);
            return true;
        }

        public int GetAmount()
        {
            return this.hintAmount;
        }

        public bool EndlessHintsEnabled =>
            this.endlessHintsEnabled || DevelopmentSettings.EndlessHints;

        public void SetDevelopmentEndlessHints(bool enabled)
        {
            bool wasEnabled = this.EndlessHintsEnabled;
            DevelopmentSettings.EndlessHints = enabled;

            if (wasEnabled != this.EndlessHintsEnabled)
            {
                this.HintsChanged?.Invoke(this.hintAmount);
            }
        }

        public void EnableEndlessHints()
        {
            if (this.endlessHintsEnabled)
            {
                return;
            }

            this.endlessHintsEnabled = true;
            this.HintsChanged?.Invoke(this.hintAmount);
        }

        public void SetHints(int amount)
        {
            int balanceBefore = this.hintAmount;
            this.hintAmount = amount;
            this.HintsChanged?.Invoke(this.hintAmount);
            this.TrackHintsEvent("set_hints", amount, balanceBefore, this.hintAmount);
        }

        private void TrackHintsEvent(
            string eventName,
            int amount,
            int balanceBefore,
            int balanceAfter
        )
        {
            this.analyticsService?.TrackEvent(
                eventName,
                new AnalyticsEventProperties()
                    .Set("amount", amount)
                    .Set("balance_before", balanceBefore)
                    .Set("balance_after", balanceAfter)
            );
        }
    }
}
