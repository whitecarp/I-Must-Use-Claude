# Building from source

Use Windows 10/11 x64 with .NET Framework 4.8. In PowerShell at the repository root, compile without launching the apps or changing settings:

```powershell
./apps/whale-claude-helper/build.ps1 -OutputDirectory "$PWD/artifacts/helper"
./apps/claude-live-translator/build.ps1 -OutputDirectory "$PWD/artifacts/translator"
./apps/two-zone-clock/build.ps1 -OutputDirectory "$PWD/artifacts/clock"
```

The environment helper and clock outputs are standalone .NET applications. The translator command produces its UI and helper components, not the portable bundle.

## Portable translator

Run its build.ps1 without OutputDirectory to place components at its project root, then use `packaging/build-portable.ps1`. The packager requires:

- Official Python 3.12.10 Windows x64 embeddable distribution under `runtime/python`, including its license.
- CTranslate2 4.8.4, SentencePiece 0.2.2, NumPy 2.5.4 and PyYAML 6.0.3 under `runtime/venv/Lib/site-packages`, with all package license metadata. These are the versions in this release payload.
- Official Visual Studio x64 VC redistributable directory; optionally pass `-VCRedistDirectory`. Its notice is retained.

```powershell
./apps/claude-live-translator/build.ps1
./apps/claude-live-translator/packaging/build-portable.ps1
```

For separate runtime and component build folders, pass `-RuntimeRoot` (a directory containing `runtime/`), `-ApplicationExe` (the compiled UI EXE, next to its three helper EXEs), and optionally `-BuildRoot` for a short temporary output path. The payload is allowlisted and includes `backend/glossary.py`; personal preferences, glossary files and translation history are never packaged.

Runtime binaries and models are not committed to Git. For normal use, download the prepared EXE from Releases; optional language packs are installed inside the translator.

## Isolated helper tests

```powershell
./apps/whale-claude-helper/tests/run-fixtures.ps1
```

This explicit runner tests synthetic cache/configuration data, injected network results and offscreen demo controls. It never starts the live helper entry point, reads real Claude configuration, sends diagnostic network requests, or shows application windows. Synthetic files and test receipts are retained for inspection; file moves use an injected fixture recycler, not the real Recycle Bin. Outputs stay under the helper's ignored `artifacts` folder.

CI also runs the clock's `-RunTests`, translator `tests/run-glossary-history.ps1`, mocked Google/offline/glossary Python tests, and `shared/tests/run-fixtures.ps1` for update selection and verified file saving. None opens Claude, translates real conversations, or applies environment settings. Independent Windows VM acceptance of the helper's modification/undo workflow remains pending. Compilation and fixtures are not end-to-end validation.

## Publishing an update

Attach only `WhaleTranslator.exe`, `HomeTimeClock.exe` and `WhaleEnvironmentHelper.exe`. Include a single-line machine-readable comment in the GitHub release body, updating each app version when its binary changes:

```html
<!-- whale-app-versions: {"translator":"0.9.0","clock":"0.3.0","helper":"0.5.0"} -->
```

The updater reads public published releases, including previews, validates the asset's repository URL, size and GitHub SHA-256 digest, and never overwrites or launches a download. Missing digests or version metadata are not accepted. Preview builds pass `currentPreview=true` to `UpdateChecker.Show` (the default); stable builds must pass `false`. A stable release with the same app version can replace a preview. Keep the artifact versions, this metadata and each app's current version in sync.

## Project page

`docs/index.html` is the bilingual project page. It is a single static file with no build step; it reuses the images in `docs/images/`, and `docs/.nojekyll` keeps GitHub Pages from processing the folder. To publish it, open the repository's **Settings → Pages**, choose **Deploy from a branch**, then select `main` and `/docs`. Preview it locally by opening the file in a browser.

## 中文

日常使用请到 Releases 下载单文件 EXE。源码构建要求 Windows 与 .NET Framework 4.8，上面的命令只编译、不启动程序。

翻译器普通构建只生成组件，自包含打包还需要上述 Python、离线引擎和官方 VC 运行库。模型由用户按需安装。CI 构建、检查语法并运行上述隔离测试，不操作真实 Claude 或修改系统；环境助手仍需独立 Windows 虚拟机验收。

项目主页是 `docs/index.html`（中英双语、无需构建）。在仓库 **Settings → Pages** 里选择 **Deploy from a branch**，分支选 `main`、目录选 `/docs` 即可发布。
