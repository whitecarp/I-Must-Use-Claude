# 大肥鱼打包助手 0.1.1 / Whale Pack Helper

只在本机整理文件，不上传；唯一的联网是手动点「检查更新」（0.1.1 起）。Windows 10/11，.NET Framework 4.8 或更高版本。

发布下载使用英文文件名 `WhalePackHelper.exe`。0.1.0 发布版还需要同名 `WhalePackHelper.exe.config` 放在同一文件夹（或下载配对 ZIP 后解压）；从 0.1.1 起，长路径开关写在程序里，单个 EXE 即可运行，配置文件可有可无。源码构建的输出名仍是 `大肥鱼打包助手.exe`。

## 使用

1. 打开 `dist/大肥鱼打包助手.exe`，拖入文件或文件夹，也可以使用选择按钮。
2. 检查每行的处理方式。点击明细查看跳过的内容；疑似密钥文件默认排除，仅在明确确认后才能放回。
3. 在「写交接说明…」填写项目、目标、做到哪了、已验证、还没验证、下一步。全部可留空。
4. 选择输出位置，点击「开始打包」。默认输出到桌面的「发给Claude」，每次建立独立子目录，避免覆盖之前的结果。
5. 完成后打开输出目录，复制交接句子。每个 ZIP 可单独解压；要还原所有内容，将它们解压到同一个空目录。

右上角可切换中文 / English。交接字段与语言、输出目录、FFmpeg 路径保存在 `%LOCALAPPDATA%/WhalePack/settings.json`，不保存待发送文件内容。

## 本机 FFmpeg

视频压缩和 WebP 解码使用本机已有的 FFmpeg，不自动下载。查找顺序：程序目录的 `ffmpeg.exe` → PATH → 设置中手动指定的位置。没有时会提示选择文件。如果同目录有 `ffprobe.exe`，会用于读取媒体信息；没有则使用 FFmpeg 读取。

FFmpeg 需要支持 `libx264`、AAC、`scale` 和 `colorspace`。处理在后台运行，可取消；不会结束用户另外启动的 FFmpeg。

## 处理与大小

- **大小使用十进制 MB**：1 MB = 1,000,000 字节。每个最终文件最多 29,000,000 字节，留出距离 30 MB 的余量；完成前核验文件实际大小。界面中的预计大小是估算，长视频可能生成多段。
- **视频**：H.264 + AAC MP4，两遍编码；短边不超过 720、不放大小视频，保留帧率/帧时间，SDR 转为并标记 BT.709。目标码率过低时按时长分段，每段独立播放。Windows 文件名不能用冒号，因此文件名时间用短横线；精确时间范围另附文本。
- **HDR 视频**：当前版本会明确提示不支持，避免把 HDR 直接误标成 SDR 导致偏色。可选择原样打包，或先在其他工具中正确转成 SDR。
- **图片**：最长边最大 2560，PNG 截图保留 PNG，超过 5 MB 的不透明照片使用质量 90 的 JPEG。保留透明图片，不强行改成不透明 JPEG。
- **源码文件夹**：保留相对路径；默认跳过 `.git`、`bin`、`obj`、`node_modules`、`__pycache__`、`.vs`、`artifacts`、`runtime`、`models`，以及 EXE、DLL、PDB、ZIP、7Z、ISO、MSI 和大于 20 MB 的单个文件。明细可逐项放回，链接和不可读取的项目不能放回。
- **密钥筛查**：按文件名、浏览器配置路径、文本中的常见密钥样式筛查。不会展示匹配出的密钥原文。筛查不能识别所有秘密、图片中的密码或任意加密归档内的内容，发送前仍应检查。
- **其他大文件**：无法压缩到预算内时，按原始字节切成 `.001`、`.002` 等，并装入独立 ZIP。交接说明包含 PowerShell 合并命令和 SHA-256 校验。存在原样切片时，交接说明会强制保留，以免缺失合并方法。
- **只读源文件**：处理使用单独临时副本，不改原文件。拒绝 UNC、网络映射盘、符号链接与重解析点；不主动访问网络。

## 编译与测试

在 PowerShell 中运行：

```powershell
./build.ps1
```

使用 Windows 自带的 `.NET Framework/v4.0.30319/csc.exe`，强制 `/langversion:5`，不需要 NuGet、dotnet SDK 或网络。默认编译并执行单元测试；`-SkipTests` 可仅编译。

Windows PowerShell 5.1 也已验证。如果本机默认禁止运行脚本，可仅对本次构建进程运行下面的命令，不更改系统执行策略：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

集成测试和界面截图的入口：

```powershell
./dist/WhalePack.Tests.exe --integration '本机ffmpeg.exe路径' '真实竖屏视频.mp4' '新的测试输出目录'
./dist/WhalePack.Tests.exe --screenshots '集成测试输出目录' '截图输出目录'
```

截图测试使用真实 WinForms 控件按 96/144 逻辑 DPI 渲染，不更改用户的 Windows 显示缩放设置。本次源码整合及隔离构建核验见 `QA-REPORT.md`；原项目的历史媒体产物和截图未重复导入。上述集成命令使用默认 `dist` 构建目录，以便定位同级翻译器源码。

## English quick start

Open the executable, switch to **English**, add files or folders, review processing modes and exclusions, optionally fill in the handoff, and select **Pack files**. Results are written to a new subfolder. Every output is checked against a strict **29,000,000-byte** ceiling; ZIPs are independently extractable. Originals remain untouched.

Release downloads use `WhalePackHelper.exe`. The 0.1.0 release also needs `WhalePackHelper.exe.config` in the same folder (or extract the paired ZIP). From 0.1.1 the long-path switches are set in code, so the EXE runs on its own and the config file is optional. Source builds retain the Chinese executable name.

This is a local utility. It does not upload files, call AI services, download FFmpeg, or access network shares; from 0.1.1 the only network access is the manual Check updates button in Settings. Secret detection is heuristic; review contents before sending. FFmpeg must already be installed or selected manually. HDR conversion is not supported in v0.1.0; use Original mode or convert to SDR separately.

Build with `build.ps1`; it uses the Windows Framework compiler with C# 5 and runs tests by default. All source files, assets, and test harnesses are included.
