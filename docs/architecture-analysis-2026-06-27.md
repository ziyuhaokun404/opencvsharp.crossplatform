# OpenCvSharp Cross-Platform 项目架构分析与优化建议

> 分析日期：2026-06-27  
> 分析范围：项目整体架构、分层、代码组织、构建系统

---

## 一、整体架构概览

项目是一个 **跨平台（macOS + Windows）的 OpenCV 封装工作区**，采用 mono-repo 模式组织：

```
opencvsharp.crossplatform/
├── .gitignore                  # 忽略 build/, bin/obj/, *.log, IDE, *.dylib, CMake 产物
├── Directory.Build.props       # MSBuild 本地原生运行时路径解析配置
├── Directory.Build.targets     # MSBuild 目标：将原生 dylib 复制到输出目录
├── OpenCvSharp.slnx            # 方案文件（新版 .slnx XML 格式）
├── README.md                   # 中文项目概览
│
├── build/                      # 原生构建产出（gitignored）
│   ├── native/osx-arm64/       ← 阶段性 dylib 目标位置
│   └── opencvsharp-extern-4.13/← CMake 构建树
│
├── docs/
│   └── plan/
│       └── 2026-06-18-workbench-refactoring-plan.md   ← 工作台分层重构计划
│
├── ref/
│   └── opencvsharp-4.13/       ← vendored C++ extern 源码（原生库 C++ 层）
│       └── src/opencvsharpextern/  ← ~120 个 .cpp/.h 文件（OpenCvSharpExtern P/Invoke 层）
│
├── scripts/                    # 三个原生运行时构建 shell 脚本
│   ├── build-native-runtime-macos.sh    # CMake 构建链
│   ├── stage-native-runtime.sh           # dylib 复制到 build/native/{RID}/
│   └── bundle-native-runtime-macos.sh    # 自包含 bundle（递归复制 Homebrew 依赖 + install_name_tool 重写 + 签名）
│
├── src/
│   └── opencvsharp.crossplatform.core/    ← 唯一核心库（C# 算法引擎）
│       ├── opencvsharp.crossplatform.core.csproj
│       ├── TemplateLocators.cs             # ~1925 行（核心算法主力）
│       ├── ContourModels.cs
│       ├── ContourCaches.cs
│       ├── ImageHelpers.cs
│       ├── MatchCandidateUtilities.cs      # NMS（非极大值抑制）算法
│       ├── TemplateLocatorModels.cs
│       └── PerformanceProfiler.cs
│
└── samples/
    ├── opencvsharp.crossplatform.samples.shared/        # 共享工具库（原生运行时解析 + 跨样本服务）
    │   ├── OpenCvSharpNativeRuntime.cs
    │   ├── Logging/AsyncFileLogger.cs
    │   ├── Services/IImageFileDialogService.cs, WindowImageFileDialogService.cs
    │   └── opencvsharp.crossplatform.samples.shared.csproj
    │
    ├── opencvsharp.crossplatform.samples.console/       # 控制台演示
    │   ├── Program.cs
    │   └── opencvsharp.crossplatform.samples.console.csproj
    │
    ├── opencvsharp.crossplatform.samples.location.avalonia/   ← 模板匹配可视化器 + 基准测试
    │   ├── Program.cs, App.axaml(.cs), app.manifest
    │   ├── Application/          # 图像会话、匹配执行、基准测试
    │   ├── Presentation/         # 结果映射 + Profiling UI（ProfileStepViewModel, ProfilePresentationMapper）
    │   ├── Converters/            # FractionToWidthConverter
    │   ├── Services/             # PerformanceLogger, TemplateMatchSettingsStore, WindowImageFileDialogService
    │   ├── ViewModels/
    │   │   ├── MainWindowViewModel.cs  # ~906 行
    │   │   └── Models/                # 7 个 model 类型（record）
    │   ├── Views/
    │   │   ├── MainWindow.axaml(.cs)  # ~740 行代码-behind（ROI 拖拽、缩放、渲染）
    │   │   ├── Controls/              # MatchOverlayControl, TimingDistributionChart
    │   │   ├── BenchmarkChartWindow.cs
    │   │   └── BenchmarkDetailWindow.cs
    │   └── opencvsharp.crossplatform.samples.location.avalonia.csproj
    │
    └── opencvsharp.crossplatform.samples.workbench.avalonia/   ← 图像处理工作台（进行中 DDD 重构）
        ├── App/
        ├── Application/          # 用例层（PipelineRunner, ImageBuffer, WorkbenchHistory）
        │   ├── Imaging/          # ImageBuffer
        │   ├── Pipeline/         # PipelineRunner, PipelineRunResult, PipelineStep, PipelineStepResult
        │   ├── Ports/            # IImageCodec（端口接口）
        │   └── Workbench/        # WorkbenchHistory（撤销/重做栈）
        ├── Domain/               # 领域层
        │   ├── Operators/        # IImageOperator、OperatorRegistry、BuiltIn/（10 个算子）
        │   │   └── BuiltIn/      # Grayscale, Blur, Canny, Threshold, Resize, Rotate 等
        │   └── Shared/           # ValueRange
        ├── Infrastructure/       # 基础设施层
        │   └── OpenCv/           # OpenCvImageCodec（IImageCodec 实现）
        │
        ├── Services/             # WorkbenchLogger（继承 AsyncFileLogger）
        ├── ViewModels/
        │   ├── MainWindowViewModel.cs  # ~1105 行（重构目标：降至 250-400 行）
        │   └── Models/               # ImageAsset, Operator, Parameter, PipelineNode
        ├── Views/               # MainWindow.axaml(.cs)
        └── opencvsharp.crossplatform.samples.workbench.avalonia.csproj
```

