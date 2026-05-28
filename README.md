# OpenCvSharp Cross-Platform Workspace

本工作区包含：

- `src/OpenCvSharp.TemplateMatching`：模板匹配库
- `demo/OpenCvSharp.Demo.Console`：控制台演示
- `demo/OpenCvSharp.Demo.Workbench.Avalonia`：图像处理工作台
- `demo/OpenCvSharp.Demo.TemplateMatch.Avalonia`：模板匹配可视化演示

目标是把 OpenCvSharp 的 .NET 演示和本地原生运行时解耦，让项目本身保持跨平台，而平台差异只留在原生库构建和分发环节。

## 原生库约定

各个 demo 现在会按当前平台加载对应文件名的 `OpenCvSharpExtern`：

- Windows: `OpenCvSharpExtern.dll`
- macOS: `libOpenCvSharpExtern.dylib`
- Linux: `libOpenCvSharpExtern.so`

推荐把原生库放到以下目录：

```text
build/native/<rid>/<native-library>
```

例如：

```text
build/native/osx-arm64/libOpenCvSharpExtern.dylib
build/native/osx-x64/libOpenCvSharpExtern.dylib
build/native/linux-x64/libOpenCvSharpExtern.so
build/native/win-x64/OpenCvSharpExtern.dll
```

构建时会自动把对应平台的原生库复制到应用输出目录。运行时也支持通过环境变量 `OPENCVSHARP_EXTERN_PATH` 显式指定原生库路径。

为了兼容当前 macOS 的旧构建目录，项目仍然接受这个历史路径：

```text
build/opencvsharp-extern-4.13/OpenCvSharpExtern/libOpenCvSharpExtern.dylib
```

## 平台脚本约定

脚本命名现在统一为：

- `build-native-runtime-<platform>.sh`
- `bundle-native-runtime-<platform>.sh`
- `stage-native-runtime.sh`

目前仓库内已提供 macOS 版本，后续新增 Linux 或 Windows 构建脚本时可以沿用同一模式。

## macOS 构建原生库

当前仓库自带的是 macOS 构建链路。如果你在 macOS arm64 上本地开发，可以继续使用：

```bash
brew install cmake pkg-config opencv
scripts/build-native-runtime-macos.sh
```

构建后可执行：

```bash
scripts/stage-native-runtime.sh osx-arm64
```

它会把原生库复制到新的通用目录 `build/native/osx-arm64/`。

如果需要制作 macOS 自包含原生包，仍可使用：

```bash
scripts/bundle-native-runtime-macos.sh
```

旧脚本名 `scripts/make-macos-native-bundle.sh` 仍然保留，作为兼容入口。

## 运行演示

```bash
dotnet run --project demo/OpenCvSharp.Demo.Console/OpenCvSharp.Demo.Console.csproj
dotnet run --project demo/OpenCvSharp.Demo.Workbench.Avalonia/OpenCvSharp.Demo.Workbench.Avalonia.csproj
dotnet run --project demo/OpenCvSharp.Demo.TemplateMatch.Avalonia/OpenCvSharp.Demo.TemplateMatch.Avalonia.csproj
```

控制台演示会输出示例图与边缘检测结果。两个 Avalonia 演示会在启动时自动加载当前平台对应的 `OpenCvSharpExtern`。

## 架构文档

本项目已生成 AI 辅助架构文档，入口见 [docs-ai/00-index.md](docs-ai/00-index.md)。
