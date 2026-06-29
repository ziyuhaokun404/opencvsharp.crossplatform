# Workbench 分层架构重构计划

## 背景

`samples/opencvsharp.crossplatform.samples.workbench.avalonia` 已经完成了一轮目录拆分：

- `App/` 放置 Avalonia 启动入口
- `Views/` 放置窗口和 AXAML
- `ViewModels/` 放置主 ViewModel 和 UI 模型
- `Services/` 放置文件选择和日志服务
- `Operators/` 放置图像处理算子

这轮拆分解决了文件平铺问题，但还没有解决架构问题。当前 `MainWindowViewModel.cs` 仍约 1074 行，仍承担了过多职责：图像资产状态、算子筛选、参数同步、流水线状态、撤销重做、图像处理执行、画布显示状态、导入导出和日志写入。

本计划的目标不是继续机械拆文件，而是建立清晰的项目分层、依赖方向和模块边界，让 workbench 后续可以稳定扩展。

## 重构目标

1. 建立明确分层：Presentation、Application、Domain、Infrastructure。
2. 让 ViewModel 只负责 UI 状态映射和命令转发，不直接处理 OpenCV 流水线细节。
3. 让图像处理流水线成为可测试的应用服务，不依赖 Avalonia 控件和 Bitmap。
4. 让算子接口描述参数元数据、参数选项和执行逻辑，避免 ViewModel 通过算子名称写特殊分支。
5. 让撤销重做围绕工作区状态快照运转，而不是散落在 UI 操作中。
6. 保持现有功能和交互不变，重构期间每个阶段都能编译。

## 设计原则

- 依赖方向只能从外层指向内层：Presentation -> Application -> Domain。
- Domain 不引用 Avalonia，不引用文件对话框，不写日志。
- Application 可以引用 OpenCvSharp，但不引用 Avalonia UI 类型。
- Infrastructure 实现文件系统、日志、图片编解码、文件对话框等外部能力。
- ViewModel 可以使用 Avalonia 的 `Bitmap`、`IImage`、`GridLength` 等展示类型，但这些类型不能进入 Domain。
- 用接口表达边界，用具体类留在组合根或 Infrastructure。
- 不为了拆分而增加过早抽象。每个新增服务都必须拿走一类明确职责。

## 目标目录结构

```text
samples/opencvsharp.crossplatform.samples.workbench.avalonia/
├── App/
│   ├── App.axaml
│   ├── App.axaml.cs
│   ├── Program.cs
│   └── ServiceComposition.cs
│
├── Presentation/
│   ├── Views/
│   │   ├── MainWindow.axaml
│   │   └── MainWindow.axaml.cs
│   ├── ViewModels/
│   │   ├── MainWindowViewModel.cs
│   │   ├── AssetsPanelViewModel.cs
│   │   ├── OperatorPanelViewModel.cs
│   │   ├── PipelinePanelViewModel.cs
│   │   ├── InspectorPanelViewModel.cs
│   │   ├── CanvasViewModel.cs
│   │   └── ShellStatusViewModel.cs
│   ├── Models/
│   │   ├── ImageAssetItem.cs
│   │   ├── OperatorItem.cs
│   │   └── PipelineNodeItem.cs
│   └── Converters/
│
├── Application/
│   ├── Workbench/
│   │   ├── WorkbenchSession.cs
│   │   ├── WorkbenchState.cs
│   │   ├── WorkbenchCommands.cs
│   │   └── WorkbenchHistory.cs
│   ├── Pipeline/
│   │   ├── PipelineRunner.cs
│   │   ├── PipelineRunResult.cs
│   │   ├── PipelineStep.cs
│   │   └── PipelineStepResult.cs
│   ├── Imaging/
│   │   ├── ImageAsset.cs
│   │   ├── ImageBuffer.cs
│   │   └── ImageMetadata.cs
│   └── Ports/
│       ├── IImageFileDialogService.cs
│       ├── IImageCodec.cs
│       ├── IWorkbenchLog.cs
│       └── IClock.cs
│
├── Domain/
│   ├── Operators/
│   │   ├── IImageOperator.cs
│   │   ├── ImageOperatorRegistry.cs
│   │   ├── ImageOperatorDescriptor.cs
│   │   ├── OperatorParameter.cs
│   │   ├── OperatorParameterOption.cs
│   │   └── BuiltIn/
│   │       ├── GrayscaleOperator.cs
│   │       ├── GaussianBlurOperator.cs
│   │       ├── CannyEdgeOperator.cs
│   │       ├── BinaryThresholdOperator.cs
│   │       ├── AdaptiveThresholdOperator.cs
│   │       ├── MorphologyCloseOperator.cs
│   │       ├── FindContoursOperator.cs
│   │       ├── SharpenOperator.cs
│   │       ├── ResizeOperator.cs
│   │       └── RotateOperator.cs
│   └── Shared/
│       ├── Result.cs
│       └── ValueRange.cs
│
├── Infrastructure/
│   ├── Avalonia/
│   │   ├── AvaloniaImageFileDialogService.cs
│   │   ├── AvaloniaBitmapFactory.cs
│   │   └── AvaloniaImageExportService.cs
│   ├── OpenCv/
│   │   └── OpenCvImageCodec.cs
│   ├── Logging/
│   │   └── WorkbenchLogger.cs
│   └── System/
│       └── SystemClock.cs
│
└── opencvsharp.crossplatform.samples.workbench.avalonia.csproj
```

