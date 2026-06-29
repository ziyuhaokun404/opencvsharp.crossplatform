using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Operators;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Pipeline;

public sealed class PipelineRunner
{
    private readonly OperatorRegistry operatorRegistry;

    public PipelineRunner(OperatorRegistry operatorRegistry)
    {
        this.operatorRegistry = operatorRegistry;
    }

    public PipelineRunResult Run(Mat source, IReadOnlyList<PipelineStep> steps)
    {
        var stepResults = new List<PipelineStepResult>();
        var result = source.Clone();

        try
        {
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (!step.IsEnabled)
                {
                    stepResults.Add(new PipelineStepResult(step.Id, step.OperatorName, true, TimeSpan.Zero, null));
                    continue;
                }

                var op = operatorRegistry.FindById(step.OperatorId) ?? operatorRegistry.FindByName(step.OperatorName);
                if (op is null)
                {
                    var ex = new InvalidOperationException($"未知算子：{step.OperatorName}");
                    stepResults.Add(new PipelineStepResult(step.Id, step.OperatorName, false, TimeSpan.Zero, ex.Message));
                    result.Dispose();
                    return new PipelineRunResult(null, stepResults, step.Id, i, ex);
                }

                var stopwatch = Stopwatch.StartNew();
                Mat processed;
                try
                {
                    processed = op.Apply(result, step.Parameters, new OperatorExecutionContext(step.ClampOutput));
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    stepResults.Add(new PipelineStepResult(step.Id, step.OperatorName, false, stopwatch.Elapsed, ex.Message));
                    result.Dispose();
                    return new PipelineRunResult(null, stepResults, step.Id, i, ex);
                }

                stopwatch.Stop();
                stepResults.Add(new PipelineStepResult(step.Id, step.OperatorName, false, stopwatch.Elapsed, null));
                result.Dispose();
                result = processed;
            }

            return new PipelineRunResult(result, stepResults, null, -1, null);
        }
        catch (Exception ex)
        {
            result.Dispose();
            return new PipelineRunResult(null, stepResults, null, -1, ex);
        }
    }
}