---

## 二、分层与关注点分离

| 层次 | 项目/目录 | 当前状态 | 评价 |
|------|----------|---------|------|
| Platform / Native | `scripts/`, `ref/` | CMake + Homebrew + shell 脚本 | ✅ 完整，CMake 构建链清晰 |
| Core Algorithm | `src/opencvsharp.crossplatform.core` | Strategy 模式，7 个文件，无子命名空间 | ⚠️ 功能完整但 1925 行巨型文件未拆分 |
| Domain | `samples.workbench.avalonia/Domain/` | 部分存在（Operators, ValueRange） | ⚠️ DDD 重构进行中 |
| Application | `samples.workbench.avalonia/Application/` | 部分存在（PipelineRunner, ImageBuffer） | ⚠️ 存在但 ViewModel 仍直接调用底层 |
| Infrastructure | `samples.workbench.avalonia/Infrastructure/` | 部分存在（OpenCvImageCodec） | ⚠️ 仅一个实现 |
| Presentation | `location` + `workbench` samples | Avalonia UI，无 DI 容器 | ⚠️ MVVM 框架用但未用容器 |

### 依赖方向（当前）

```
opencvsharp.crossplatform.core ──────────→ OpenCvSharp4 NuGet
       │                                    (+ Windows: OpenCvSharp4.runtime.win NuGet)
       │                                    (+ macOS: 自编译 libOpenCvSharpExtern.dylib)
       ↑
opencvsharp.crossplatform.samples.location.avalonia ──→ core + samples.shared + Avalonia
opencvsharp.crossplatform.samples.workbench.avalonia → samples.shared (无 core 引用)
opencvsharp.crossplatform.samples.console → samples.shared (无 core 引用)
opencvsharp.crossplatform.samples.shared (独立，无其他 reference)
```

✅ 依赖方向正确（Core → Samples），无循环引用。

---

## 三、项目与目标框架

