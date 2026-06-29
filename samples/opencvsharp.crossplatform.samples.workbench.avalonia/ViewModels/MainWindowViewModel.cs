using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Imaging;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Pipeline;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Ports;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Workbench;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Operators;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Services;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.ViewModels.Models;
using CvPoint = OpenCvSharp.Point;
using CvRect = OpenCvSharp.Rect;
using CvSize = OpenCvSharp.Size;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IImageFileDialogService fileDialogService;
    private readonly WorkbenchLogger logger;
    private readonly OperatorRegistry operatorRegistry = new();
    private readonly PipelineRunner pipelineRunner;
    private readonly WorkbenchHistory<List<PipelineStep>> pipelineHistory = new();
    private readonly IImageCodec imageCodec;
    private readonly List<PipelineStep> pipeline = [];
    private Bitmap? outputBitmap;
    private byte[]? outputBytes;
    private int failedPipelineStepIndex = -1;
    private Guid? inspectorEditStepId;
    private List<PipelineStep>? inspectorEditBaseline;
    private bool inspectorEditUndoPushed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputImageSource))]
    private ImageAssetViewModel? selectedAsset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParamAMinimum))]
    [NotifyPropertyChangedFor(nameof(ParamAMaximum))]
    [NotifyPropertyChangedFor(nameof(ParamBMinimum))]
    [NotifyPropertyChangedFor(nameof(ParamBMaximum))]
    [NotifyPropertyChangedFor(nameof(IsParamBOptionsVisible))]
    [NotifyPropertyChangedFor(nameof(IsStandardParamBVisible))]
    private OperatorViewModel? selectedOperator;

    [ObservableProperty]
    private PipelineNodeViewModel? selectedPipelineNode;

    [ObservableProperty]
    private string operatorSearchText = "";

    [ObservableProperty]
    private string runtimeText = "";

    [ObservableProperty]
    private string activeImageText = "未选择图像";

    [ObservableProperty]
    private string activeImageMetaText = "导入图像后开始。";

    [ObservableProperty]
    private string canvasTitleText = "无活动图像";

    [ObservableProperty]
    private string inputPlaceholderText = "导入图像后开始";

    [ObservableProperty]
    private string outputPlaceholderText = "处理结果将显示在这里";

    [ObservableProperty]
    private string imageInfoText = "未选择图像";

    [ObservableProperty]
    private string pipelineInfoText = "0/0 步";

    [ObservableProperty]
    private string pipelineSummaryText = "0 个步骤，0 个已启用";

    [ObservableProperty]
    private string statusText = "";

    [ObservableProperty]
    private string inspectorSubtitleText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParamAMinimum))]
    [NotifyPropertyChangedFor(nameof(ParamAMaximum))]
    private string paramANameText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParamBMinimum))]
    [NotifyPropertyChangedFor(nameof(ParamBMaximum))]
    [NotifyPropertyChangedFor(nameof(IsParamBOptionsVisible))]
    [NotifyPropertyChangedFor(nameof(IsStandardParamBVisible))]
    private string paramBNameText = "";

    private string paramAText = "128";
    private string paramBText = "32";
    private double paramAValue = 128;
    private double paramBValue = 32;

    [ObservableProperty]
    private bool isStepEnabled = true;

    [ObservableProperty]
    private bool isClampEnabled = true;

    [ObservableProperty]
    private bool isPreviewEnabled = true;

    [ObservableProperty]
    private bool inputImageVisible;

    [ObservableProperty]
    private bool outputImageVisible;

    [ObservableProperty]
    private bool inputPlaceholderVisible = true;

    [ObservableProperty]
    private bool outputPlaceholderVisible = true;

    [ObservableProperty]
    private Stretch imageStretch = Stretch.Uniform;

    [ObservableProperty]
    private string operatorDescriptionText = "";

    [ObservableProperty]
    private string nodeDetailsText = "";

    [ObservableProperty]
    private bool inspectorParametersVisible = true;

    [ObservableProperty]
    private bool inspectorOutputVisible = true;

    [ObservableProperty]
    private int selectedToolTabIndex;

    [ObservableProperty]
    private bool isPipelineCompact = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSideBySideMode))]
    [NotifyPropertyChangedFor(nameof(IsResultOnlyMode))]
    [NotifyPropertyChangedFor(nameof(IsSliderCompareMode))]
    private string canvasMode = CanvasModeSideBySide;

    private double compareBlend = 0.5;

    [ObservableProperty]
    private GridLength compareOutputWidth = new(0.5, GridUnitType.Star);

    [ObservableProperty]
    private GridLength compareInputWidth = new(0.5, GridUnitType.Star);

    private bool isSyncingParameters;
    private bool isSyncingPipelineSelection;
    private bool suppressOperatorSelection;
    private const string CanvasModeSideBySide = "SideBySide";
    private const string CanvasModeResultOnly = "ResultOnly";
    private const string CanvasModeSlider = "Slider";

    public MainWindowViewModel(IImageFileDialogService fileDialogService, IImageCodec imageCodec)
    {
        this.fileDialogService = fileDialogService;
        this.imageCodec = imageCodec;
        logger = new WorkbenchLogger(Path.Combine(AppContext.BaseDirectory, "logs"));
        pipelineRunner = new PipelineRunner(operatorRegistry);

        RuntimeText = $"OpenCV {Cv2.GetVersionString()}";

        SeedOperators();
        SeedWorkspace();
        RefreshOperatorList();
        RefreshPipelineNodes();
        RefreshCanvas();
        RunPipeline(false);
        SelectedToolTabIndex = 0;
        Log("工作台已就绪。");
    }

    public ObservableCollection<ImageAssetViewModel> Assets { get; } = [];

    public ObservableCollection<OperatorViewModel> FilteredOperators { get; } = [];

    public ObservableCollection<PipelineNodeViewModel> PipelineNodes { get; } = [];

    public ObservableCollection<ParameterOptionViewModel> ParamBOptions { get; } = [];

    public string ParamAText
    {
        get => paramAText;
        set
        {
            if (SetProperty(ref paramAText, value) && !isSyncingParameters)
                SetParamAValue(ReadParamText(value, ParamAValue, ParamAMinimum, ParamAMaximum));
        }
    }

    public string ParamBText
    {
        get => paramBText;
        set
        {
            if (SetProperty(ref paramBText, value) && !isSyncingParameters)
                SetParamBValue(ReadParamText(value, ParamBValue, 0, 255));
        }
    }

    public double ParamAValue
    {
        get => paramAValue;
        set => SetParamAValue(value);
    }

    public double ParamBValue
    {
        get => paramBValue;
        set => SetParamBValue(value);
    }

    public bool IsSideBySideMode => CanvasMode == CanvasModeSideBySide;

    public bool IsResultOnlyMode => CanvasMode == CanvasModeResultOnly;

    public bool IsSliderCompareMode => CanvasMode == CanvasModeSlider;

    public int ParamAMinimum => SelectedOperator is not null && operatorRegistry.FindByName(SelectedOperator.Name) is { } op ? op.ParamAMinimum : 0;

    public int ParamAMaximum => SelectedOperator is not null && operatorRegistry.FindByName(SelectedOperator.Name) is { } op ? op.ParamAMaximum : 255;

    public int ParamBMinimum => SelectedOperator is not null && operatorRegistry.FindByName(SelectedOperator.Name) is { } op ? op.ParamBMinimum : 0;

    public int ParamBMaximum => SelectedOperator is not null && operatorRegistry.FindByName(SelectedOperator.Name) is { } op ? op.ParamBMaximum : 255;

    public bool IsParamBOptionsVisible => ParamBOptions.Count > 0;

    public bool IsStandardParamBVisible => !IsParamBOptionsVisible;

    public double PipelinePaneHeight => IsPipelineCompact ? 126 : 188;

    public double CompareBlend
    {
        get => compareBlend;
        set
        {
            var normalized = Math.Clamp(value, 0, 1);
            if (!SetProperty(ref compareBlend, normalized))
                return;

            CompareInputWidth = new GridLength(normalized, GridUnitType.Star);
            CompareOutputWidth = new GridLength(1 - normalized, GridUnitType.Star);
        }
    }

    public Bitmap? InputImageSource => SelectedAsset?.Bitmap;

    public Bitmap? OutputImageSource => IsPreviewEnabled ? outputBitmap ?? SelectedAsset?.Bitmap : SelectedAsset?.Bitmap;

    public void Dispose()
    {
        logger.Dispose();
    }

    partial void OnSelectedAssetChanged(ImageAssetViewModel? value)
    {
        RunPipeline(false);
        if (value is not null)
            Log($"已选择 {value.Name}。");
    }

    partial void OnSelectedOperatorChanged(OperatorViewModel? value)
    {
        if (suppressOperatorSelection)
            return;

        if (SelectedPipelineNode is not null)
            SelectedPipelineNode = null;

        RefreshInspector();
        SelectedToolTabIndex = 1;
    }

    partial void OnSelectedPipelineNodeChanged(PipelineNodeViewModel? value)
    {
        ResetInspectorEditBaseline();
        foreach (var node in PipelineNodes)
            node.IsCurrent = ReferenceEquals(node, value);

        RefreshPipelineSummary();

        if (value is not null)
        {
            RefreshInspectorForPipelineNode(value);
            SelectedToolTabIndex = 1;
        }
    }

    partial void OnIsStepEnabledChanged(bool value)
    {
        UpdateSelectedPipelineStepFromInspector();
    }

    partial void OnIsClampEnabledChanged(bool value)
    {
        UpdateSelectedPipelineStepFromInspector();
    }

    partial void OnIsPipelineCompactChanged(bool value)
    {
        OnPropertyChanged(nameof(PipelinePaneHeight));
        RefreshPipelineNodes();
    }

    partial void OnOperatorSearchTextChanged(string value)
    {
        RefreshOperatorList();
    }

    partial void OnIsPreviewEnabledChanged(bool value)
    {
        RefreshCanvas();
    }

    [RelayCommand]
    private async Task ImportImageAsync()
    {
        var imageFile = await fileDialogService.OpenImageAsync();
        if (imageFile is null)
            return;

        try
        {
            var image = imageCodec.InspectPng(imageFile.Bytes);
            var bitmap = CreateBitmap(image.Bytes);
            var asset = new ImageAssetViewModel(
                Path.GetFileName(imageFile.Path),
                imageFile.Path,
                image.SizeText,
                "导入的图像",
                bitmap,
                image.Bytes);

            Assets.Add(asset);
            SelectedAsset = asset;
            Log($"已导入 {asset.Name}。");
        }
        catch (Exception ex)
        {
            LogError("导入失败。", ex);
        }
    }

    [RelayCommand]
    private async Task ExportResultAsync()
    {
        if (outputBytes is null || SelectedAsset is null)
        {
            LogWarning("没有可导出的结果。");
            return;
        }

        try
        {
            var fileName = await fileDialogService.SavePngAsync(
                $"{Path.GetFileNameWithoutExtension(SelectedAsset.Name)}-result.png",
                outputBytes);

            if (!string.IsNullOrWhiteSpace(fileName))
                Log($"已导出 {fileName}。");
        }
        catch (Exception ex)
        {
            LogError("导出失败。", ex);
        }
    }

    private void SeedOperators()
    {
        var operators = operatorRegistry.GetAll();
        var viewModels = operators.Select(op => new OperatorViewModel(
            op.Category,
            op.Name,
            op.Description,
            op.ParamAName,
            op.ParamBName,
            op.DefaultParamA,
            op.DefaultParamB,
            AddOperatorToPipeline
        )).ToList();

        foreach (var op in viewModels)
            FilteredOperators.Add(op);

        if (FilteredOperators.Count > 0)
            SelectedOperator = FilteredOperators[0];
    }

    private void SeedWorkspace()
    {
        var sample = CreateSampleAsset("sample-shapes.png");
        Assets.Add(sample);
        SelectedAsset = sample;

        pipeline.Add(CreateStep(GetOperator("Gaussian Blur"), 7, 0, true));
        pipeline.Add(CreateStep(GetOperator("Canny Edge"), 80, 160, true));
    }

    [RelayCommand]
    private void NewSample()
    {
        var asset = CreateSampleAsset($"sample-{Assets.Count + 1}.png");
        Assets.Add(asset);
        SelectedAsset = asset;
        Log($"已创建 {asset.Name}。");
    }

    private ImageAssetViewModel CreateSampleAsset(string name)
    {
        var image = CreateSampleImage();
        var bitmap = CreateBitmap(image.Bytes);
        return new ImageAssetViewModel(name, "生成", image.SizeText, "生成的示例", bitmap, image.Bytes);
    }

    [RelayCommand]
    private void DuplicateResult()
    {
        if (SelectedAsset is null || outputBytes is null)
            return;

        var bitmap = CreateBitmap(outputBytes);
        var duplicate = new ImageAssetViewModel(
            $"{Path.GetFileNameWithoutExtension(SelectedAsset.Name)} result.png",
            "处理结果",
            $"{bitmap.PixelSize.Width} x {bitmap.PixelSize.Height}",
            "复制自当前输出",
            bitmap,
            outputBytes);

        Assets.Add(duplicate);
        SelectedAsset = duplicate;
        Log($"已添加结果素材 {duplicate.Name}。");
    }

    private void AddOperatorToPipeline(OperatorViewModel? op)
    {
        if (op is null)
            return;

        SelectedToolTabIndex = 1;
        SelectedOperator = op;
        AddStep();
    }

    [RelayCommand]
    private void AddStep()
    {
        if (SelectedOperator is null)
            return;

        PushUndoState();
        var step = CreateStep(
            SelectedOperator,
            (int)Math.Round(ParamAValue),
            (int)Math.Round(ParamBValue),
            IsStepEnabled,
            IsClampEnabled);

        pipeline.Add(step);
        RefreshPipelineNodes(pipeline.Count);
        RunPipeline(false);
        Log($"已添加步骤 {pipeline.Count:00}: {step.OperatorName}。");
    }

    [RelayCommand]
    private void Preview()
    {
        if (SelectedAsset is null || SelectedOperator is null)
        {
            LogWarning("请先选择图像和算子再预览。");
            return;
        }

        try
        {
            failedPipelineStepIndex = -1;
            using var source = imageCodec.Decode(SelectedAsset.ImageBytes);
            var op = operatorRegistry.FindByName(SelectedOperator.Name);
            if (op is null)
                throw new InvalidOperationException($"未知算子：{SelectedOperator.Name}");

            using var result = op.Apply(source, (int)Math.Round(ParamAValue), (int)Math.Round(ParamBValue), IsClampEnabled);
            SetOutputFromMat(result);
            RefreshCanvas();
            Log($"已预览 {SelectedOperator.Name}。");
        }
        catch (Exception ex)
        {
            LogError("预览失败。", ex);
        }
    }

    [RelayCommand]
    private void DeleteStep()
    {
        var index = SelectedPipelineStepIndex();
        if (index < 0 || index >= pipeline.Count)
            return;

        PushUndoState();
        var removed = pipeline[index];
        pipeline.RemoveAt(index);
        RefreshPipelineNodes(Math.Min(index + 1, pipeline.Count));
        RunPipeline(false);
        Log($"已删除步骤 {index + 1:00}: {removed.OperatorName}。");
    }

    [RelayCommand]
    private void MoveUp()
    {
        MovePipelineStep(-1);
    }

    [RelayCommand]
    private void MoveDown()
    {
        MovePipelineStep(1);
    }

    [RelayCommand]
    private void TogglePipelineDensity()
    {
        IsPipelineCompact = !IsPipelineCompact;
        Log($"处理流程显示：{(IsPipelineCompact ? "紧凑" : "展开")}。");
    }

    private void MovePipelineStep(int delta)
    {
        var index = SelectedPipelineStepIndex();
        var target = index + delta;
        if (index < 0 || target < 0 || target >= pipeline.Count)
            return;

        PushUndoState();
        (pipeline[index], pipeline[target]) = (pipeline[target], pipeline[index]);
        RefreshPipelineNodes(target + 1);
        RunPipeline(false);
        Log($"已移动步骤到第 {target + 1} 位。");
    }

    [RelayCommand]
    private void RunPipeline()
    {
        RunPipeline(true);
    }

    private void RunPipeline(bool writeLog)
    {
        if (SelectedAsset is null)
        {
            outputBitmap = null;
            outputBytes = null;
            failedPipelineStepIndex = -1;
            RefreshCanvas();
            return;
        }

        try
        {
            failedPipelineStepIndex = -1;
            using var source = imageCodec.Decode(SelectedAsset.ImageBytes);
            using var runResult = pipelineRunner.Run(source, pipeline);
            if (!runResult.Succeeded || runResult.Output is null)
            {
                failedPipelineStepIndex = runResult.FailedStepIndex;
                throw runResult.Exception ?? new InvalidOperationException("处理流程运行失败。");
            }

            SetOutputFromMat(runResult.Output);

            RefreshCanvas();

            if (writeLog)
                Log($"已运行处理流程：{pipeline.Count(step => step.IsEnabled)} 个启用步骤。");
        }
        catch (Exception ex)
        {
            RefreshPipelineNodes();
            LogError("处理流程运行失败。", ex);
        }
    }

    [RelayCommand]
    private void SetParameterOption(string? valueText)
    {
        if (!int.TryParse(valueText, out var value) || value < ParamBMinimum || value > ParamBMaximum)
            return;

        SetParamBValue(value);
    }

    [RelayCommand]
    private void Undo()
    {
        if (!pipelineHistory.CanUndo)
        {
            LogWarning("没有可撤销的操作。");
            return;
        }

        RestorePipeline(pipelineHistory.Undo(ClonePipeline()));
        Log("已撤销处理流程编辑。");
    }

    [RelayCommand]
    private void Redo()
    {
        if (!pipelineHistory.CanRedo)
        {
            LogWarning("没有可重做的操作。");
            return;
        }

        RestorePipeline(pipelineHistory.Redo(ClonePipeline()));
        Log("已重做处理流程编辑。");
    }

    private void PushUndoState()
    {
        pipelineHistory.PushUndo(ClonePipeline());
    }

    private List<PipelineStep> ClonePipeline()
    {
        return pipeline.Select(CloneStep).ToList();
    }

    private void RestorePipeline(List<PipelineStep> snapshot)
    {
        pipeline.Clear();
        pipeline.AddRange(snapshot.Select(CloneStep));
        RefreshPipelineNodes();
        RunPipeline(false);
    }

    private void RefreshOperatorList()
    {
        suppressOperatorSelection = true;
        if (FilteredOperators.Count == 0)
        {
            suppressOperatorSelection = false;
            return;
        }

        var selectedName = SelectedOperator?.Name;
        var query = OperatorSearchText.Trim();
        var allOperators = operatorRegistry.GetAll()
            .Select(op => new OperatorViewModel(
                op.Category,
                op.Name,
                op.Description,
                op.ParamAName,
                op.ParamBName,
                op.DefaultParamA,
                op.DefaultParamB,
                AddOperatorToPipeline
            ))
            .ToList();

        var filtered = allOperators
            .Where(op => query.Length == 0 ||
                         op.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                         op.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        FilteredOperators.Clear();
        foreach (var op in filtered)
            FilteredOperators.Add(op);

        suppressOperatorSelection = false;
        SelectedOperator = FilteredOperators.FirstOrDefault(op => op.Name == selectedName) ?? FilteredOperators.FirstOrDefault();
    }

    private OperatorViewModel GetOperator(string name)
    {
        return FilteredOperators.First(op => op.Name == name);
    }

    private void RefreshInspector()
    {
        if (SelectedOperator is null)
            return;

        isSyncingPipelineSelection = true;
        InspectorParametersVisible = OperatorHasParameters(SelectedOperator.Name);
        InspectorOutputVisible = true;
        IsStepEnabled = true;
        IsClampEnabled = true;
        InspectorSubtitleText = SelectedOperator.Name;
        ParamANameText = SelectedOperator.ParamA;
        ParamBNameText = SelectedOperator.ParamB;
        SetBothParameterValues(SelectedOperator.DefaultA, SelectedOperator.DefaultB);
        RefreshParameterOptions();
        OperatorDescriptionText = SelectedOperator.Description;
        NodeDetailsText =
            $"类别：{SelectedOperator.Category}\n" +
            $"默认值：{FormatStepParameters(SelectedOperator.Name, SelectedOperator.DefaultA, SelectedOperator.DefaultB)}\n" +
            "预览：对当前图像运行该算子。";
        isSyncingPipelineSelection = false;
    }

    private void RefreshPipelineNodes(int selectedIndex = -1)
    {
        var previousIndex = selectedIndex >= 0 ? selectedIndex : SelectedPipelineNode?.Index ?? -1;
        var previousNode = SelectedPipelineNode;
        PipelineNodes.Clear();
        PipelineNodes.Add(PipelineNodeViewModel.CreateInput(0, SelectedAsset?.Name ?? "未选择图像", true, IsPipelineCompact));

        for (var i = 0; i < pipeline.Count; i++)
            PipelineNodes.Add(PipelineNodeViewModel.CreateStep(i + 1, i, CreateStepModel(i, pipeline[i]), true, i == failedPipelineStepIndex, IsPipelineCompact, TogglePipelineStep));

        PipelineNodes.Add(PipelineNodeViewModel.CreateResult(pipeline.Count + 1, failedPipelineStepIndex >= 0 ? "处理流程错误" : outputBitmap is null ? "未渲染" : "就绪", false, IsPipelineCompact));
        SelectedPipelineNode = PipelineNodes.FirstOrDefault(node => node.Index == previousIndex);
        if (SelectedPipelineNode is not null && previousNode is not null)
            RefreshInspectorForPipelineNode(SelectedPipelineNode);

        RefreshPipelineSummary();
    }

    private void RefreshInspectorForPipelineNode(PipelineNodeViewModel node)
    {
        isSyncingPipelineSelection = true;
        InspectorParametersVisible = false;
        InspectorOutputVisible = node.IsStep;
        InspectorSubtitleText = node.Title;
        OperatorDescriptionText = node.Subtitle;

        if (node.IsStep && node.StepIndex >= 0 && node.StepIndex < pipeline.Count)
        {
            var step = pipeline[node.StepIndex];
            CaptureInspectorEditBaseline(step.Id);
            var op = FilteredOperators.FirstOrDefault(item => item.Name == step.OperatorName);
            if (op is not null)
            {
                suppressOperatorSelection = true;
                SelectedOperator = op;
                suppressOperatorSelection = false;
            }

            InspectorParametersVisible = op is not null && OperatorHasParameters(op.Name);
            ParamANameText = op?.ParamA ?? "参数 A";
            ParamBNameText = op?.ParamB ?? "参数 B";
            SetBothParameterValues(GetPrimaryParameterValue(step), GetSecondaryParameterValue(step));
            RefreshParameterOptions();
            IsStepEnabled = step.IsEnabled;
            IsClampEnabled = step.ClampOutput;
            var parameterText = FormatStepParameters(step);
            OperatorDescriptionText = op?.Description ?? parameterText;
            NodeDetailsText =
                $"状态：{node.StatusText}\n" +
                $"参数：{parameterText}\n" +
                $"位置：第 {node.Index} 个，共 {pipeline.Count} 个";
        }
        else
        {
            ParamANameText = "";
            ParamBNameText = "";
            ParamBOptions.Clear();
            NodeDetailsText =
                $"状态：{node.StatusText}\n" +
                $"角色：{node.Title}";
        }

        isSyncingPipelineSelection = false;
    }

    private void CaptureInspectorEditBaseline(Guid stepId)
    {
        if (inspectorEditStepId == stepId && inspectorEditBaseline is not null)
            return;

        inspectorEditStepId = stepId;
        inspectorEditBaseline = ClonePipeline();
        inspectorEditUndoPushed = false;
    }

    private void ResetInspectorEditBaseline()
    {
        inspectorEditStepId = null;
        inspectorEditBaseline = null;
        inspectorEditUndoPushed = false;
    }

    private void RefreshCanvas()
    {
        OnPropertyChanged(nameof(InputImageSource));
        OnPropertyChanged(nameof(OutputImageSource));

        InputImageVisible = SelectedAsset is not null;
        OutputImageVisible = SelectedAsset is not null;
        InputPlaceholderVisible = SelectedAsset is null;
        OutputPlaceholderVisible = SelectedAsset is null || outputBitmap is null;
        OutputPlaceholderText = SelectedAsset is null ? "处理结果将显示在这里" : "运行或预览以更新结果";
        CanvasTitleText = SelectedAsset is null ? "无活动图像" : SelectedAsset.Name;
        StatusText = SelectedAsset is null ? "导入图像后开始。" : "就绪。";
        ImageInfoText = SelectedAsset?.SizeText ?? "未选择图像";
        ActiveImageText = SelectedAsset?.Name ?? "未选择图像";
        ActiveImageMetaText = SelectedAsset is null
            ? "导入图像后开始。"
            : $"{SelectedAsset.SizeText}\n{SelectedAsset.Source}";
        RefreshPipelineNodes();
    }

    private void RefreshPipelineSummary()
    {
        PipelineSummaryText = $"{pipeline.Count} 个步骤，{pipeline.Count(step => step.IsEnabled)} 个已启用";
        PipelineInfoText = $"{pipeline.Count(step => step.IsEnabled)}/{pipeline.Count} 步";
    }

    private void TogglePipelineStep(int index)
    {
        if (index < 0 || index >= pipeline.Count)
            return;

        PushUndoState();
        var enabled = !pipeline[index].IsEnabled;
        pipeline[index] = pipeline[index] with { IsEnabled = enabled };
        RefreshPipelineNodes(index + 1);
        RunPipeline(false);
        Log($"{pipeline[index].OperatorName} 已{(enabled ? "启用" : "禁用")}。");
    }

    private int SelectedPipelineStepIndex()
    {
        return SelectedPipelineNode?.StepIndex ?? -1;
    }

    private void SetParamAValue(double value)
    {
        var normalized = Math.Clamp((int)Math.Round(value), ParamAMinimum, ParamAMaximum);
        if (!SetProperty(ref paramAValue, normalized, nameof(ParamAValue)))
            return;

        isSyncingParameters = true;
        ParamAText = normalized.ToString();
        isSyncingParameters = false;
        UpdateSelectedPipelineStepFromInspector();
    }

    private void SetParamBValue(double value)
    {
        var normalized = Math.Clamp((int)Math.Round(value), ParamBMinimum, ParamBMaximum);
        if (!SetProperty(ref paramBValue, normalized, nameof(ParamBValue)))
            return;

        isSyncingParameters = true;
        ParamBText = normalized.ToString();
        isSyncingParameters = false;
        RefreshParameterOptionState();
        UpdateSelectedPipelineStepFromInspector();
    }

    private void SetBothParameterValues(int paramA, int paramB)
    {
        var normalizedA = Math.Clamp(paramA, ParamAMinimum, ParamAMaximum);
        var normalizedB = Math.Clamp(paramB, ParamBMinimum, ParamBMaximum);
        isSyncingParameters = true;
        SetProperty(ref paramAValue, (double)normalizedA, nameof(ParamAValue));
        SetProperty(ref paramBValue, (double)normalizedB, nameof(ParamBValue));
        ParamAText = normalizedA.ToString();
        ParamBText = normalizedB.ToString();
        isSyncingParameters = false;
        RefreshParameterOptionState();
    }

    private void RefreshParameterOptionState()
    {
        RefreshParameterOptions();
        OnPropertyChanged(nameof(IsParamBOptionsVisible));
        OnPropertyChanged(nameof(IsStandardParamBVisible));
    }

    private void RefreshParameterOptions()
    {
        var selectedValue = (int)Math.Round(ParamBValue);
        var op = SelectedOperator is null ? null : operatorRegistry.FindByName(SelectedOperator.Name);
        var options = op?.Descriptor.SecondaryParameter?.Options ?? [];

        ParamBOptions.Clear();
        foreach (var option in options)
        {
            var item = new ParameterOptionViewModel(option.Value, option.Label, value => SetParamBValue(value));
            item.IsSelected = option.Value == selectedValue;
            ParamBOptions.Add(item);
        }
    }

    private void UpdateSelectedPipelineStepFromInspector()
    {
        if (isSyncingPipelineSelection || SelectedPipelineNode?.StepIndex is not { } index || index < 0 || index >= pipeline.Count)
            return;

        var current = pipeline[index];
        var paramA = (int)Math.Round(ParamAValue);
        var paramB = (int)Math.Round(ParamBValue);
        var op = operatorRegistry.FindById(current.OperatorId) ?? operatorRegistry.FindByName(current.OperatorName);
        var updated = current with
        {
            Parameters = CreateParameterValues(op, paramA, paramB),
            IsEnabled = IsStepEnabled,
            ClampOutput = IsClampEnabled
        };

        if (updated == current)
            return;

        PushInspectorEditUndoState(current.Id);
        pipeline[index] = updated;
        failedPipelineStepIndex = -1;
        RefreshPipelineNodes(index + 1);
        RunPipeline(false);
    }

    private void PushInspectorEditUndoState(Guid stepId)
    {
        if (inspectorEditUndoPushed)
            return;

        if (inspectorEditStepId != stepId || inspectorEditBaseline is null)
            CaptureInspectorEditBaseline(stepId);

        pipelineHistory.PushUndo((inspectorEditBaseline ?? ClonePipeline()).Select(CloneStep).ToList());
        inspectorEditUndoPushed = true;
    }

    private string FormatStepParameters(string operatorName, int paramA, int paramB)
    {
        var op = operatorRegistry.FindByName(operatorName);
        if (op is null || !OperatorHasParameters(operatorName))
            return "无参数";

        return $"{op.ParamAName}={op.FormatParameterValue(op.ParamAName, paramA)}, " +
               $"{op.ParamBName}={op.FormatParameterValue(op.ParamBName, paramB)}";
    }

    private string FormatStepParameters(PipelineStep step)
    {
        var op = operatorRegistry.FindById(step.OperatorId) ?? operatorRegistry.FindByName(step.OperatorName);
        if (op is null || op.Descriptor.Parameters.Count == 0)
            return "无参数";

        return string.Join(", ", op.Descriptor.Parameters.Select(parameter =>
        {
            var value = step.GetParameter(parameter.Key, parameter.DefaultValue);
            return $"{parameter.DisplayName}={parameter.FormatValue(value)}";
        }));
    }

    private bool OperatorHasParameters(string operatorName)
    {
        return operatorRegistry.FindByName(operatorName)?.Descriptor.Parameters.Count > 0;
    }

    private static int ReadParamText(string? text, double currentValue, int min, int max)
    {
        return int.TryParse(text, out var value)
            ? Math.Clamp(value, min, max)
            : (int)Math.Round(currentValue);
    }

    private void SetImageStretch(Stretch stretch, string label)
    {
        ImageStretch = stretch;
        Log($"画布缩放：{label}。");
    }

    [RelayCommand]
    private void Fit()
    {
        SetImageStretch(Stretch.Uniform, "适应");
    }

    [RelayCommand]
    private void ActualSize()
    {
        SetImageStretch(Stretch.None, "100%");
    }

    [RelayCommand]
    private void SetCanvasMode(string? mode)
    {
        if (mode is not (CanvasModeSideBySide or CanvasModeResultOnly or CanvasModeSlider))
            return;

        CanvasMode = mode;
    }

    private void SetOutputFromMat(Mat result)
    {
        var image = imageCodec.EncodePng(result);
        outputBytes = image.Bytes;
        outputBitmap = CreateBitmap(outputBytes);
        OnPropertyChanged(nameof(OutputImageSource));
    }

    private PipelineStep CreateStep(OperatorViewModel op, int paramA, int paramB, bool enabled, bool clamp = true)
    {
        var imageOperator = operatorRegistry.FindByName(op.Name);
        var parameters = CreateParameterValues(imageOperator, paramA, paramB);

        return new PipelineStep(
            Guid.NewGuid(),
            imageOperator?.Descriptor.Id ?? op.Name,
            op.Name,
            parameters,
            enabled,
            clamp);
    }

    private PipelineStepModel CreateStepModel(int index, PipelineStep step)
    {
        return new PipelineStepModel(
            step.Id,
            (index + 1).ToString("00"),
            step.OperatorName,
            FormatStepParameters(step),
            step.IsEnabled,
            GetPrimaryParameterValue(step),
            GetSecondaryParameterValue(step),
            step.ClampOutput);
    }

    private static PipelineStep CloneStep(PipelineStep step)
    {
        return step with { Parameters = new Dictionary<string, int>(step.Parameters) };
    }

    private static IReadOnlyDictionary<string, int> CreateParameterValues(IImageOperator? op, int paramA, int paramB)
    {
        var parameters = new Dictionary<string, int>();
        if (op?.Descriptor.PrimaryParameter is { } primary)
            parameters[primary.Key] = paramA;
        if (op?.Descriptor.SecondaryParameter is { } secondary)
            parameters[secondary.Key] = paramB;
        return parameters;
    }

    private int GetPrimaryParameterValue(PipelineStep step)
    {
        var op = operatorRegistry.FindById(step.OperatorId) ?? operatorRegistry.FindByName(step.OperatorName);
        return op?.Descriptor.PrimaryParameter is { } parameter ? step.GetParameter(parameter.Key, parameter.DefaultValue) : 0;
    }

    private int GetSecondaryParameterValue(PipelineStep step)
    {
        var op = operatorRegistry.FindById(step.OperatorId) ?? operatorRegistry.FindByName(step.OperatorName);
        return op?.Descriptor.SecondaryParameter is { } parameter ? step.GetParameter(parameter.Key, parameter.DefaultValue) : 0;
    }

    private static Bitmap CreateBitmap(byte[] bytes)
    {
        return new Bitmap(new MemoryStream(bytes));
    }

    private void Log(string message)
    {
        logger.Info(message);
        StatusText = message;
    }

    private void LogWarning(string message)
    {
        logger.Warning(message);
        StatusText = message;
    }

    private void LogError(string message, Exception exception)
    {
        logger.Error(message, exception);
        StatusText = $"{message} {exception.Message}";
    }

    private ImageBuffer CreateSampleImage()
    {
        using var image = new Mat(new CvSize(1200, 760), MatType.CV_8UC3, new Scalar(246, 248, 251));
        Cv2.Rectangle(image, new CvRect(110, 110, 300, 240), new Scalar(62, 121, 224), -1);
        Cv2.Circle(image, new CvPoint(820, 270), 145, new Scalar(232, 96, 71), -1);
        Cv2.Line(image, new CvPoint(125, 540), new CvPoint(1060, 540), new Scalar(30, 41, 59), 10, LineTypes.AntiAlias);
        Cv2.Ellipse(image, new CvPoint(585, 340), new CvSize(220, 90), -10, 0, 360, new Scalar(72, 176, 120), 14, LineTypes.AntiAlias);
        Cv2.PutText(image, "Workbench Canvas", new CvPoint(100, 660), HersheyFonts.HersheySimplex, 1.8, new Scalar(22, 32, 46), 4, LineTypes.AntiAlias);
        return imageCodec.EncodePng(image);
    }
}
