using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Imaging;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Matching;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task ImportSourceAsync()
    {
        var image = await fileDialogService.OpenImageAsync("导入源图");
        if (image is null)
            return;

        imageSession.SetSource(image.Bytes);
        ReplaceSourceImage(AvaloniaImagePreview.FromBytes(image.Bytes));
        UpdateSourceSizeDisplay();
        SaveCurrentImages();
        ClearMatchResult();
        logger.Info($"Source imported. Size={SourcePixelWidth}×{SourcePixelHeight}, Bytes={image.Bytes.Length}");
    }

    [RelayCommand]
    private async Task ImportTemplateAsync()
    {
        var image = await fileDialogService.OpenImageAsync("导入模板图");
        if (image is null)
            return;

        imageSession.SetTemplate(image.Bytes);
        RefreshTemplatePreview();
        SaveCurrentImages();
        ClearMatchResult();
        logger.Info($"Template imported. Bytes={image.Bytes.Length}");
    }

    public void SetTemplateFromSourceRotatedRoi(RotatedRect roi)
    {
        var crop = imageSession.TryCropTemplateFromRotatedRoi(roi);
        if (crop is null)
            return;

        imageSession.SetTemplate(crop.TemplateBytes);
        RefreshTemplatePreview();
        SaveCurrentImages();
        StatusText = $"已从选区生成模板：横坐标={crop.X}，纵坐标={crop.Y}，宽={crop.Width}，高={crop.Height}。";
        ClearMatchResult();
        logger.Info($"Template from ROI. Rect=({crop.X},{crop.Y},{crop.Width},{crop.Height}), Angle={crop.Angle:F1}");
    }

    [RelayCommand]
    private async Task TrainTemplateAsync()
    {
        if (IsBusy)
            return;

        if (!matchOrchestrator.HasPair)
        {
            StatusText = "请先导入源图和模板图。";
            return;
        }

        if (Options.SelectedAlgorithm.Locator is not ContourTemplateLocator)
        {
            StatusText = "当前算法不需要训练模板。";
            return;
        }

        IsBusy = true;
        StatusText = "正在训练模板，请稍候...";
        var shouldRefreshMatch = false;

        try
        {
            var result = await matchOrchestrator.TrainContourAsync(
                Contour.Locator.CurrentSettings,
                Options.NmsOverlapThreshold);

            if (!result.IsSuccess || result.Result is null)
            {
                ClearMatchResult();
                StatusText = result.ErrorMessage ?? "模板训练失败，请调整选区或更换模板图。";
                return;
            }

            var training = result.Result;
            Contour.ApplySettings(training.Settings);
            Options.Threshold = training.SuggestedThreshold;
            SaveCurrentImages();
            StatusText = $"模板训练完成。轮廓 {training.TemplateContourCount}，候选 {training.CandidateCount}，匹配 {training.MatchCount}，建议阈值 {training.SuggestedThreshold:0.00}，最佳分数 {training.BestScore:0.0000}，Canny {training.Settings.CannyLowThreshold:0}/{training.Settings.CannyHighThreshold:0}，梯度 {training.Settings.GradientThresholdScale:0.00}，耗时 {training.Elapsed.TotalMilliseconds:0.0} 毫秒。";
            logger.Info($"Train completed. Contours={training.TemplateContourCount}, Candidates={training.CandidateCount}, Matches={training.MatchCount}, BestScore={training.BestScore:F4}, SuggestedThreshold={training.SuggestedThreshold:F2}, Canny={training.Settings.CannyLowThreshold:F0}/{training.Settings.CannyHighThreshold:F0}, Gradient={training.Settings.GradientThresholdScale:F2}, Elapsed={training.Elapsed.TotalMilliseconds:F1}ms");
            shouldRefreshMatch = hasRunMatch;
        }
        finally
        {
            IsBusy = false;
        }

        if (shouldRefreshMatch)
            await RunMatch();
    }

    [RelayCommand]
    private async Task RunMatch()
    {
        if (IsBusy)
            return;

        hasRunMatch = true;
        if (!matchOrchestrator.HasPair)
        {
            ClearMatchResult();
            return;
        }

        IsBusy = true;
        try
        {
            var locator = Options.CreateMatchLocator();
            var activeLocator = Options.SelectedAlgorithm.Locator;
            var outcome = await matchOrchestrator.RunMatchAsync(
                locator,
                activeLocator,
                Options.CreateOptions());

            switch (outcome.Status)
            {
                case MatchOrchestrationStatus.NoPair:
                    ClearMatchResult();
                    break;
                case MatchOrchestrationStatus.TemplateTooLarge:
                    ClearMatchResult();
                    StatusText = outcome.Message ?? "模板图必须小于源图。";
                    break;
                case MatchOrchestrationStatus.Failed:
                    ClearMatchResult();
                    StatusText = outcome.Message ?? "模板匹配失败，请检查源图、模板图和参数。";
                    break;
                case MatchOrchestrationStatus.Success when outcome.Result is not null:
                    var applyOutcome = Result.Apply(outcome.Result, Options.SelectedAlgorithm, Options.SelectedMethod);
                    StatusText = applyOutcome.StatusText;
                    if (applyOutcome.ProfileToLog is not null)
                        logger.LogProfile(applyOutcome.ProfileToLog);
                    Pyramid.Apply(outcome.Result);
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
