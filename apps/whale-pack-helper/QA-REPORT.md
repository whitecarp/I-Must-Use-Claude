# Pack Helper 0.1.0 — source integration and release preparation

2026-11-01. Imported the app source, tests, embedded icon/whale artwork, manifest, configuration, build script and README into this repository. The original project remains intact. Historical output, private media fixtures and internal task notes were not imported.

- Built with the Windows Framework compiler using `/langversion:5`, into a fresh isolated output directory.
- Default tests passed: **4,713 core + 2,375 media assertions = 7,088**.
- The tests use generated local fixtures and temporary files. They do not open the application UI, read user settings, use live Claude, or invoke FFmpeg.
- The supplied desktop EXE and `.exe.config` were copied byte-for-byte and hash-checked. Version: **0.1.0.0**.
- A fresh build in the isolated release worktree again passed all **7,088** assertions. The required `src/HandoffForm.cs` is explicitly retained despite the repository's general handoff-file exclusion.
- All **22** product/build/test/resource files match the original project byte-for-byte. README and this report contain repository-specific integration notes.
- Release files use `WhalePackHelper.exe` plus `WhalePackHelper.exe.config`; both preserve the supplied bytes. The configuration contains no name-specific binding. Keeping the sidecar named after the executable preserves the CLR runtime configuration convention. This check is static; the real application UI was not launched.
- The paired ZIP has exactly three entries: the executable, its configuration, and a short bilingual README. Each uncompressed entry was hash-checked against the prepared file.
- Source screening found no personal user paths or generated media/log/config artifacts. Credential-shaped matches occur in the scanner's detection rules and deliberately synthetic test fixtures, not personal credentials.
- This integration did not repeat the original project's real-video or screenshot acceptance. Publication is handled separately; this document records build and asset validation, not an upload receipt.

Supplied executable SHA-256:

```text
AC2888F31F3EA1A3887FC983CFEA196EB664D059088FAFD0DF7970CFAAA29749
```

Configuration SHA-256:

```text
6B5AA0A506E1824E04474920B6678ADE3825BDB3542DB92180A98444D6D8E7E7
```

Prepared paired ZIP (`WhalePackHelper-0.1.0.zip`, 172,742 bytes) SHA-256:

```text
3767CAD881A0129B739FEAB69A5F9D4520D0DA51A8F18E4F53DAA8E2AADF168E
```

## 中文

源码已归入工具箱仓库，保留构建必需的资源、测试、配置和说明；原项目未修改。没有重复导入历史打包产物或私人素材。发布工作区重新构建通过 7,088 项检查，22 个产品、构建、测试及资源文件与原项目一致。发布使用英文 EXE 文件名，配套配置必须同名；两者保留原始字节，ZIP 内三个文件均已逐项核验。源码未发现个人路径或生成日志，密钥形状命中均来自筛查规则与合成夹具。本次没有重新运行真实视频集成或界面截图验收；本文是发布前核验记录，不代表已经上传。