| 项目 | 路径 | 输出类型 | 目标框架 | 根命名空间 |
|------|------|---------|---------|-----------|
| `opencvsharp.crossplatform.core` | `src/opencvsharp.crossplatform.core/` | Library | net10.0 | `OpenCvSharp.CrossPlatform.Core` |
| `opencvsharp.crossplatform.samples.shared` | `samples/...shared/` | Library | net10.0 | file-scoped |
| `opencvsharp.crossplatform.samples.console` | `samples/...console/` | Exe | net10.0 | file-scoped |
| `opencvsharp.crossplatform.samples.location.avalonia` | `samples/...location.avalonia/` | WinExe | net10.0 | `OpenCvSharp.CrossPlatform.Samples.Location.Avalonia` |
| `opencvsharp.crossplatform.samples.workbench.avalonia` | `samples/...workbench.avalonia/` | WinExe | net10.0 | file-scoped |

所有项目均目标 `net10.0`，通过根目录 `Directory.Packages.props` 中央管理包版本；`OpenCvSharp4` 当前为 `4.13.0.20260528`。

---

## 四、核心模式总结

### 4.1 Strategy 模式（唯一正式 OO 抽象）

```csharp
public interface ITemplateLocator
{
    string Name { get; }
    TemplateLocatorResult Locate(Mat source, Mat template, TemplateLocatorOptions options);
}
```

两个实现：
- `MatchTemplateLocator` — 像素级模板匹配，含金字塔降采样、SIMD 向量化候选提取（Vector256/Vector128）、patch 共识精化、NMS、角度估计
- `ContourTemplateLocator` — 轮廓特征匹配，含自动训练（Train() 方法）、距离变换评分、膨胀局部最大值检测、轮廓金字塔缓存

### 4.2 Record Types 作为主数据建模模式

几乎所有数据类都是 `sealed record` 或 `readonly record struct`，利用 C# 9+ record 特性实现不可变性、相等性、`with` 表达式和析构。

### 4.3 手动构造函数注入（无 DI 容器）

```csharp
// MainWindow.axaml.cs
viewModel = new MainWindowViewModel(new WindowImageFileDialogService(this));
```

两个 Avalonia 项目均无 DI 容器。CommunityToolkit.Mvvm 提供样板 MVVM（[ObservableProperty]、[RelayCommand]）。

### 4.4 异步 Channel 日志（跨项目重复）

`PerformanceLogger`（Location）和 `WorkbenchLogger`（Workbench）使用完全相同的模式：
- `Channel<string>.CreateUnbounded()` 非阻塞写入队列
- `Task.Run(WriteLoopAsync)` 后台写入器
- 14 天保留期启动清理
- 每日日志文件分割

### 4.5 原生运行时自定义解析

`OpenCvSharpNativeRuntime.Register()` 通过 `NativeLibrary.SetDllImportResolver` 拦截所有 `DllImport("OpenCvSharpExtern")` 调用，优先级：(1) `OPENCVSHARP_EXTERN_PATH` 环境变量 → (2) 本地 fallback `AppContext.BaseDirectory/libOpenCvSharpExtern.dylib`。

---

## 五、关键问题识别

### 问题 1：测试覆盖仍不足

**严重程度：中**（已从「零覆盖」持续改善）

- 已有 `tests/opencvsharp.crossplatform.core.tests`（35 个 xUnit 用例），覆盖 NMS/IoU、轮廓缓存、性能分析、基准汇总统计等
- 已新增定位器集成测试（`TemplateLocatorIntegrationTests`：`MatchTemplateLocator` + `ContourTemplateLocator`）及大网格 NMS 回归（`NmsGridTests`，>256 格对比暴力参考实现）
- 1925 行的 `TemplateLocators.cs` 仍缺少 SIMD、金字塔等深层分支的专项测试
- 建议继续补充边界用例与回归测试

### 问题 2：巨型文件未拆分

**严重程度：中**

- `TemplateLocators.cs` 1925 行——包含两个 Strategy 实现 + SIMD 优化 + 金字塔 + 精化 + 角度估计
- `MainWindowViewModel.cs`（Location）906 行 +（Workbench）1105 行——混合 UI 状态 + 业务逻辑 + 历史管理

