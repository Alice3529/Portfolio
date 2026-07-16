using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace CorgiPlay.PuzzleGame.Hints
{
    public class HintUI : MonoBehaviour
    {
        private const float EndlessHintsFontSize = 42f;

        [SerializeField] private TextMeshProUGUI hintText = default;
        [SerializeField] private Button hintButton = default;
        [SerializeField] private HintSystemController controller = default;
        [SerializeField] private GameObject addHintButton = default;

        private RewardedHintService rewardedHintService = default;
        private bool isHintButtonDisabled;
        private float defaultHintFontSize;

        [Inject]
        private void Construct(RewardedHintService rewardedHintService)
        {
            this.rewardedHintService = rewardedHintService;
        }

        private void Awake()
        {
            if (FirstLaunchState.IsTutorialHintModeEnabled)
            {
                this.hintButton.gameObject.SetActive(false);
                return;
            }

            this.defaultHintFontSize = this.hintText.fontSize;

            this.hintButton.OnClickAsObservable()
            .Subscribe(_ => this.GetHint().Forget())
            .AddTo(this);
            
            this.controller.OnHintsEnded += this.ShowAddHintButton;
            this.controller.OnUpdateHintAmount += SetHintAmount;
            this.SetAddHintButtonVisible(false);

            if (this.controller.EndlessHintsEnabled)
            {
                this.SetHintText(endless: true);
            }
        }

        public void SetHintAmount(int amount)
        {
            if (this.controller.EndlessHintsEnabled)
            {
                this.SetHintText(endless: true);
                this.SetAddHintButtonVisible(false);
                return;
            }
            this.SetHintText(endless: false, amount);
            this.SetAddHintButtonVisible(!this.isHintButtonDisabled && amount <= 0);
        }

        private void SetHintText(bool endless, int amount = 0)
        {
            this.hintText.fontSize = endless
                ? EndlessHintsFontSize
                : this.defaultHintFontSize;
            this.hintText.text = endless ? "∞" : amount.ToString();
        }

        public void ShowAddHintButton()
        {
            if (this.isHintButtonDisabled)
            {
                return;
            }

            this.SetAddHintButtonVisible(true);
        }

        public void DisableHintButton()
        {
            this.isHintButtonDisabled = true;
            this.hintButton.interactable = false;
            this.SetAddHintButtonVisible(false);
        }

        private void ShowRewardedHintsAd()
        {
            if (this.controller.EndlessHintsEnabled ||
                this.rewardedHintService.IsWaiting ||
                this.isHintButtonDisabled)
            {
                return;
            }

            this.hintButton.interactable = false;

            this.rewardedHintService.Show(
                onRewarded: hintsAmount =>
                {
                    if (this == null)
                    {
                        return;
                    }

                    this.controller.AddHints(hintsAmount);
                    this.RestoreHintButtonInteractability();
                    this.SetAddHintButtonVisible(false);
                },
                onFailed: _ =>
                {
                    if (this == null)
                    {
                        return;
                    }

                    this.RestoreHintButtonInteractability();
                    this.SetAddHintButtonVisible(!this.isHintButtonDisabled);
                });
        }

        private async UniTask GetHint()
        {
            if (this.isHintButtonDisabled || this.rewardedHintService.IsWaiting)
            {
                return;
            }

            if (!this.controller.HasHints())
            {
                this.SetAddHintButtonVisible(true);
                this.ShowRewardedHintsAd();
                return;
            }

            this.hintButton.interactable = false;
            await controller.GetHint();
            this.RestoreHintButtonInteractability();
        }

        private void RestoreHintButtonInteractability()
        {
            this.hintButton.interactable = !this.isHintButtonDisabled;
        }

        private void SetAddHintButtonVisible(bool isVisible)
        {
            this.addHintButton.SetActive(isVisible);
        }

        private void OnDestroy()
        {
            this.controller.OnHintsEnded -= this.ShowAddHintButton;
            this.controller.OnUpdateHintAmount -= SetHintAmount;            
        }
    }
}
