using CorgiPlay.Unity.Core.Analytics;

namespace CorgiPlay.PuzzleGame.Hints
{
    public sealed class HintAddAnalytics
    {
        private const string EventName = "add_hints";

        private readonly IAnalyticsService analyticsService;

        public HintAddAnalytics(IAnalyticsService analyticsService)
        {
            this.analyticsService = analyticsService;
        }

        public void Track(HintAddStatus status, RewardedAdStatus rewardedAdStatus, int amount)
        {
            this.analyticsService?.TrackEvent(
                EventName,
                new AnalyticsEventProperties()
                    .Set("status", status.ToString())
                    .Set("placement", RewardedAdPlacement.HintUIAddHints.ToString())
                    .Set("add_status", rewardedAdStatus.ToString())
                    .Set("amount", amount));
        }
    }
}