### 问题 3：包版本硬编码重复

**严重程度：低**（P0-1 已缓解）

- `Directory.Packages.props` 已集中管理 `OpenCvSharp4`（`4.13.0.20260528`）、Avalonia、xUnit 等版本；各 csproj 使用无版本号的 `<PackageReference>`
- 升级包仍只需编辑一处，但新增项目/包时需记得在 `Directory.Packages.props` 登记

### 问题 4：重构进行中但未完成

**严重程度：中**

- Workbench 项目有 `docs/plan/2026-06-18-workbench-refactoring-plan.md` 计划（DDD 分层），但命令行 `Operators/` 与 `Domain/Operators/` 并存——旧代码未清理
- `MainWindowViewModel` 仍混合 Application/Presentation 职责（计划目标 250-400 行，当前 1105 行）

### 问题 5：跨项目重复代码

**严重程度：低-中**

- `WindowImageFileDialogService` 在两个 Avalonia 项目中各有一份，Workbench 有接口而 Location 没有
- `PerformanceLogger` 和 `WorkbenchLogger` 使用完全相同的异步日志模式

### 问题 6：无 CI/CD

**严重程度：低**

-  `.github/workflows/` 不存在
- 原生运行时构建（CMake + Homebrew）完全手动

---

## 六、优化建议（按优先级排序）

### P0 — 立即改进（低风险、高收益）

> **状态（2026-06-29）**：P0-1、P0-2 及下列 Location 样本修补均已完成。

#### [P0-1] 引入 `Directory.Packages.props` 集中包版本管理 ✅

- **问题**：所有 5 个项目硬编码相同包版本，升级需编辑 5 个文件
- **完成**：已创建 `Directory.Packages.props`（`ManagePackageVersionsCentrally`），各 csproj 改为无版本号 `<PackageReference>`；`OpenCvSharp4` 已对齐至 `4.13.0.20260528`

#### [P0-2] 为 `opencvsharp.crossplatform.core` 添加单元测试项目 ✅

- **问题**：测试覆盖仍不足，复杂算法分支缺少专项用例
- **完成**：`tests/opencvsharp.crossplatform.core.tests/` 现有 **35** 个 xUnit 用例，覆盖：
  - `MatchCandidateUtilities` / `NmsTests`：IoU、NMS 去重
  - `NmsGridTests`：>256 网格 NMS 与暴力参考实现一致性
  - `ContourResponseCacheTests`：缓存命中/未命中
  - `PerformanceProfileTests`：性能分析数据结构
  - `TemplateLocatorIntegrationTests`：`MatchTemplateLocator` 与 `ContourTemplateLocator` 固定图案集成
  - `BenchmarkSummaryTests`：基准汇总统计（均值、分位数、稳定性）
- **剩余**：SIMD、金字塔等 `TemplateLocators.cs` 深层分支仍缺专项测试

#### [P0-3] Location 样本稳定性与 UX 修补 ✅

- **基准线程安全**：`TemplateLocatorSnapshotFactory` 在基准/训练路径为 `ContourTemplateLocator` 创建快照，避免并发 `Train()` 共享可变状态
- **UX**：工具栏 `ImportTemplate` 按钮；基准图表标题随运行次数动态显示（`{N} 次运行耗时`）；`LoadPersistedImages()` 对损坏/不兼容设置文件优雅降级（捕获 `JsonException` 等并忽略）

### P1 — 中期改进（需设计决策）

> **状态（2026-06-29）**：P1 全部完成。

#### [P1-1] 提取跨项目共享服务到 `samples.shared` ✅

