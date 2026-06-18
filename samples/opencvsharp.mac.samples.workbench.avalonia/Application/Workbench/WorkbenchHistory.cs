using System.Collections.Generic;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.Application.Workbench;

public sealed class WorkbenchHistory<TSnapshot>
{
    private readonly Stack<TSnapshot> undoStack = [];
    private readonly Stack<TSnapshot> redoStack = [];

    public bool CanUndo => undoStack.Count > 0;

    public bool CanRedo => redoStack.Count > 0;

    public void PushUndo(TSnapshot snapshot)
    {
        undoStack.Push(snapshot);
        redoStack.Clear();
    }

    public TSnapshot Undo(TSnapshot current)
    {
        redoStack.Push(current);
        return undoStack.Pop();
    }

    public TSnapshot Redo(TSnapshot current)
    {
        undoStack.Push(current);
        return redoStack.Pop();
    }
}