如果暂时不想大规模移动命名空间，可以先保留现有 `Views/`、`ViewModels/`、`Services/`、`Operators/` 目录，在第二阶段完成后再统一迁移到目标目录。优先级是边界清晰，不是目录一次到位。

## 分层职责

### Presentation

负责 Avalonia UI 和绑定状态：

- `MainWindowViewModel` 作为组合型 shell，只暴露页面级命令和子 ViewModel。
- `AssetsPanelViewModel` 管理图像列表、选择、导入命令入口。
- `OperatorPanelViewModel` 管理算子列表、搜索、分类、选择。
- `PipelinePanelViewModel` 管理步骤列表、启用/禁用、排序、删除。
- `InspectorPanelViewModel` 管理当前算子或步骤的参数编辑。
- `CanvasViewModel` 管理输入/输出图像展示、画布模式、滑动对比。
- `ShellStatusViewModel` 管理状态栏、运行时间、错误提示。

Presentation 不直接执行 `Cv2.*`，不直接保存 pipeline，不直接读写图片文件。

### Application

负责用例和工作区状态：

- `WorkbenchSession` 是 workbench 的应用门面，提供导入、导出、选择资产、添加步骤、更新参数、运行流水线、撤销重做等用例。
- `WorkbenchState` 保存当前资产、当前流水线、当前选择、最近一次运行结果。
- `WorkbenchHistory` 维护可撤销状态快照。
- `PipelineRunner` 按顺序执行 `PipelineStep`，返回每一步耗时、成功/失败状态、最终图像。
- `ImageAsset`、`ImageBuffer` 表达应用层图像数据，不暴露 Avalonia `Bitmap`。

Application 可以调用 Domain 算子，可以调用 Ports 接口，但不能知道具体窗口、文件对话框和 Avalonia 控件。

### Domain

负责纯业务规则和算子定义：

- `IImageOperator` 执行一个图像处理操作。
- `ImageOperatorDescriptor` 描述名称、分类、说明、参数元数据。
- `OperatorParameter` 描述参数 key、显示名、范围、默认值、显示格式、可选项。
- `ImageOperatorRegistry` 提供算子注册和查询。
- 内置算子只负责参数归一化和 OpenCV 调用。

Domain 不处理 UI 可见性。比如 `Rotate` 的边界模式、`Resize` 的插值方式应通过参数选项表达，而不是让 ViewModel 判断 `SelectedOperator.Name == "Rotate"`。

### Infrastructure

负责外部系统适配：

- 文件选择：Avalonia storage provider。
- 图片编解码：OpenCV `Mat` 与文件字节、PNG/JPEG 等格式转换。
- UI 位图工厂：`ImageBuffer` -> Avalonia `Bitmap`。
- 日志：写文件、控制台或后续扩展为 structured logging。
- 系统时间：给日志和耗时统计提供可替换时钟。

Infrastructure 可以引用 Avalonia 和 OpenCvSharp，但实现应该被 Ports 隔离。

## 核心接口调整

### IImageOperator

现有接口把参数 A/B 固定死，扩展性有限。目标接口应改为参数集合：

```csharp
public interface IImageOperator
{
    ImageOperatorDescriptor Descriptor { get; }

    Mat Apply(Mat source, IReadOnlyDictionary<string, int> parameters, OperatorExecutionContext context);
}
```

`ImageOperatorDescriptor`：