- **问题**：两个 Avalonia 项目各自有 `WindowImageFileDialogService`；日志器模式重复
- **完成**：
  - `samples.shared/Services/`：`IImageFileDialogService`、`ImageFileResult`、`WindowImageFileDialogService`（含 Open/Save PNG）
  - `samples.shared/Logging/AsyncFileLogger.cs`：异步 Channel 写入、日志轮转、14 天保留
  - Location `PerformanceLogger`、Workbench `WorkbenchLogger` 改为继承 `AsyncFileLogger`
  - 删除 Location/Workbench 中重复的 dialog 服务与 `ImageFileResult` 定义
- **收益**：消除 ~120 行重复代码，统一服务抽象

#### [P1-2] 拆分 `TemplateLocators.cs` 巨型文件 ✅

- **问题**：1925 行一处文件，含两个 Strategy 实现和多个共享辅助逻辑
- **完成**：
  - 删除 `TemplateLocators.cs`，拆为 `MatchTemplateLocator.cs`（~1200 行，含嵌套精化类型）与 `ContourTemplateLocator.cs`（~720 行）
  - `ITemplateLocator` 并入 `TemplateLocatorModels.cs`
  - 精化/金字塔逻辑仍为 `MatchTemplateLocator` 私有嵌套类（未单独提取 `RefinementEngine`/`PyramidProcessor`，留作 P2 可选深化）
- **收益**：单文件从 1925 行降至最大 ~1200 行，locator 职责边界清晰

#### [P1-3] 为 `opencvsharp.crossplatform.core` 添加子命名空间 ✅

- **问题**：7 个文件全部在扁平 `OpenCvSharp.CrossPlatform.Core` 命名空间
- **完成**：按功能拆分命名空间：

```
OpenCvSharp.CrossPlatform.Core.Matching    → MatchTemplateLocator, ContourTemplateLocator, TemplateLocatorModels
OpenCvSharp.CrossPlatform.Core.Contours    → ContourModels, ContourCaches
OpenCvSharp.CrossPlatform.Core.Image       → ImageHelpers
OpenCvSharp.CrossPlatform.Core.Selection   → MatchCandidateUtilities (NMS)
OpenCvSharp.CrossPlatform.Core.Profiling   → PerformanceProfile, ProfileStep, ProfileResult
```

- Location 与 core.tests 项目通过 csproj `<Using Include="..."/>` 引入子命名空间
- **Profiler UI（2026-06-29）**：`ProfileStepViewModel` 与 `ToDisplayText`/`ToStatusText`/`ToStepViewModels` 扩展方法已迁至 Location 样本 `Presentation/Profiling/`；`MainWindow.axaml` 绑定使用 `Presentation.Profiling` 前缀

#### [P1-4] 消除 `Operators/` 与 `Domain/Operators/` 重叠 ✅

- **问题**：Workbench 项目同时存在 `Domain/Operators/`（重构后新位置）和 `Operators/`（旧位置），重构进行中但未完成
- **完成**：
  1. 将 `IImageOperator`、`OperatorRegistry` 及 10 个 `BuiltIn/` 算子迁移至 `Domain/Operators/`
  2. 更新 `PipelineRunner`、`MainWindowViewModel` 引用
  3. 删除旧 `Operators/` 目录
- **工程量**：小（验证 + 删除）

### P2 — 中期改进（预计较大改动）

#### [P2-1] 为 Avalonia 项目引入 DI 容器

- **问题**：手动 `new MainWindowViewModel(new WindowImageFileDialogService(this))`，随着项目增长依赖图会变复杂
- **做法**：
  1. 在 Workbench 项目完成 DDD 分层后引入 `Microsoft.Extensions.DependencyInjection`
  2. 在 `App.axaml.cs` 中注册服务：`IImageFileDialogService` → `WindowImageFileDialogService`，`IImageCodec` → `OpenCvImageCodec` 等
  3. ViewModel 通过构造函数自动解析依赖
- **注意**：建议在 Workbench DDD 重构完成后再引入 DI，避免在重构进行中增加另一层变动

#### [P2-2] 将 `MainWindowViewModel` 职责拆分

