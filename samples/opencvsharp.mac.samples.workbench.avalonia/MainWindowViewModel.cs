using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using CvPoint = OpenCvSharp.Point;
using CvRect = OpenCvSharp.Rect;
using CvSize = OpenCvSharp.Size;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IImageFileDialogService fileDialogService;
    private readonly WorkbenchLogger logger = new(Path.Combine(AppContext.BaseDirectory, "logs"));
    private readonly List<OperatorViewModel> operators = [];
    private readonly List<PipelineStepModel> pipeline = [];
    private readonly Stack<List<PipelineStepModel>> undoStack = [];
    private readonly Stack<List<PipelineStepModel>> redoStack = [];
    private Bitmap? outputBitmap;
    private byte[]? outputBytes;
    private int failedPipelineStepIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputImageSource))]
    private ImageAssetViewModel? selectedAsset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParamAMinimum))]
    [NotifyPropertyChangedFor(nameof(ParamAMaximum))]
    [NotifyPropertyChangedFor(nameof(ParamBMinimum))]
    [NotifyPropertyChangedFor(nameof(ParamBMaximum))]
    [NotifyPropertyChangedFor(nameof(IsBorderModeParameterVisible))]
    [NotifyPropertyChangedFor(nameof(IsInterpolationParameterVisible))]
    [NotifyPropertyChangedFor(nameof(IsStandardParamBVisible))]
    [NotifyPropertyChangedFor(nameof(IsBorderModeConstant))]
    [NotifyPropertyChangedFor(nameof(IsBorderModeReflect))]
    [NotifyPropertyChangedFor(nameof(IsInterpolationNearest))]
    [NotifyPropertyChangedFor(nameof(IsInterpolationLinear))]
    [NotifyPropertyChangedFor(nameof(IsInterpolationCubic))]
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
    [NotifyPropertyChangedFor(nameof(IsBorderModeParameterVisible))]
    [NotifyPropertyChangedFor(nameof(IsInterpolationParameterVisible))]
    [NotifyPropertyChangedFor(nameof(IsStandardParamBVisible))]
    [NotifyPropertyChangedFor(nameof(IsBorderModeConstant))]
    [NotifyPropertyChangedFor(nameof(IsBorderModeReflect))]
    [NotifyPropertyChangedFor(nameof(IsInterpolationNearest))]
    [NotifyPropertyChangedFor(nameof(IsInterpolationLinear))]
    [NotifyPropertyChangedFor(nameof(IsInterpolationCubic))]
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

    public MainWindowViewModel(IImageFileDialogService fileDialogService)
    {
        this.fileDialogService = fileDialogService;

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

    public int ParamAMinimum => SelectedOperator?.Name switch
    {
        "Gaussian Blur" or "Adaptive Threshold" or "Morphology Close" => 3,
        "Sharpen" => 5,
        "Resize" => 10,
        "Rotate" => -180,
        _ => 0
    };

    public int ParamAMaximum => SelectedOperator?.Name switch
    {
        "Gaussian Blur" => 61,
        "Adaptive Threshold" => 99,
        "Morphology Close" => 41,
        "Sharpen" => 300,
        "Resize" => 200,
        "Rotate" => 180,
        _ => 255
    };

    public int ParamBMinimum => SelectedOperator?.Name switch
    {
        "Binary Threshold" or "Morphology Close" => 1,
        "Adaptive Threshold" => -128,
        "Find Contours" => 10,
        "Sharpen" => 3,
        _ => 0
    };

    public int ParamBMaximum => SelectedOperator?.Name switch
    {
        "Gaussian Blur" => 50,
        "Adaptive Threshold" => 127,
        "Morphology Close" => 12,
        "Find Contours" => 5000,
        "Sharpen" => 41,
        "Resize" => 2,
        "Rotate" => 1,
        _ => 255
    };

    public bool IsBorderModeParameterVisible => SelectedOperator?.Name == "Rotate";

    public bool IsInterpolationParameterVisible => SelectedOperator?.Name == "Resize";

    public bool IsStandardParamBVisible => !IsBorderModeParameterVisible && !IsInterpolationParameterVisible;

    public bool IsBorderModeConstant => IsBorderModeParameterVisible && (int)Math.Round(ParamBValue) == 0;

    public bool IsBorderModeReflect => IsBorderModeParameterVisible && !IsBorderModeConstant;

    public bool IsInterpolationNearest => IsInterpolationParameterVisible && (int)Math.Round(ParamBValue) == 0;

    public bool IsInterpolationLinear => IsInterpolationParameterVisible && (int)Math.Round(ParamBValue) == 1;

    public bool IsInterpolationCubic => IsInterpolationParameterVisible && (int)Math.Round(ParamBValue) == 2;

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
            var bitmap = CreateBitmap(imageFile.Bytes);
            var asset = new ImageAssetViewModel(
                Path.GetFileName(imageFile.Path),
                imageFile.Path,
                $"{bitmap.PixelSize.Width} x {bitmap.PixelSize.Height}",
                "导入的图像",
                bitmap,
                imageFile.Bytes);

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
        operators.AddRange(
        [
            new OperatorViewModel("颜色", "Grayscale", "转换为单通道强度图。", "", "", 0, 0, AddOperatorToPipeline),
            new OperatorViewModel("平滑", "Gaussian Blur", "使用高斯核抑制高频噪声。", "核大小", "sigma ×10", 7, 0, AddOperatorToPipeline),
            new OperatorViewModel("边缘", "Canny Edge", "从灰度图中提取清晰边缘。", "低阈值", "高阈值", 80, 160, AddOperatorToPipeline),
            new OperatorViewModel("阈值", "Binary Threshold", "将像素划分为前景和背景。", "阈值", "最大值", 128, 255, AddOperatorToPipeline),
            new OperatorViewModel("阈值", "Adaptive Threshold", "用于光照不均场景的局部阈值。", "块大小", "C", 31, -2, AddOperatorToPipeline),
            new OperatorViewModel("形态学", "Morphology Close", "闭合二值区域中的小孔和间隙。", "核大小", "迭代次数", 7, 2, AddOperatorToPipeline),
            new OperatorViewModel("轮廓", "Find Contours", "检测连通轮廓并标注边界框。", "阈值", "最小面积", 128, 900, AddOperatorToPipeline),
            new OperatorViewModel("增强", "Sharpen", "基于反锐化掩模增强细节。", "强度", "模糊核", 120, 5, AddOperatorToPipeline),
            new OperatorViewModel("几何", "Resize", "按目标比例重采样图像。", "缩放 %", "插值方式", 75, 1, AddOperatorToPipeline),
            new OperatorViewModel("几何", "Rotate", "围绕图像中心旋转。", "角度", "边界模式", 18, 1, AddOperatorToPipeline)
        ]);

        foreach (var op in operators)
            FilteredOperators.Add(op);

        SelectedOperator = FilteredOperators[0];
    }

    private void SeedWorkspace()
    {
        var sample = CreateSampleAsset("sample-shapes.png");
        Assets.Add(sample);
        SelectedAsset = sample;

        pipeline.Add(CreateStep(GetOperator("Gaussian Blur"), 7, 0, true));
        pipeline.Add(CreateStep(GetOperator("Canny Edge"), 80, 160, true));
        RenumberPipeline();
    }

    [RelayCommand]
    private void NewSample()
    {
        var asset = CreateSampleAsset($"sample-{Assets.Count + 1}.png");
        Assets.Add(asset);
        SelectedAsset = asset;
        Log($"已创建 {asset.Name}。");
    }

    private static ImageAssetViewModel CreateSampleAsset(string name)
    {
        var bytes = CreateSampleImageBytes();
        var bitmap = CreateBitmap(bytes);
        return new ImageAssetViewModel(name, "生成", $"{bitmap.PixelSize.Width} x {bitmap.PixelSize.Height}", "生成的示例", bitmap, bytes);
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
        RenumberPipeline();
        RefreshPipelineNodes(pipeline.Count);
        RunPipeline(false);
        Log($"已添加步骤 {step.Number}: {step.Name}。");
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
            using var source = DecodeAsset(SelectedAsset);
            using var result = ApplyOperator(source, SelectedOperator.Name, (int)Math.Round(ParamAValue), (int)Math.Round(ParamBValue), IsClampEnabled);
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
        RenumberPipeline();
        RefreshPipelineNodes(Math.Min(index + 1, pipeline.Count));
        RunPipeline(false);
        Log($"已删除步骤 {removed.Number}: {removed.Name}。");
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
        RenumberPipeline();
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
            using var source = DecodeAsset(SelectedAsset);
            var result = source.Clone();
            try
            {
                for (var i = 0; i < pipeline.Count; i++)
                {
                    var step = pipeline[i];
                    if (!step.Enabled)
                        continue;

                    Mat processed;
                    try
                    {
                        processed = ApplyOperator(result, step.Name, step.ParamA, step.ParamB, step.Clamp);
                    }
                    catch
                    {
                        failedPipelineStepIndex = i;
                        throw;
                    }

                    result.Dispose();
                    result = processed;
                }

                SetOutputFromMat(result);
            }
            finally
            {
                result.Dispose();
            }

            RefreshCanvas();

            if (writeLog)
                Log($"已运行处理流程：{pipeline.Count(step => step.Enabled)} 个启用步骤。");
        }
        catch (Exception ex)
        {
            RefreshPipelineNodes();
            LogError("处理流程运行失败。", ex);
        }
    }

    private static Mat ApplyOperator(Mat source, string name, int paramA, int paramB, bool clamp)
    {
        return name switch
        {
            "Grayscale" => ApplyGrayscale(source),
            "Gaussian Blur" => ApplyGaussianBlur(source, paramA, paramB),
            "Canny Edge" => ApplyCanny(source, paramA, paramB),
            "Binary Threshold" => ApplyBinaryThreshold(source, paramA, paramB),
            "Adaptive Threshold" => ApplyAdaptiveThreshold(source, paramA, paramB),
            "Morphology Close" => ApplyMorphologyClose(source, paramA, paramB),
            "Find Contours" => ApplyFindContours(source, paramA, paramB),
            "Sharpen" => ApplySharpen(source, paramA, paramB, clamp),
            "Resize" => ApplyResize(source, paramA, paramB),
            "Rotate" => ApplyRotate(source, paramA, paramB),
            _ => throw new InvalidOperationException($"未知算子：{name}")
        };
    }

    private static Mat ApplyGrayscale(Mat source)
    {
        using var gray = ToGray(source);
        var result = new Mat();
        Cv2.CvtColor(gray, result, ColorConversionCodes.GRAY2BGR);
        return result;
    }

    private static Mat ApplyGaussianBlur(Mat source, int kernelValue, int sigmaValue)
    {
        var kernel = OddInRange(kernelValue, 3, 61);
        var sigma = Math.Max(0, sigmaValue) / 10.0;
        var result = new Mat();
        Cv2.GaussianBlur(source, result, new CvSize(kernel, kernel), sigma);
        return result;
    }

    private static Mat ApplyCanny(Mat source, int low, int high)
    {
        using var gray = ToGray(source);
        using var edges = new Mat();
        Cv2.Canny(gray, edges, Math.Min(low, high), Math.Max(low, high));
        var result = new Mat();
        Cv2.CvtColor(edges, result, ColorConversionCodes.GRAY2BGR);
        return result;
    }

    private static Mat ApplyBinaryThreshold(Mat source, int threshold, int maxValue)
    {
        using var gray = ToGray(source);
        using var thresholded = new Mat();
        Cv2.Threshold(gray, thresholded, threshold, Math.Max(1, maxValue), ThresholdTypes.Binary);
        var result = new Mat();
        Cv2.CvtColor(thresholded, result, ColorConversionCodes.GRAY2BGR);
        return result;
    }

    private static Mat ApplyAdaptiveThreshold(Mat source, int blockSizeValue, int cValue)
    {
        using var gray = ToGray(source);
        using var thresholded = new Mat();
        var blockSize = OddInRange(blockSizeValue, 3, 99);
        var c = Math.Clamp(cValue, -128, 127);
        Cv2.AdaptiveThreshold(gray, thresholded, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, blockSize, c);
        var result = new Mat();
        Cv2.CvtColor(thresholded, result, ColorConversionCodes.GRAY2BGR);
        return result;
    }

    private static Mat ApplyMorphologyClose(Mat source, int kernelValue, int iterationsValue)
    {
        using var gray = ToGray(source);
        using var thresholded = new Mat();
        Cv2.Threshold(gray, thresholded, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new CvSize(OddInRange(kernelValue, 3, 41), OddInRange(kernelValue, 3, 41)));
        using var closed = new Mat();
        Cv2.MorphologyEx(thresholded, closed, MorphTypes.Close, kernel, iterations: Math.Clamp(iterationsValue, 1, 12));

        var result = new Mat();
        Cv2.CvtColor(closed, result, ColorConversionCodes.GRAY2BGR);
        return result;
    }

    private static Mat ApplyFindContours(Mat source, int threshold, int minAreaValue)
    {
        using var gray = ToGray(source);
        using var thresholded = new Mat();
        Cv2.Threshold(gray, thresholded, threshold, 255, ThresholdTypes.Binary);
        Cv2.FindContours(thresholded, out CvPoint[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var result = source.Clone();
        var minArea = Math.Max(10, minAreaValue);
        foreach (var contour in contours.Where(contour => Cv2.ContourArea(contour) >= minArea))
        {
            var rect = Cv2.BoundingRect(contour);
            Cv2.Rectangle(result, rect, new Scalar(80, 190, 120), 4, LineTypes.AntiAlias);
        }

        return result;
    }

    private static Mat ApplySharpen(Mat source, int amountValue, int blurKernelValue, bool clamp)
    {
        var kernel = OddInRange(blurKernelValue, 3, 41);
        var amount = Math.Clamp(amountValue / 100.0, 0.05, 3.0);
        using var blurred = new Mat();
        Cv2.GaussianBlur(source, blurred, new CvSize(kernel, kernel), 0);

        var result = new Mat();
        Cv2.AddWeighted(source, 1.0 + amount, blurred, -amount, 0, result);

        if (!clamp)
            return result;

        var clamped = new Mat();
        result.ConvertTo(clamped, MatType.CV_8UC3);
        result.Dispose();
        return clamped;
    }

    private static Mat ApplyResize(Mat source, int scaleValue, int interpolationValue)
    {
        var scale = Math.Clamp(scaleValue, 10, 200) / 100.0;
        var interpolation = interpolationValue switch
        {
            0 => InterpolationFlags.Nearest,
            2 => InterpolationFlags.Cubic,
            _ => InterpolationFlags.Linear
        };

        var result = new Mat();
        Cv2.Resize(source, result, new CvSize(), scale, scale, interpolation);
        return result;
    }

    private static Mat ApplyRotate(Mat source, int angleValue, int borderModeValue)
    {
        var angle = Math.Clamp(angleValue, -180, 180);
        using var rotation = Cv2.GetRotationMatrix2D(new Point2f(source.Width / 2f, source.Height / 2f), angle, 1);
        var result = new Mat();
        var borderMode = borderModeValue == 0 ? BorderTypes.Constant : BorderTypes.Reflect101;
        Cv2.WarpAffine(source, result, rotation, source.Size(), InterpolationFlags.Linear, borderMode, Scalar.All(0));
        return result;
    }

    private static Mat ToGray(Mat source)
    {
        if (source.Channels() == 1)
            return source.Clone();

        var gray = new Mat();
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private static int OddInRange(int value, int min, int max)
    {
        var clamped = Math.Clamp(value, min, max);
        return clamped % 2 == 0 ? clamped + 1 <= max ? clamped + 1 : clamped - 1 : clamped;
    }

    [RelayCommand]
    private void SetBorderMode(string? mode)
    {
        SetParamBOption(mode, 0, 1);
    }

    [RelayCommand]
    private void SetInterpolationMode(string? mode)
    {
        SetParamBOption(mode, 0, 2);
    }

    private void SetParamBOption(string? mode, int min, int max)
    {
        if (!int.TryParse(mode, out var value) || value < min || value > max)
            return;

        SetParamBValue(value);
    }

    [RelayCommand]
    private void Undo()
    {
        if (undoStack.Count == 0)
        {
            LogWarning("没有可撤销的操作。");
            return;
        }

        redoStack.Push(ClonePipeline());
        pipeline.Clear();
        pipeline.AddRange(undoStack.Pop());
        RenumberPipeline();
        RefreshPipelineNodes();
        RunPipeline(false);
        Log("已撤销处理流程编辑。");
    }

    [RelayCommand]
    private void Redo()
    {
        if (redoStack.Count == 0)
        {
            LogWarning("没有可重做的操作。");
            return;
        }

        undoStack.Push(ClonePipeline());
        pipeline.Clear();
        pipeline.AddRange(redoStack.Pop());
        RenumberPipeline();
        RefreshPipelineNodes();
        RunPipeline(false);
        Log("已重做处理流程编辑。");
    }

    private void PushUndoState()
    {
        undoStack.Push(ClonePipeline());
        redoStack.Clear();
    }

    private List<PipelineStepModel> ClonePipeline()
    {
        return pipeline.Select(step => step with { }).ToList();
    }

    private void RenumberPipeline()
    {
        for (var i = 0; i < pipeline.Count; i++)
            pipeline[i] = pipeline[i] with { Number = (i + 1).ToString("00") };
    }

    private void RefreshOperatorList()
    {
        suppressOperatorSelection = true;
        if (operators.Count == 0)
        {
            suppressOperatorSelection = false;
            return;
        }

        var selectedName = SelectedOperator?.Name;
        var query = OperatorSearchText.Trim();
        var filtered = operators
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
            PipelineNodes.Add(PipelineNodeViewModel.CreateStep(i + 1, i, pipeline[i], true, i == failedPipelineStepIndex, IsPipelineCompact, TogglePipelineStep));

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
            var op = operators.FirstOrDefault(item => item.Name == step.Name);
            if (op is not null)
            {
                suppressOperatorSelection = true;
                SelectedOperator = op;
                suppressOperatorSelection = false;
            }

            InspectorParametersVisible = op is not null && OperatorHasParameters(op.Name);
            ParamANameText = op?.ParamA ?? "参数 A";
            ParamBNameText = op?.ParamB ?? "参数 B";
            SetBothParameterValues(step.ParamA, step.ParamB);
            IsStepEnabled = step.Enabled;
            IsClampEnabled = step.Clamp;
            OperatorDescriptionText = op?.Description ?? step.Parameters;
            NodeDetailsText =
                $"状态：{node.StatusText}\n" +
                $"参数：{step.Parameters}\n" +
                $"位置：第 {node.Index} 个，共 {pipeline.Count} 个";
        }
        else
        {
            ParamANameText = "";
            ParamBNameText = "";
            NodeDetailsText =
                $"状态：{node.StatusText}\n" +
                $"角色：{node.Title}";
        }

        isSyncingPipelineSelection = false;
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
        PipelineSummaryText = $"{pipeline.Count} 个步骤，{pipeline.Count(step => step.Enabled)} 个已启用";
        PipelineInfoText = $"{pipeline.Count(step => step.Enabled)}/{pipeline.Count} 步";
    }

    private void TogglePipelineStep(int index)
    {
        if (index < 0 || index >= pipeline.Count)
            return;

        PushUndoState();
        var enabled = !pipeline[index].Enabled;
        pipeline[index] = pipeline[index] with { Enabled = enabled };
        RenumberPipeline();
        RefreshPipelineNodes(index + 1);
        RunPipeline(false);
        Log($"{pipeline[index].Name} 已{(enabled ? "启用" : "禁用")}。");
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
        OnPropertyChanged(nameof(IsBorderModeConstant));
        OnPropertyChanged(nameof(IsBorderModeReflect));
        OnPropertyChanged(nameof(IsInterpolationNearest));
        OnPropertyChanged(nameof(IsInterpolationLinear));
        OnPropertyChanged(nameof(IsInterpolationCubic));
    }

    private void UpdateSelectedPipelineStepFromInspector()
    {
        if (isSyncingPipelineSelection || SelectedPipelineNode?.StepIndex is not { } index || index < 0 || index >= pipeline.Count)
            return;

        var current = pipeline[index];
        var paramA = (int)Math.Round(ParamAValue);
        var paramB = (int)Math.Round(ParamBValue);
        var updated = current with
        {
            Parameters = FormatStepParameters(current.Name, paramA, paramB),
            Enabled = IsStepEnabled,
            ParamA = paramA,
            ParamB = paramB,
            Clamp = IsClampEnabled
        };

        if (updated == current)
            return;

        pipeline[index] = updated;
        failedPipelineStepIndex = -1;
        RefreshPipelineNodes(index + 1);
        RunPipeline(false);
    }

    private string FormatStepParameters(string operatorName, int paramA, int paramB)
    {
        var op = operators.FirstOrDefault(item => item.Name == operatorName);
        if (!OperatorHasParameters(operatorName))
            return "无参数";

        return op is null
            ? $"参数 A={paramA}, 参数 B={paramB}"
            : $"{op.ParamA}={FormatParameterValue(operatorName, op.ParamA, paramA)}, " +
              $"{op.ParamB}={FormatParameterValue(operatorName, op.ParamB, paramB)}";
    }

    private static bool OperatorHasParameters(string operatorName)
    {
        return operatorName != "Grayscale";
    }

    private static string FormatParameterValue(string operatorName, string parameterName, int value)
    {
        return (operatorName, parameterName, value) switch
        {
            ("Resize", "插值方式", 0) => "最近邻",
            ("Resize", "插值方式", 1) => "线性",
            ("Resize", "插值方式", 2) => "三次",
            ("Rotate", "边界模式", 0) => "常量填充",
            ("Rotate", "边界模式", _) => "镜像边界",
            _ => value.ToString()
        };
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
        outputBytes = EncodePng(result);
        outputBitmap = CreateBitmap(outputBytes);
        OnPropertyChanged(nameof(OutputImageSource));
    }

    private static PipelineStepModel CreateStep(OperatorViewModel op, int paramA, int paramB, bool enabled, bool clamp = true)
    {
        var parameters = OperatorHasParameters(op.Name)
            ? $"{op.ParamA}={FormatParameterValue(op.Name, op.ParamA, paramA)}, " +
              $"{op.ParamB}={FormatParameterValue(op.Name, op.ParamB, paramB)}"
            : "无参数";

        return new PipelineStepModel("", op.Name, parameters, enabled, paramA, paramB, clamp);
    }

    private static Mat DecodeAsset(ImageAssetViewModel asset)
    {
        var mat = Cv2.ImDecode(asset.ImageBytes, ImreadModes.Color);
        if (mat.Empty())
            throw new InvalidOperationException("无法解码图像数据。");
        return mat;
    }

    private static byte[] EncodePng(Mat image)
    {
        Cv2.ImEncode(".png", image, out var bytes);
        return bytes;
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

    private static byte[] CreateSampleImageBytes()
    {
        using var image = new Mat(new CvSize(1200, 760), MatType.CV_8UC3, new Scalar(246, 248, 251));
        Cv2.Rectangle(image, new CvRect(110, 110, 300, 240), new Scalar(62, 121, 224), -1);
        Cv2.Circle(image, new CvPoint(820, 270), 145, new Scalar(232, 96, 71), -1);
        Cv2.Line(image, new CvPoint(125, 540), new CvPoint(1060, 540), new Scalar(30, 41, 59), 10, LineTypes.AntiAlias);
        Cv2.Ellipse(image, new CvPoint(585, 340), new CvSize(220, 90), -10, 0, 360, new Scalar(72, 176, 120), 14, LineTypes.AntiAlias);
        Cv2.PutText(image, "Workbench Canvas", new CvPoint(100, 660), HersheyFonts.HersheySimplex, 1.8, new Scalar(22, 32, 46), 4, LineTypes.AntiAlias);
        return EncodePng(image);
    }
}

public interface IImageFileDialogService
{
    Task<ImageFileResult?> OpenImageAsync();

    Task<string?> SavePngAsync(string suggestedFileName, byte[] bytes);
}

public sealed record ImageFileResult(string Path, byte[] Bytes);

public sealed record ImageAssetViewModel(string Name, string Source, string SizeText, string Note, Bitmap Bitmap, byte[] ImageBytes);

public sealed class OperatorViewModel
{
    public OperatorViewModel(
        string category,
        string name,
        string description,
        string paramA,
        string paramB,
        int defaultA,
        int defaultB,
        Action<OperatorViewModel> addAction)
    {
        Category = category;
        Name = name;
        Description = description;
        ParamA = paramA;
        ParamB = paramB;
        DefaultA = defaultA;
        DefaultB = defaultB;
        CategoryDisplay = category.ToUpperInvariant();
        AddCommand = new RelayCommand(() => addAction(this));
    }

    public string Category { get; }

    public string CategoryDisplay { get; }

    public string Name { get; }

    public string Description { get; }

    public string ParamA { get; }

    public string ParamB { get; }

    public int DefaultA { get; }

    public int DefaultB { get; }

    public ICommand AddCommand { get; }
}

public sealed class PipelineNodeViewModel : ObservableObject
{
    private PipelineNodeViewModel(
        int index,
        int stepIndex,
        bool isStep,
        string nodeKind,
        string title,
        string subtitle,
        bool enabled,
        bool hasError,
        bool isCompact,
        bool hasNext,
        Action<int>? toggleAction)
    {
        Index = index;
        StepIndex = stepIndex;
        IsStep = isStep;
        NodeKind = nodeKind;
        Title = title;
        Subtitle = subtitle;
        Enabled = enabled;
        HasError = hasError;
        HasNext = hasNext;
        IsCompact = isCompact;
        var stateColor = StateColor(nodeKind, enabled, hasError, subtitle);
        AccentBrush = new SolidColorBrush(stateColor);
        StatusSurfaceBrush = new SolidColorBrush(Color.FromArgb(34, stateColor.R, stateColor.G, stateColor.B));
        StatusBorderBrush = new SolidColorBrush(Color.FromArgb(96, stateColor.R, stateColor.G, stateColor.B));
        StatusTextBrush = new SolidColorBrush(stateColor);
        StatusText = hasError ? "错误" : isStep ? enabled ? "已启用" : "已禁用" : nodeKind == "Input" ? "输入" : subtitle;
        ToggleText = enabled ? "开" : "关";
        ToggleCommand = new RelayCommand(() => toggleAction?.Invoke(stepIndex));
    }

    private bool isCurrent;

    public int Index { get; }

    public int StepIndex { get; }

    public bool IsStep { get; }

    public string NodeKind { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public bool Enabled { get; }

    public bool HasError { get; }

    public bool HasNext { get; }

    public bool IsCompact { get; }

    public IBrush AccentBrush { get; }

    public IBrush StatusSurfaceBrush { get; }

    public IBrush StatusBorderBrush { get; }

    public IBrush StatusTextBrush { get; }

    public string StatusText { get; }

    public string ToggleText { get; }

    public int SwitchKnobColumn => Enabled ? 1 : 0;

    public ICommand ToggleCommand { get; }

    public bool IsCurrent
    {
        get => isCurrent;
        set
        {
            if (!SetProperty(ref isCurrent, value))
                return;

            OnPropertyChanged(nameof(CardBorderBrush));
            OnPropertyChanged(nameof(CardBorderThickness));
        }
    }

    public IBrush CardBorderBrush => IsCurrent ? new SolidColorBrush(Color.Parse("#2563EB")) : StatusBorderBrush;

    public Thickness CardBorderThickness => IsCurrent ? new Thickness(2) : new Thickness(1);

    public bool IsSubtitleVisible => !IsCompact;

    public bool IsToggleVisible => IsStep;

    public double CardHeight => IsCompact ? 58 : 104;

    public Thickness CardPadding => IsCompact ? new Thickness(10, 7) : new Thickness(10);

    public static PipelineNodeViewModel CreateInput(int index, string subtitle, bool hasNext, bool isCompact)
    {
        return new PipelineNodeViewModel(index, -1, false, "Input", "输入", subtitle, true, false, isCompact, hasNext, null);
    }

    public static PipelineNodeViewModel CreateResult(int index, string subtitle, bool hasNext, bool isCompact)
    {
        return new PipelineNodeViewModel(index, -1, false, "Result", "结果", subtitle, subtitle == "就绪", subtitle == "处理流程错误", isCompact, hasNext, null);
    }

    public static PipelineNodeViewModel CreateStep(int index, int stepIndex, PipelineStepModel step, bool hasNext, bool hasError, bool isCompact, Action<int> toggleAction)
    {
        return new PipelineNodeViewModel(index, stepIndex, true, hasError ? "Error" : step.Enabled ? "Enabled" : "Disabled", $"{step.Number}  {step.Name}", step.Parameters, step.Enabled, hasError, isCompact, hasNext, toggleAction);
    }

    private static Color StateColor(string nodeKind, bool enabled, bool hasError, string subtitle)
    {
        if (hasError)
            return Color.Parse("#DC2626");

        return nodeKind switch
        {
            "Input" => Color.Parse("#2563EB"),
            "Enabled" => Color.Parse("#16A34A"),
            "Disabled" => Color.Parse("#94A3B8"),
            "Result" when subtitle == "就绪" => Color.Parse("#16A34A"),
            _ => enabled ? Color.Parse("#16A34A") : Color.Parse("#94A3B8")
        };
    }
}

public sealed record PipelineStepModel(string Number, string Name, string Parameters, bool Enabled, int ParamA, int ParamB, bool Clamp);