```csharp
public sealed record ImageOperatorDescriptor(
    string Id,
    string Name,
    string Category,
    string Description,
    IReadOnlyList<OperatorParameter> Parameters,
    bool SupportsClamp);
```

`OperatorParameter`：

```csharp
public sealed record OperatorParameter(
    string Key,
    string DisplayName,
    int DefaultValue,
    ValueRange Range,
    IReadOnlyList<OperatorParameterOption> Options,
    string? Unit = null);
```

这样 `Resize` 的插值方式、`Rotate` 的边界模式都可以通过 `Options` 显示，不需要 Presentation 写特殊分支。

### PipelineStep

```csharp
public sealed record PipelineStep(
    Guid Id,
    string OperatorId,
    IReadOnlyDictionary<string, int> Parameters,
    bool IsEnabled,
    bool ClampOutput);
```

Pipeline 不应保存 `OperatorViewModel`，只保存稳定的 `OperatorId` 和参数。

### PipelineRunner

```csharp
public sealed class PipelineRunner
{
    public PipelineRunResult Run(ImageBuffer input, IReadOnlyList<PipelineStep> steps);
}
```

`PipelineRunResult` 应包含：

- 最终输出图像
- 每一步耗时
- 每一步输出摘要
- 失败步骤 ID 和错误信息

## MainWindowViewModel 目标形态

最终 `MainWindowViewModel` 应缩减到 250-400 行，主要职责：

- 创建并暴露子 ViewModel。
- 绑定顶层命令：导入、导出、撤销、重做、运行。
- 订阅 `WorkbenchSession` 状态变化并分发到子 ViewModel。
- 释放资源。

它不应再包含：

- `Cv2.*` 处理逻辑
- pipeline 执行循环
- 参数范围和参数选项判断
- 图像文件读写细节
- 大量 canvas 文案拼接
- undo/redo 的快照细节

## 迁移步骤

### 阶段 0：建立安全基线

1. 确认当前状态可以构建：
   ```bash
   dotnet build OpenCvSharp.slnx
   ```
2. 记录当前 workbench 主要交互作为手工回归清单：
   - 导入图像
   - 选择算子并预览
   - 添加/删除/移动 pipeline step
   - 启用/禁用 step
   - 修改参数
   - 撤销/重做
   - 三种画布模式
   - 导出结果
   - 日志写入

### 阶段 1：改造算子元数据

1. 引入 `ImageOperatorDescriptor`、`OperatorParameter`、`OperatorParameterOption`、`ValueRange`。
2. 给现有 10 个算子补充稳定 `Id`。
3. 把参数 A/B 改成参数集合，但先提供兼容层，避免一次性改完整个 ViewModel。
4. 消除 ViewModel 中基于算子名称的参数 UI 判断，把参数选项交给描述符。
5. 构建验证。

验收标准：

- 新增算子不需要修改 `MainWindowViewModel`。
- `Resize`、`Rotate` 的特殊参数由 descriptor options 驱动。
- `dotnet build` 通过。

### 阶段 2：抽出 PipelineRunner

1. 新建 `Application/Pipeline/PipelineStep.cs`。
2. 新建 `PipelineRunner`，迁移当前 `RunPipeline` 中的执行循环。
3. 新建 `PipelineRunResult` 和 `PipelineStepResult`。
4. ViewModel 只把输入图像和步骤列表交给 runner，并消费结果。
5. 将失败步骤、耗时、输出图像的计算从 ViewModel 移出。

验收标准：

- `MainWindowViewModel` 不再直接包含 pipeline 执行循环。
- pipeline 执行可以在没有 Avalonia 窗口的情况下测试。
- `dotnet build` 通过。

### 阶段 3：抽出 WorkbenchSession 和状态快照

1. 新建 `WorkbenchState`，保存资产、步骤、选择和运行结果。
2. 新建 `WorkbenchSession`，集中提供工作区用例方法。
3. 新建 `WorkbenchHistory`，用 `WorkbenchState` 快照实现撤销重做。
4. 把 ViewModel 中的 `pipeline`、`undoStack`、`redoStack` 迁移到 Application。
5. ViewModel 订阅 session 状态变化后刷新绑定集合。

验收标准：

- 撤销重做不依赖 UI 集合。
- pipeline step 的增删改移逻辑不在 `MainWindowViewModel`。
- `MainWindowViewModel` 行数明显下降。
- `dotnet build` 通过。