- **问题**：Location VM + Workbench VM 混用 UI 状态 + 业务逻辑 + 历史管理
- **Location 进展（2026-06-29）** ✅：
  - 新增 `Application/Matching/TemplateMatchOrchestrator.cs` — 匹配、轮廓训练、稳定性/性能分析基准
  - `TemplateMatchOrchestrationResults.cs` — `MatchOrchestrationResult` / `TrainOrchestrationResult`
  - `MainWindowViewModel.Commands.cs` 仅负责 UI 状态更新；`BenchmarkPanelViewModel` 委托 orchestrator 执行
  - `TemplateLocatorSnapshotFactory.DisposeOwnedLocator` — locator 生命周期从 ViewModel 层下沉
  - MainWindowViewModel 三文件合计 ~330 行（目标 250–400 行区间）
- **Workbench 进展（2026-06-29）** ✅：
  - 新增 `Application/Workbench/WorkbenchOrchestrator.cs` — 处理流程变更、撤销/重做、运行与单算子预览
  - `WorkbenchOrchestrationResults.cs` — `WorkbenchPipelineRunResult` / `WorkbenchPreviewResult` 及变更结果记录
  - `ClonePipeline` / `CloneStep` / `CreateParameterValues` 从 ViewModel 下沉至 orchestrator
  - `MainWindowViewModel` 保留 Observable 绑定、画布刷新与 `WorkbenchLogger` 用户消息；~1064 行（UI 状态占主导，后续可拆 partial）
- **Workbench 可选后续**：`WorkbenchSession`（素材/输出会话）、ViewModel partial 拆分（Inspector / Pipeline / Canvas）

#### [P2-3] 提取 View 侧可复用控件

- **问题**：`MainWindow.axaml.cs`（Location）~740 行——ROI 拖拽、缩放动画、画布平移、旋转矩形渲染全部混在代码-behind
- **完成（2026-06-29）**：
  - `Controls/ImageDisplayTransform.cs` — 图像/屏幕坐标变换
  - `Controls/ImageViewportController.cs` — 缩放、平移、动画
  - `Controls/TemplateRoiEditorControl.axaml(.cs)` — ROI 移动/缩放/旋转与 `RotatedRect` 输出
  - `MatchOverlayControl` — 匹配结果叠加层（已有）
  - `MainWindow.axaml.cs` 从 ~740 行降至 ~380 行
- **可选后续**：将视口 + ROI 合并为单一 `SourceImageViewport` 复合控件

### P3 — 低优先级/渐进式

#### [P3-1] 添加 CI/CD 流水线 ✅

- **问题**：无任何自动化构建/测试
- **完成（2026-06-29）**：`.github/workflows/ci.yml`
  - 触发：`push`/`pull_request` → `main`/`master`
  - 运行环境：`windows-latest` + .NET 10
  - 步骤：restore → build 全部 6 个项目 → `dotnet test`（35 用例）
- **待做**：macOS job（native runtime + console smoke test，需 `brew install opencv`）

#### [P3-2] 统一日志基础设施

- **做法**：在 `samples.shared` 创建 `AsyncFileLogger`，两个项目共用
- **额外**：将日志格式从纯文本改为结构化（JSON 行），便于后续分析

#### [P3-3] 解耦 `PerformanceProfiler` 的 UI 逻辑 ✅

- **问题**：`ProfileResult` 有 `ToDisplayText()`、`ToStatusText()`、`ToStepViewModels()`——呈现逻辑不应在核心算法层
- **完成（2026-06-29）**：
  - **Core 层**（`OpenCvSharp.CrossPlatform.Core.Profiling`）：仅保留 `PerformanceProfile`、`ProfileStep`、`ProfileResult` 原始数据结构
  - **Presentation 层**（Location 样本 `Presentation/Profiling/`）：
    - `ProfileStepViewModel.cs` — UI 绑定模型（名称、耗时文本、百分比、`BarFraction`）
    - `ProfilePresentationMapper.cs` — `ToDisplayText`、`ToStatusText`、`ToStepViewModels`、`FormatStepLine` 扩展方法
  - **消费者更新**：`MatchResultViewModel`、`PerformanceLogger`、`MainWindow.axaml` 引用 `Presentation.Profiling`
  - **测试拆分**：`PerformanceProfileTests`（core 原始 profiling）+ `ProfilePresentationTests`（UI 格式化，引用 Location 项目）
