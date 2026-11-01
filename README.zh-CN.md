# 我一定要用上Claude

**看懂 Claude 的回复，记住自己那边几点，了解使用环境。**

I Must Use Claude · 围绕 Claude 使用体验制作的三款鲸鱼主题 Windows 工具。

[English](README.md) · **简体中文**

## 三个软件，围绕同一件事

| 软件 | 帮你解决什么 |
| --- | --- |
| **大肥鱼翻译 / Whale Translator** | 把 Claude 的回复翻成你看得懂的语言，也把中文问题翻成英文，预览后再发送。 |
| **两地时钟 / Two-Zone Clock** | 电脑设成其他时区后，依然随时知道自己原本地区的时间。 |
| **大肥鱼环境助手 / Whale Environment Helper** | 先测浏览器环境，再处理命中的可恢复本地设置，最后复测对比。 |

这是独立的辅助工具箱，不是 Anthropic 官方产品。环境分数来自第三方项目规则，不是 Claude 官方资格评分，也不保证解除访问限制。

**为方便使用，目前仅提供 Windows EXE 下载。下载需要的软件后双击即可，无需下载源码或自行编译；源码仍公开。翻译器自带运行库，离线语言包按需安装。**

## 下载使用

打开[首个预览版下载页](https://github.com/whitecarp/I-Must-Use-Claude/releases/tag/v0.1.0-preview)，下载需要的软件 EXE，双击打开。三个软件独立运行，不必全部安装。

| 下载 | 内容 |
| --- | --- |
| [WhaleTranslator.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.1.0-preview/WhaleTranslator.exe) | 大肥鱼翻译 0.8.1；离线语言包另行选装 |
| [HomeTimeClock.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.1.0-preview/HomeTimeClock.exe) | 两地时钟当前版本 |
| [WhaleEnvironmentHelper.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.1.0-preview/WhaleEnvironmentHelper.exe) | 大肥鱼环境助手 0.3.0 预览版 |

- 适用于 Windows 10/11；翻译器便携运行库需要 **x64**。
- 桌面程序使用 **.NET Framework 4.8**。
- 下载 Release 中的程序即可；源码 ZIP 面向开发者。
- GitHub 默认展示英文，并提供这份中文说明。软件自身默认语言保持现状，支持 English / 中文切换。

### ① 大肥鱼翻译

在窗口卡片里选择 Claude，然后按住鼠标左键选中文字，松开后显示原文与译文。其他窗口的划选会被忽略。

- Google 在线模式自动识别原文，提供 17 种目标语言。
- 「中文提问」把中文翻成英文，先预览再主动发送；不支持直接发送时可复制后手动粘贴。
- 有网时按需下载离线语言包。每个包对应一个方向，例如英文 → 中文；反向需要另装一个包。
- Google 限流或要求验证时，可点「改用离线翻译」，使用兼容的已安装语言包重译当前内容。
- 支持置顶、暂停划选、手动粘贴和复制译文。

在线模式会把所选文字发给 Google 的免密钥网页翻译接口，并非 Gemini 或付费 Google Cloud Translation API，可能遇到限流或不可用。离线模式只在本机翻译，不会悄悄切回联网。图片和扫描件不做 OCR。

### ② 两地时钟

上方设成**原本地区**，下方设成**目标地区**。这样即使电脑时区改了，也不会忘记自己那边现在几点。

- 直接在地区下拉框里搜索中文或英文名称。
- 小面板上下排列，显示各地时间、日期，按 Windows 时区规则自动处理夏令时。
- 支持 12/24 小时制、秒数、单/双时钟、置顶与锁定位置。
- 可以拖动，也能放回屏幕右下角；重新打开后保留设置。

时钟不联网，也**不修改电脑时间或时区**。想让原本地区不随系统变化，请选择固定地区，而不是「跟随电脑时区」。

### ③ 大肥鱼环境助手 · 预览版

流程是 **检测 → 一键优化 → 复测**。首次检测会打开默认浏览器的本机页面，点击「开始测量」后，回到助手查看分数与明细。

- 检测不到或失败的项目显示**未知**，不当作 0 分。
- 必须有近期、完整的可处理本地项目结果才能优化；没有命中的设置跳过。
- 命中时可调整为 UTC、将被测 Chrome/Edge 的语言偏好设为英语，或备份移除确切匹配的个人字体。
- 系统字体受保护。网络、代理、DNS、Claude 登录、聊天与凭据均不修改。
- 先备份再修改，提供「撤回」；发生后续修改冲突时保留新内容并提示。

优化或撤回前，请保存工作并正常退出 Claude、Chrome 和 Edge。时区修改影响整台电脑；移除个人字体可能使文档临时使用替代字体。前后检测请使用相同浏览器与个人资料。

检测对象是浏览器页面，不是 Claude Desktop 内嵌环境；它不能修复网络路线或账号限制。0.3.0 已编译并通过隔离评分检查，但**仍待独立 Windows 虚拟机端到端验收**。

## 数据和联网

| 软件 | 数据处理 |
| --- | --- |
| 翻译器 | 在线模式发送所选文字给 Google；离线翻译在本机完成。语言包按用户操作下载，不保存原文和译文历史。 |
| 时钟 | 不联网、不读账号，只保存显示与地区偏好。 |
| 环境助手 | 测量页在本机提供。页面说明的 WebRTC 项目会联系 Google STUN，只回传候选数量，不回传原始 IP。配置备份留在本机，可能包含私人浏览器数据。 |

请勿把本机配置备份、凭据、诊断目录或私人文件上传到仓库。

## 源码与致谢

- [翻译器源码](apps/claude-live-translator/)
- [时钟源码](apps/two-zone-clock/)
- [环境助手源码](apps/whale-claude-helper/)
- [第三方声明](THIRD_PARTY_NOTICES.md)
- [发布说明](RELEASE_NOTES.md)

环境评分参考 [LinXiaoTao/FuckClaude](https://github.com/LinXiaoTao/FuckClaude)；离线翻译使用 CTranslate2、SentencePiece，以及用户选装的 Argos 目录语言包。许可与范围见第三方声明。
构建说明：[English / 中文](docs/BUILDING.md)。
