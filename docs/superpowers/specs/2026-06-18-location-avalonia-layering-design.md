# opencvsharp.mac.samples.location.avalonia 项目分层设计

## 目标

以可维护性为优先目标，对 `opencvsharp.mac.samples.location.avalonia` 项目进行分层重构。建立清晰的文件夹结构，让代码易于理解和修改。

## 当前状态

16 个源文件全部平铺在根目录，无分层：

```
opencvsharp.mac.samples.location.avalonia/
├── App.axaml / App.axaml.cs
├── Program.cs
├── MainWindow.axaml / MainWindow.axaml.cs (~740 行)
├── MainWindowViewModel.cs (~900 行)
├── BenchmarkChartWindow.cs
├── BenchmarkDetailWindow.cs (~685 行，含 TimingDistributionChart 和 DetailedBenchmarkResult)
├── BenchmarkSummary.cs
├── PerformanceLogger.cs
├── TemplateMatchSettings.cs
├── TemplateMatchSettingsStore.cs
├── TemplateMatchViewModels.cs (3 个 record)
├── WindowImageFileDialogService.cs
├── MatchOverlayControl.cs
├── FractionToWidthConverter.cs
└── ImageFileResult.cs
```

主要问题：
- MainWindowViewModel.cs 近 900 行，混合了图像管理、匹配逻辑、基准测试、设置持久化
- MainWindow.axaml.cs 740 行，混合了 ROI 交互、画布缩放平移、UI 辅助逻辑
- BenchmarkDetailWindow.cs 内含独立的 TimingDistributionChart 控件和 DetailedBenchmarkResult 模型

## 目标结构

```
opencvsharp.mac.samples.location.avalonia/
├── App/
│   ├── App.axaml
│   ├── App.axaml.cs
│   └── Program.cs
│
├── Views/
│   ├── MainWindow.axaml
│   ├── MainWindow.axaml.cs
│   ├── BenchmarkChartWindow.cs
│   └── Controls/
│       ├── MatchOverlayControl.cs
│       └── TimingDistributionChart.cs
│
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   └── Models/
│       ├── TemplateLocatorViewModel.cs
│       ├── TemplateMatchMethodViewModel.cs
│       ├── MatchOverlayViewModel.cs
│       ├── BenchmarkSummary.cs
│       ├── DetailedBenchmarkResult.cs
│       ├── TemplateMatchSettings.cs
│       └── ImageFileResult.cs
│
├── Services/
│   ├── TemplateMatchSettingsStore.cs
│   ├── PerformanceLogger.cs
│   └── WindowImageFileDialogService.cs
│
├── Converters/
│   └── FractionToWidthConverter.cs
│
└── opencvsharp.mac.samples.location.avalonia.csproj
```

## 命名空间约定

| 目录 | 命名空间 |
|------|----------|
| `App/` | `OpenCvSharp.Mac.Samples.Location.Avalonia`（保持不变） |
| `Views/` | `OpenCvSharp.Mac.Samples.Location.Avalonia.Views` |
| `Views/Controls/` | `OpenCvSharp.Mac.Samples.Location.Avalonia.Controls` |
| `ViewModels/` | `OpenCvSharp.Mac.Samples.Location.Avalonia.ViewModels` |
| `ViewModels/Models/` | `OpenCvSharp.Mac.Samples.Location.Avalonia.ViewModels.Models` |
| `Services/` | `OpenCvSharp.Mac.Samples.Location.Avalonia.Services` |
| `Converters/` | `OpenCvSharp.Mac.Samples.Location.Avalonia.Converters` |

## 代码拆分

### BenchmarkDetailWindow.cs → 拆为 3 个文件

| 类 | 目标文件 | 目标目录 |
|---|---|---|
| `DetailedBenchmarkResult` | `DetailedBenchmarkResult.cs` | `ViewModels/Models/` |
| `TimingDistributionChart` | `TimingDistributionChart.cs` | `Views/Controls/` |
| `BenchmarkDetailWindow` | `BenchmarkDetailWindow.cs` | `Views/` |

### TemplateMatchViewModels.cs → 拆为 3 个文件

| 类 | 目标文件 | 目标目录 |
|---|---|---|
| `TemplateLocatorViewModel` | `TemplateLocatorViewModel.cs` | `ViewModels/Models/` |
| `TemplateMatchMethodViewModel` | `TemplateMatchMethodViewModel.cs` | `ViewModels/Models/` |
| `MatchOverlayViewModel` | `MatchOverlayViewModel.cs` | `ViewModels/Models/` |

## 迁移步骤

每步完成后项目可编译。

1. **创建目录结构** — `mkdir -p App Views/Controls ViewModels/Models Services Converters`
2. **搬迁基础设施** — `FractionToWidthConverter`, `ImageFileResult`, `TemplateMatchSettings`, `PerformanceLogger`, `TemplateMatchSettingsStore`, `WindowImageFileDialogService`
3. **拆分 TemplateMatchViewModels.cs** → 3 个文件到 `ViewModels/Models/`
4. **搬迁 BenchmarkSummary.cs** → `ViewModels/Models/`
5. **拆分 BenchmarkDetailWindow.cs** → 3 个文件
6. **搬迁 BenchmarkChartWindow.cs** → `Views/`
7. **搬迁 MatchOverlayControl.cs** → `Views/Controls/`
8. **搬迁 MainWindow.axaml + .cs** → `Views/`
9. **搬迁 MainWindowViewModel.cs** → `ViewModels/`
10. **搬迁 App 入口** → `App/`（App.axaml, App.axaml.cs, Program.cs）
11. **更新 .csproj** 中的 AXAML 引用路径
12. **验证构建** — `dotnet build`

## 约束

- 本次只做文件搬迁和命名空间调整，不重构内部逻辑
- MainWindowViewModel 与 MainWindow 之间的紧密耦合保持不变
- 不新增功能，不修改行为