### 阶段 4：拆分 Presentation ViewModel

1. 抽出 `AssetsPanelViewModel`。
2. 抽出 `OperatorPanelViewModel`。
3. 抽出 `PipelinePanelViewModel`。
4. 抽出 `InspectorPanelViewModel`。
5. 抽出 `CanvasViewModel`。
6. `MainWindowViewModel` 只做组合和应用命令协调。

验收标准：

- 单个 ViewModel 文件原则上不超过 400 行。
- 子 ViewModel 职责可以用一句话说明。
- AXAML 绑定路径清晰，没有大量跨区域状态绑定。
- `dotnet build` 通过。

### 阶段 5：完善 Infrastructure 边界

1. 把文件对话框接口移动到 `Application/Ports`。
2. 把 Avalonia 文件对话框实现移动到 `Infrastructure/Avalonia`。
3. 抽出 `IImageCodec`，集中处理文件字节、Mat、导出格式。
4. 抽出 `AvaloniaBitmapFactory`，集中处理展示位图创建和释放策略。
5. 把日志接口变成 `IWorkbenchLog`，实现留在 `Infrastructure/Logging`。

验收标准：

- Application 只依赖 Ports，不依赖 Infrastructure 具体类。
- 图片导入导出逻辑不在 ViewModel。
- `dotnet build` 通过。

### 阶段 6：目录和命名空间收口

1. 将现有 `Views/`、`ViewModels/`、`Services/`、`Operators/` 迁移到目标目录结构。
2. 更新 AXAML 命名空间。
3. 更新 `.csproj` 中必要的路径配置。
4. 删除过渡兼容代码。
5. 更新 README 或新增 workbench 架构说明。

验收标准：

- 目录结构与分层一致。
- 没有旧 namespace 残留。
- `dotnet build OpenCvSharp.slnx` 通过。

## 测试策略

当前项目没有专门测试项目。建议新增：

```text
tests/opencvsharp.crossplatform.samples.workbench.tests/
```

优先覆盖 Application 和 Domain：

- 算子 descriptor 完整性：每个算子有稳定 Id、名称、分类、参数范围。
- 参数归一化：奇数 kernel、阈值范围、枚举参数。
- `PipelineRunner`：跳过 disabled step、失败步骤返回、结果顺序和耗时。
- `WorkbenchHistory`：撤销、重做、执行新操作后清空 redo。
- `WorkbenchSession`：添加、删除、移动、更新参数。

Avalonia UI 不作为第一批自动化测试重点，先通过手工回归清单验证。

## 手工回归清单

- 应用启动，OpenCV runtime 正常加载。
- 导入 PNG/JPEG 后资产列表、画布和元信息更新。
- 10 个内置算子都能预览和添加到 pipeline。
- 每个算子的参数范围、默认值、显示文本正确。
- `Resize` 插值方式和 `Rotate` 边界模式通过选项显示。
- pipeline step 可以添加、删除、移动、启用、禁用。
- 撤销/重做覆盖添加、删除、移动、参数修改。
- 处理失败时能标记失败步骤并显示错误。
- Side-by-side、Result-only、Slider compare 三种画布模式正常。
- 导出结果文件可打开。
- 日志文件正常生成。

## 风险和控制

- 风险：一次性移动目录和改架构会制造大量噪音。
  控制：先改边界和接口，最后再收口目录。

- 风险：OpenCV `Mat` 生命周期在服务拆分后变得不清晰。
  控制：为 `ImageBuffer` 和 `PipelineRunResult` 明确所有权，规定谁创建谁释放。

- 风险：AXAML 绑定在拆分 ViewModel 后容易断。
  控制：每拆一个 panel 就构建一次，并做对应 UI 手工检查。

- 风险：参数模型从 A/B 改成字典会影响现有 UI。
  控制：先建立兼容适配层，等 Inspector 拆分后再移除 A/B 绑定。

## 完成定义

- `MainWindowViewModel` 降到 250-400 行，且只负责 shell 协调。
- pipeline 执行、工作区状态、撤销重做都位于 Application。
- 算子和参数元数据位于 Domain，新增算子不需要改 ViewModel。
- 文件对话框、日志、图片编解码、Avalonia Bitmap 创建位于 Infrastructure。
- 所有阶段 `dotnet build OpenCvSharp.slnx` 通过。
- 手工回归清单通过。
- 可选但建议：Application/Domain 有基础单元测试覆盖。
