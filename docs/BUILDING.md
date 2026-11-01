# Building from source

Use Windows 10/11 x64 with .NET Framework 4.8. In PowerShell at the repository root, compile without launching the apps or changing settings:

```powershell
./apps/claude-live-translator/build.ps1 -OutputDirectory "$PWD/artifacts/translator"
./apps/two-zone-clock/build.ps1 -OutputDirectory "$PWD/artifacts/clock"
./apps/whale-claude-helper/build.ps1 -OutputDirectory "$PWD/artifacts/helper"
```

The clock and environment helper outputs are standalone .NET applications. The translator command produces its UI and helper components, not the portable bundle.

## Portable translator

Run its build.ps1 without OutputDirectory to place components at its project root, then use `packaging/build-portable.ps1`. The packager requires:

- Official Python 3.12.10 Windows x64 embeddable distribution under `runtime/python`, including its license.
- CTranslate2 4.8.4, SentencePiece 0.2.2, NumPy 2.5.4 and PyYAML 6.0.3 under `runtime/venv/Lib/site-packages`, with all package license metadata. These are the versions in this release payload.
- Official Visual Studio x64 VC redistributable directory; optionally pass `-VCRedistDirectory`. Its notice is retained.

```powershell
./apps/claude-live-translator/build.ps1
./apps/claude-live-translator/packaging/build-portable.ps1
```

Runtime binaries and models are not committed to Git. For normal use, download the prepared EXE from Releases; optional language packs are installed inside the translator.

CI compiles the apps and checks Python syntax. It does not open Claude, test live translation, or apply environment settings. Independent Windows VM acceptance of the helper's modification/undo workflow remains pending. Compilation is not end-to-end validation.

## 中文

日常使用请到 Releases 下载单文件 EXE。源码构建要求 Windows 与 .NET Framework 4.8，上面的命令只编译、不启动程序。

翻译器普通构建只生成组件，自包含打包还需要上述 Python、离线引擎和官方 VC 运行库。模型由用户按需安装。CI 只构建和检查语法，不操作 Claude 或修改系统；环境助手仍需独立 Windows 虚拟机验收。