- **收益**：核心库无 UI 呈现依赖；Workbench 等未来 UI 可各自实现格式化层

#### [P3-4] 统一命名约定

- 目录/项目名已统一为 `opencvsharp.crossplatform.*` 点分小写风格
- 命名空间仍使用 `OpenCvSharp.CrossPlatform.*` PascalCase（符合 .NET 惯例，无需再改）

---

## 七、优先级总结矩阵

| 优先级 | 建议 | 预期收益 | 工程量 | 风险 |
|--------|------|---------|-------|-----|
| **P0** ✅ | 引入 `Directory.Packages.props` | 包升级效率 5x | 小 | 极低 |
| **P0** ✅ | 扩充 core 库单元测试（35 用例） | 算法质量保障 | 中 | 低（不影响现有代码） |
| **P0** ✅ | Location 样本稳定性与 UX 修补 | 基准可靠性与易用性 | 小 | 低 |
| **P1** ✅ | 提取共享服务到 shared | 消除重复代码 | 中 | 低 |
| **P1** ✅ | 拆分 TemplateLocators.cs | 降低巨型文件复杂度 | 中 | 低（只拆分不修改逻辑） |
| **P1** ✅ | 添加 core 子命名空间 | 为未来拆分 csproj 准备 | 中 | 低 |
| **P1** ✅ | 消除 Operators/ 重叠 | 清理重构残留 | 小 | 低（纯删除） |
| **P2** ✅ | 提取 View 侧绘图控件 | 改善 MVVM 纯度 | 中 | 低 |
| **P2** | 引入 DI 容器 | 改善可测试性 | 中 | 中（需完成 DDD 重构后） |
| **P2** ✅ | 拆分 Location ViewModel（Orchestrator） | 改善职责分离 | 大 | 中 |
| **P2** | 拆分 Workbench ViewModel | 改善职责分离 | 大 | 中 |
| **P3** ✅ | 添加 CI/CD（Windows） | 自动化质量门 | 中 | 低 |
| **P3** | 统一日志基础设施 | 简化日志维护 | 中 | 低 |
| **P3** ✅ | 解耦 Profiler UI 逻辑 | 纯化核心层 | 小 | 低 |
| **P3** | 统一命名约定 | 改善可读性 | 小 | 低 |

### 推荐行动路径

1. ~~**本周**~~：**已完成** P0-1（Directory.Packages.props）+ P0-2（35 个 core 测试用例）+ P0-3（Location 快照/UX）
2. ~~**本周**~~：**已完成** P1-1（共享服务/AsyncFileLogger）+ P1-4（Operators 迁移至 Domain/Operators）
3. ~~**本周**~~：**已完成** P1-3（core 子命名空间）+ P1-2（TemplateLocators 拆分为 Match/Contour 两文件）
4. ~~**下周**~~：**已完成** P3-1（Windows CI）+ P2-3（`ImageViewportController` + `TemplateRoiEditorControl`）
5. ~~**下一步**~~：**已完成** P2-2 Location 部分（`TemplateMatchOrchestrator`）
6. ~~**下一步**~~：**已完成** P3-3（Profiler UI 解耦 → `Presentation/Profiling`）
7. ~~**下一步**~~：**已完成** Workbench `WorkbenchOrchestrator`（Pipeline、Undo/Redo、运行、预览）
8. **后续**：P2-1（DI，Workbench DDD 完成后）+ P3-2（结构化日志）+ Workbench VM partial 拆分
