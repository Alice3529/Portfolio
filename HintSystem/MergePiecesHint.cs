using CorgiPlay.PuzzleGame;
using CorgiPlay.PuzzleGame.Extensions;
using CorgiPlay.PuzzleGame.Hints;

/// <summary>
/// Finds and swaps ungrouped neighbouring pieces into their correct positions.
/// </summary>
public class MergePiecesHint : IHintAction
{
    private readonly PuzzleManager puzzleManager;
    private readonly PuzzleGrid grid;

    public MergePiecesHint(PuzzleManager puzzleManager)
    {
        this.puzzleManager = puzzleManager;
        this.grid = puzzleManager.grid;
    }

    public bool TryGetPlan(out IHintPlan plan)
    {
        foreach (var piece in this.puzzleManager.allPieces)
        {
            foreach (var neighbour in this.grid.GetNeighbourIds(piece.Id))
            {
                var currentNeighbour = piece.GetNeighbourByDirection(neighbour.direction);
                if (currentNeighbour == null) continue;

                var expectedNeighbourPiece = this.puzzleManager.GetPieceById(neighbour.Id);
                var currentNeighbourPiece = this.puzzleManager.GetPieceById(currentNeighbour.Value.Id);

                if (expectedNeighbourPiece.GetGroup() != null || currentNeighbourPiece.GetGroup() != null) continue;

                plan = new SwapPiecesPlan(expectedNeighbourPiece.DraggablePiece, currentNeighbourPiece.DraggablePiece);
                return true;
            }
        }

        plan = null;
        return false;
    }

}
