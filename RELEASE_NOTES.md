# Release notes

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
