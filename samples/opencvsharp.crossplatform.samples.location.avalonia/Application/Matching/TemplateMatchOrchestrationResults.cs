using OpenCvSharp.CrossPlatform.Core;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Matching;

public enum MatchOrchestrationStatus
{
    Success,
    NoPair,
    TemplateTooLarge,
    Failed
}

public sealed record MatchOrchestrationResult(
    MatchOrchestrationStatus Status,
    TemplateLocatorResult? Result,
    string? Message = null)
{
    public static MatchOrchestrationResult NoPair { get; } = new(MatchOrchestrationStatus.NoPair, null);

    public static MatchOrchestrationResult Succeeded(TemplateLocatorResult result) =>
        new(MatchOrchestrationStatus.Success, result);

    public static MatchOrchestrationResult TemplateTooLarge(string message) =>
        new(MatchOrchestrationStatus.TemplateTooLarge, null, message);

    public static MatchOrchestrationResult Failed { get; } =
        new(MatchOrchestrationStatus.Failed, null, "模板匹配失败，请检查源图、模板图和参数。");
}

public sealed record TrainOrchestrationResult(
    bool IsSuccess,
    ContourTrainingResult? Result,
    string? ErrorMessage = null)
{
    public static TrainOrchestrationResult Succeeded(ContourTrainingResult result) =>
        new(true, result);

    public static TrainOrchestrationResult Failed(string message) =>
        new(false, null, message);
}
