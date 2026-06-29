using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Imaging;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Pipeline;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Ports;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;
using OpenCvSharp.CrossPlatform.Samples.Shared.Logging;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Workbench;

/// <summary>
/// Coordinates pipeline mutations, undo/redo, execution, and preview away from the ViewModel layer.
/// </summary>
public sealed class WorkbenchOrchestrator
{
    private readonly PipelineRunner pipelineRunner;
    private readonly OperatorRegistry operatorRegistry;
    private readonly IImageCodec imageCodec;
    private readonly AsyncFileLogger logger;
    private readonly WorkbenchHistory<List<PipelineStep>> pipelineHistory = new();
    private readonly List<PipelineStep> pipeline = [];

    public WorkbenchOrchestrator(
        PipelineRunner pipelineRunner,
        OperatorRegistry operatorRegistry,
        IImageCodec imageCodec,
        AsyncFileLogger logger)
    {
        this.pipelineRunner = pipelineRunner;
        this.operatorRegistry = operatorRegistry;
        this.imageCodec = imageCodec;
        this.logger = logger;
    }

    public OperatorRegistry OperatorRegistry => operatorRegistry;

    public IReadOnlyList<PipelineStep> Steps => pipeline;

    public bool CanUndo => pipelineHistory.CanUndo;

    public bool CanRedo => pipelineHistory.CanRedo;

    public int StepCount => pipeline.Count;

    public int EnabledStepCount => pipeline.Count(step => step.IsEnabled);

    public void SetInitialSteps(IEnumerable<PipelineStep> steps)
    {
        pipeline.Clear();
        pipeline.AddRange(steps.Select(CloneStep));
    }

    public PipelineStep GetStep(int index) => pipeline[index];

    public PipelineStep AddStep(PipelineStep step, bool pushUndo = true)
    {
        if (pushUndo)
            PushUndoState();

        pipeline.Add(step);
        return step;
    }

    public DeleteStepResult? TryDeleteStep(int index)
    {
        if (index < 0 || index >= pipeline.Count)
            return null;

        PushUndoState();
        var removed = pipeline[index];
        pipeline.RemoveAt(index);
        return new DeleteStepResult(removed, index);
    }

    public MoveStepResult? TryMoveStep(int index, int delta)
    {
        var target = index + delta;
        if (index < 0 || target < 0 || target >= pipeline.Count)
            return null;

        PushUndoState();
        (pipeline[index], pipeline[target]) = (pipeline[target], pipeline[index]);
        return new MoveStepResult(index, target);
    }

    public ToggleStepResult? TryToggleStep(int index)
    {
        if (index < 0 || index >= pipeline.Count)
            return null;

        PushUndoState();
        var enabled = !pipeline[index].IsEnabled;
        pipeline[index] = pipeline[index] with { IsEnabled = enabled };
        return new ToggleStepResult(pipeline[index], enabled, index);
    }

    public void UpdateStep(int index, PipelineStep updated)
    {
        pipeline[index] = updated;
    }

    public bool TryUndo()
    {
        if (!pipelineHistory.CanUndo)
            return false;

        RestoreFromSnapshot(pipelineHistory.Undo(ClonePipeline(pipeline)));
        return true;
    }

    public bool TryRedo()
    {
        if (!pipelineHistory.CanRedo)
            return false;

        RestoreFromSnapshot(pipelineHistory.Redo(ClonePipeline(pipeline)));
        return true;
    }

    public void PushUndoState()
    {
        pipelineHistory.PushUndo(ClonePipeline(pipeline));
    }

    public void PushUndoSnapshot(IReadOnlyList<PipelineStep> snapshot)
    {
        pipelineHistory.PushUndo(snapshot.Select(CloneStep).ToList());
    }

    public void RestoreFromSnapshot(IReadOnlyList<PipelineStep> snapshot)
    {
        pipeline.Clear();
        pipeline.AddRange(snapshot.Select(CloneStep));
    }

    public WorkbenchPipelineRunResult RunPipeline(byte[]? imageBytes)
    {
        if (imageBytes is null)
            return WorkbenchPipelineRunResult.NoImage;

        try
        {
            using var source = imageCodec.Decode(imageBytes);
            using var runResult = pipelineRunner.Run(source, pipeline);
            if (!runResult.Succeeded || runResult.Output is null)
            {
                return WorkbenchPipelineRunResult.Failed(
                    runResult.FailedStepIndex,
                    runResult.Exception ?? new InvalidOperationException("处理流程运行失败。"));
            }

            var output = imageCodec.EncodePng(runResult.Output);
            return WorkbenchPipelineRunResult.Succeeded(output);
        }
        catch (Exception ex)
        {
            logger.Error("Pipeline run failed", ex);
            return WorkbenchPipelineRunResult.Failed(-1, ex);
        }
    }

    public WorkbenchPreviewResult Preview(
        byte[] imageBytes,
        string operatorName,
        int paramA,
        int paramB,
        bool clampOutput)
    {
        try
        {
            using var source = imageCodec.Decode(imageBytes);
            var op = operatorRegistry.FindByName(operatorName);
            if (op is null)
                throw new InvalidOperationException($"未知算子：{operatorName}");

            using var result = op.Apply(source, paramA, paramB, clampOutput);
            var output = imageCodec.EncodePng(result);
            return WorkbenchPreviewResult.Succeeded(output);
        }
        catch (Exception ex)
        {
            logger.Error("Preview failed", ex);
            return WorkbenchPreviewResult.Failed(ex);
        }
    }

    public PipelineStep CreateStep(
        IImageOperator? op,
        string operatorName,
        int paramA,
        int paramB,
        bool enabled,
        bool clamp = true)
    {
        var parameters = CreateParameterValues(op, paramA, paramB);
        return new PipelineStep(
            Guid.NewGuid(),
            op?.Descriptor.Id ?? operatorName,
            operatorName,
            parameters,
            enabled,
            clamp);
    }

    public static PipelineStep CloneStep(PipelineStep step)
    {
        return step with { Parameters = new Dictionary<string, int>(step.Parameters) };
    }

    public static List<PipelineStep> ClonePipeline(IReadOnlyList<PipelineStep> steps)
    {
        return steps.Select(CloneStep).ToList();
    }

    public static IReadOnlyDictionary<string, int> CreateParameterValues(IImageOperator? op, int paramA, int paramB)
    {
        var parameters = new Dictionary<string, int>();
        if (op?.Descriptor.PrimaryParameter is { } primary)
            parameters[primary.Key] = paramA;
        if (op?.Descriptor.SecondaryParameter is { } secondary)
            parameters[secondary.Key] = paramB;
        return parameters;
    }
}
