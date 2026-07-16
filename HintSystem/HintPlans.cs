using Cysharp.Threading.Tasks;
using CorgiPlay.Unity.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CorgiPlay.PuzzleGame.Hints
{
    public readonly struct HintPointerPath
    {
        public Vector2 StartPosition { get; }
        public Vector2 EndPosition { get; }

        public HintPointerPath(Vector2 startPosition, Vector2 endPosition)
        {
            this.StartPosition = startPosition;
            this.EndPosition = endPosition;
        }
    }

    public sealed class SwapPiecesPlan : IHintPlan
    {
        private readonly DraggablePiece movingPiece;
        private readonly DraggablePiece targetPiece;

        public SwapPiecesPlan(DraggablePiece movingPiece, DraggablePiece targetPiece)
        {
            this.movingPiece = movingPiece;
            this.targetPiece = targetPiece;
        }

        public HintPointerPath GetPointerPath(RectTransform canvasRect)
        {
            Vector2 startPosition = RectTransformExtensions.GetLocalPoint(this.movingPiece.rectTransform, canvasRect);
            Vector2 endPosition = RectTransformExtensions.GetLocalPoint(this.targetPiece.rectTransform, canvasRect);
            return new HintPointerPath(endPosition, startPosition);
        }

        public async UniTask Execute()
        {
            await this.movingPiece.SwapWith(
                this.targetPiece,
                this.movingPiece.rectTransform.localPosition);
        }
    }

    public sealed class MoveGroupPlan : IHintPlan
    {
        private readonly PieceGroup group;
        private readonly List<PuzzlePiece> overlappedPieces;
        private readonly Vector2Int offset;
        private readonly PuzzlePiece anchorPiece;
        private readonly PuzzleGrid grid;
        private readonly RectTransform puzzleContainer;

        public MoveGroupPlan(
            PieceGroup group,
            IEnumerable<PuzzlePiece> overlappedPieces,
            Vector2Int offset,
            PuzzlePiece anchorPiece,
            PuzzleGrid grid,
            RectTransform puzzleContainer)
        {
            this.group = group;
            this.overlappedPieces = new List<PuzzlePiece>(overlappedPieces);
            this.offset = offset;
            this.anchorPiece = anchorPiece;
            this.grid = grid;
            this.puzzleContainer = puzzleContainer;
        }

        public HintPointerPath GetPointerPath(RectTransform canvasRect)
        {
            Vector2 startPosition = RectTransformExtensions.GetLocalPoint(this.anchorPiece.rectTransform, canvasRect);
            Vector2Int currentCoordinates = this.grid.GetCoordinatesById(this.anchorPiece.Id);
            Vector2Int targetCoordinates = currentCoordinates + this.offset;
            Vector3 targetAnchoredPosition = this.grid.GetWorldPositionFromGrid(
                targetCoordinates.x,
                targetCoordinates.y,
                this.puzzleContainer);
            Vector3 targetWorldPosition = this.puzzleContainer.TransformPoint(targetAnchoredPosition);
            Vector2 endPosition = canvasRect.InverseTransformPoint(targetWorldPosition);

            return new HintPointerPath(startPosition, endPosition);
        }

        public async UniTask Execute()
        {
            await this.group.Draggable.Swap(this.overlappedPieces, this.offset);
        }
    }
}
