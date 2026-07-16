namespace CorgiPlay.PuzzleGame.Hints
{
    /// <summary>
    /// Selects the first available hint plan in priority order.
    /// </summary>
    public sealed class HintActionSelector
    {
        private readonly IHintAction extendGroups;
        private readonly IHintAction mergePieces;
        private readonly IHintAction mergeAdjacentElements;
        private readonly IHintAction moveGroupToCorrectPlace;

        public HintActionSelector(
            IHintAction extendGroups,
            IHintAction mergePieces,
            IHintAction mergeAdjacentElements,
            IHintAction moveGroupToCorrectPlace)
        {
            this.extendGroups = extendGroups;
            this.mergePieces = mergePieces;
            this.mergeAdjacentElements = mergeAdjacentElements;
            this.moveGroupToCorrectPlace = moveGroupToCorrectPlace;
        }

        public bool TrySelectPlan(out IHintPlan plan)
        {
            if (this.extendGroups.TryGetPlan(out plan))
            {
                return true;
            }

            if (this.mergePieces.TryGetPlan(out plan))
            {
                return true;
            }

            if (this.mergeAdjacentElements.TryGetPlan(out plan))
            {
                return true;
            }

            return this.moveGroupToCorrectPlace.TryGetPlan(out plan);
        }
    }
}
