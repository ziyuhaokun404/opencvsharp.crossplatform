# OpenCvSharp Cross-Platform Workspace

本工作区包含：

- `src/opencvsharp.crossplatform.core`：算法封装库（模板匹配、轮廓分析等）
- `ref/opencvsharp-4.13`：OpenCvSharp 原生 C++ 源码（用于构建 `OpenCvSharpExtern`）
- `samples/opencvsharp.crossplatform.samples.console`：控制台演示
- `samples/opencvsharp.crossplatform.samples.workbench.avalonia`：图像处理工作台
- `samples/opencvsharp.crossplatform.samples.location.avalonia`：模板匹配可视化演示
- `samples/opencvsharp.crossplatform.samples.shared`：共享原生运行时加载逻辑
- `tests/opencvsharp.crossplatform.core.tests`：核心算法单元测试

## 跨平台原生运行时

| 平台 | 策略 |
|------|------|
| **macOS** | 本地 CMake 自编译 `libOpenCvSharpExtern.dylib`，构建时复制到输出目录 |
| **Windows** | 官方 NuGet `OpenCvSharp4.runtime.win` / `OpenCvSharp4.runtime.win-arm64`（按 CPU 架构自动选择） |

Windows 上无需手动构建原生库；macOS 上仍需按下方步骤构建并 stage。

## 原生库约定（macOS）

原生库文件名：`libOpenCvSharpExtern.dylib`

推荐放到以下目录：

```text
build/native/osx-arm64/libOpenCvSharpExtern.dylib
build/native/osx-x64/libOpenCvSharpExtern.dylib
```

构建时会自动把原生库复制到应用输出目录。运行时也支持通过环境变量 `OPENCVSHARP_EXTERN_PATH` 显式指定原生库路径（仅 macOS）。

## 构建原生库（macOS）

```bash
brew install cmake pkg-config opencv
scripts/build-native-runtime-macos.sh
```

脚本默认通过 `brew --prefix opencv` 查找 OpenCV；如果 OpenCV 安装在其他位置，可以显式传入：

```bash
OpenCV_DIR=/path/to/opencv4/cmake scripts/build-native-runtime-macos.sh
```

构建后执行：

```bash
scripts/stage-native-runtime.sh arm64
```

它会把原生库复制到 `build/native/osx-arm64/`。

如果需要制作自包含原生包（含所有 Homebrew 依赖）：

```bash
scripts/bundle-native-runtime-macos.sh
```

## 运行演示

```bash
dotnet run --project samples/opencvsharp.crossplatform.samples.console/opencvsharp.crossplatform.samples.console.csproj
dotnet run --project samples/opencvsharp.crossplatform.samples.workbench.avalonia/opencvsharp.crossplatform.samples.workbench.avalonia.csproj
dotnet run --project samples/opencvsharp.crossplatform.samples.location.avalonia/opencvsharp.crossplatform.samples.location.avalonia.csproj
dotnet test tests/opencvsharp.crossplatform.core.tests/opencvsharp.crossplatform.core.tests.csproj
```

控制台演示会输出示例图与边缘检测结果。Avalonia 演示在 macOS 上启动时会加载本地 `libOpenCvSharpExtern.dylib`；在 Windows 上由 NuGet 运行时包提供 `OpenCvSharpExtern.dll`。
