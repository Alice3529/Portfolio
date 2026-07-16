using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using VContainer;

namespace CorgiPlay.PuzzleGame.Hints
{
    public class HintSystemController : MonoBehaviour
    {
        [SerializeField] private PuzzleManager puzzleManager = default;

        private HintSystem hintSystem;
        private HintActionSelector hintActionSelector = default;

        public event Action<int> OnUpdateHintAmount = default;
        public event Action OnHintsEnded = default;
        public bool EndlessHintsEnabled => this.hintSystem?.EndlessHintsEnabled == true;

        [Inject]
        private void Construct(HintSystem hintSystem)
        {
            this.hintSystem = hintSystem;
            this.hintSystem.HintsChanged += this.OnHintsChanged;
        }

        public void SetUp()
        {
            if (this.hintSystem == null || this.puzzleManager == null)
            {
                return;
            }

            this.hintActionSelector = new HintActionSelector(
                new ExtendGroupWithPiecesHint(this.puzzleManager),
                new MergePiecesHint(this.puzzleManager),
                new MergeAdjacentElementsHint(this.puzzleManager),
                new MoveGroupToCorrectPlaceHint(this.puzzleManager));
            this.OnHintsChanged(this.hintSystem.GetAmount());
        }

        public bool HasHints()
        {
            return this.hintSystem != null &&
                   (this.hintSystem.EndlessHintsEnabled || this.hintSystem.GetAmount() > 0);
        }

        public void AddHints(int amount)
        {
            if (amount <= 0 || this.hintSystem == null)
            {
                return;
            }

            this.hintSystem.AddHints(amount);
        }

        public async UniTask GetHint()
        {
            if (!this.HasHints())
            {
                this.OnHintsEnded?.Invoke();
                return;
            }

            if (!DragOwnershipService.Instance.TryBlockInput(out IDisposable inputBlock))
            {
                return;
            }

            using (inputBlock)
            {
                this.HideHintPointer();

                if (this.TryGetHintPlan(out IHintPlan plan))
                {
                    await plan.Execute();
                    this.hintSystem.UseHint();
                }
            }
        }
        
        public bool TryGetHintPlan(out IHintPlan plan)
        {
            plan = null;

            if (this.hintActionSelector == null)
            {
                return false;
            }

            return this.hintActionSelector.TrySelectPlan(out plan);
        }

        public bool TryGetHintPointerPath(out HintPointerPath pointerPath)
        {
            pointerPath = default;
            RectTransform canvasRect = this.puzzleManager?.HintPointerPresenter?.CanvasRect;

            if (canvasRect == null || !this.TryGetHintPlan(out IHintPlan plan))
            {
                return false;
            }

            pointerPath = plan.GetPointerPath(canvasRect);
            return true;
        }

        public bool ShowHintPointer()
        {
            HintPointerPresenter hintPointerPresenter = this.puzzleManager?.HintPointerPresenter;

            if (hintPointerPresenter == null || !this.TryGetHintPointerPath(out HintPointerPath pointerPath))
            {
                return false;
            }

            return hintPointerPresenter.Show(pointerPath);
        }

        public void HideHintPointer()
        {
            this.puzzleManager?.HintPointerPresenter?.Hide();
        }

        private void OnHintsChanged(int amount)
        {
            this.OnUpdateHintAmount?.Invoke(amount);

            if (amount <= 0 && !this.EndlessHintsEnabled)
            {
                this.OnHintsEnded?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (this.hintSystem != null)
            {
                this.hintSystem.HintsChanged -= this.OnHintsChanged;
            }
        }
    }
}
