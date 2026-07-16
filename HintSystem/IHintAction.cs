using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CorgiPlay.PuzzleGame.Hints
{
    /// <summary>
    /// Finds and executes a specific type of hint.
    /// </summary>
    public interface IHintAction
    {
        bool TryGetPlan(out IHintPlan plan);
    }

    /// <summary>
    /// Describes a prepared hint that can be visualized and executed.
    /// </summary>
    public interface IHintPlan
    {
        HintPointerPath GetPointerPath(RectTransform canvasRect);
        UniTask Execute();
    }
}
