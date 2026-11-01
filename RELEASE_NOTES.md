# Release notes

## v0.3.0-preview — terminology, recent translations and updates

- **Whale Translator 0.9.0:** a personal glossary for preferred terms, plus Previous / Next navigation through translations from the current session. Translation history stays in memory and is cleared on exit; saved glossary entries are separate.
- **Two-Zone Clock 0.3.0:** show the target region's time difference and relative day compared with the home region, using Windows timezone rules including daylight saving changes.
- **All three apps, including Environment Helper 0.5.0:** manually check this repository's public GitHub releases, including previews, and save the correct standalone EXE to a user-selected location. A GitHub-provided SHA-256 digest is required and must match. Missing or mismatched digests are not accepted as verified updates. There is no automatic background check, replacement or execution; existing apps and settings stay in place.

Three Windows EXE downloads remain the delivery format. The clock's time display stays offline; manual update checks/downloads are its only new network use. These features do not change the apps' language defaults or the helper's protected-data and account-access boundaries.

**Validation scope:** isolated fixtures and build checks. Independent Windows VM end-to-end acceptance is still pending; this preview does not claim production-environment or account-access validation.

翻译器 **0.9.0** 新增个人术语表，以及仅保留在本次运行内存中的「上一条／下一条」翻译记录；关闭软件清空翻译记录，保存的术语表单独保留。两地时钟 **0.3.0** 新增目标地区相对原本地区的时差与跨日提示，考虑夏令时。

三个软件（含环境助手 **0.5.0**）新增手动检查更新，可查看包含预览版在内的 GitHub 发布，并把对应 EXE 下载到用户选择的位置。必须有 GitHub 提供的 SHA-256 且核验一致；缺少或不匹配时不接受为已验证更新。不后台自动检查、不自动替换或运行，保留旧软件和设置。时钟正常计时仍无需联网。

验证范围为隔离模拟测试与构建检查，独立 Windows 虚拟机端到端验收仍待完成；不代表已在真实用户环境验证，也不保证 Claude 账号可用。

## v0.2.0-preview — maintenance and cleanup

**Whale Environment Helper 0.4.0** adds a separate **Maintenance & cleanup** entry:

- Optional read-only installation, proxy-variable, DNS and HTTPS checks. No credentials are sent with connection probes; results do not determine account or region eligibility.
- Claude Code configuration inspection and selective removal of old endpoint, authentication and model overrides, with local backups.
- Allowlisted cache/log scanning, byte counts, verified backups, Recycle Bin cleanup and conflict-aware restoration. Chats, cookies, login data, skills, plugins, hooks, custom rules and identity are preserved.
- Additional protections against linked files, unsafe Windows path aliases and cache changes between scan and cleanup.
- English / 中文 maintenance pages and isolated test fixtures. No developer-machine cleanup, real Claude configuration modification or live connectivity probe was performed for this release. Independent Windows VM end-to-end acceptance is pending.

Translator **0.8.1** and the clock EXE are unchanged. The README also links to the independent **Cockpit Tools** project; it is not bundled. Three Windows EXE downloads, with public source code.

环境助手 **0.4.0** 新增「维护与清理」：连接与安装体检、官方配置检查与选择性修复、缓存日志扫描清理、备份恢复。先查看范围，再核验备份后移入回收站；会话、登录数据、Skills 等保留。只完成隔离模拟测试与构建，未在个人环境执行清理或真实连接探测；仍待独立 Windows 虚拟机验收。翻译器及时钟 EXE 保持不变，Cockpit Tools 作为独立项目推荐。

## v0.1.0-preview — initial public preview

A Windows companion collection for using Claude:

- **Whale Translator 0.8.1:** translate selected Claude text; review Chinese-to-English prompts; choose a source window using searchable cards; use Google online translation or explicitly installed offline language pairs; retry a rate-limited request offline when a matching pack is available.
- **Two-Zone Clock:** keep your original/home region and target region visible in a compact vertical whale-themed panel. Search regions directly in the selector. Display preferences and daylight saving behavior follow the app's saved settings and Windows timezone rules.
- **Whale Environment Helper 0.3.0 preview:** measure browser signals first, optimize matched reversible local settings, measure again, and undo changes from backups.

English is the repository's default documentation language, with a Simplified Chinese README. This packaging does not change the apps' existing language defaults.

### Validation and limits

The environment helper has compiled and passed isolated JavaScript scoring checks. Independent Windows VM end-to-end acceptance is still pending; it is published as a preview. A build or a successful simulated score is not proof that a real user's environment changed or that Claude access will work.

The translator's earlier window-selection/copy workflow was user-confirmed in Claude. Newer packaging and offline features should be checked on the intended Windows machine before travel. Download the required language pairs while online; copying the EXE to a new computer does not copy its installed models.

Google online translation may be unavailable or rate-limited. Offline translation quality depends on the chosen model. The clock depends on Windows' clock accuracy and timezone rules. The helper does not alter network routing or account restrictions.

### Data handling

Source publishing excludes personal settings, backups, diagnostic records, account data, translation history and development handoffs. App configuration backups remain local.

## 首个公开预览版

三款工具围绕 Claude 使用体验：翻译 Claude 的回复与中文问题、查看自己原本地区的时间、先检测再调整可恢复的本地环境。

GitHub 默认英文，提供简体中文说明；软件默认语言保持现状。环境助手仍待独立 Windows 虚拟机端到端验收。离线使用前请联网安装需要的语言方向，并在目标电脑上检查。仓库不包含私人配置、备份、凭据、诊断记录或开发交接材料。

Only the three Windows EXE files are attached. No compilation is needed to use them; source remains public. Optional translator language packs are installed inside the app.

下载区仅提供三个 Windows EXE，双击运行，无需自行编译；源码公开。翻译器离线语言包在软件内选装。

Checksums / 校验值：[SHA256SUMS.txt](https://github.com/whitecarp/I-Must-Use-Claude/blob/main/docs/SHA256SUMS.txt)
