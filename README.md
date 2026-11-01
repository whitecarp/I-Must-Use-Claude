# I Must Use Claude

**Read Claude comfortably. Keep your home time in view. Understand your local setup.**

我一定要用上Claude · Three whale-themed Windows companions for using Claude.

**English** · [简体中文](README.zh-CN.md)

## Three tools, one purpose

| App | What it helps you do |
| --- | --- |
| **Whale Translator / 大肥鱼翻译** | Understand Claude's replies in your preferred language, and turn Chinese prompts into English you can review before sending. |
| **Two-Zone Clock / 两地时钟** | Keep your original/home timezone visible when your computer is set to another timezone. |
| **Whale Environment Helper / 大肥鱼环境助手** | Measure browser environment signals, apply reversible changes to matched local settings, and measure again. |

These are independent community companion tools, not an Anthropic product. The environment score is a third-party heuristic, not an official Claude eligibility score or a guarantee of access.

## Download and start

**For convenience, we currently distribute Windows EXE files only. Download the app you want and double-click it—no source checkout or manual compilation needed. Source code remains public for developers.**

The translator bundles its runtime; optional offline language packs are installed separately inside the app.

Download from the [initial preview release](https://github.com/whitecarp/I-Must-Use-Claude/releases/tag/v0.1.0-preview), then double-click the app you need. Each app runs separately; you do not need to install all three.

| Download | Includes |
| --- | --- |
| [WhaleTranslator.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.1.0-preview/WhaleTranslator.exe) | Translator 0.8.1; optional language packs are downloaded separately |
| [HomeTimeClock.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.1.0-preview/HomeTimeClock.exe) | Two-Zone Clock, current build |
| [WhaleEnvironmentHelper.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.1.0-preview/WhaleEnvironmentHelper.exe) | Environment Helper 0.3.0 preview |

- Windows 10/11; the translator's portable runtime requires **x64**.
- The desktop apps use **.NET Framework 4.8**.
- Release executables are separate from the source download. Source ZIP files are for development.
- The repository opens in English and also has a Chinese README. The apps retain their existing language defaults and support English / 中文 switching.

### 1. Whale Translator

Choose the Claude window from a card showing its icon and title. Select text with the left mouse button and release: the app copies that selection and displays the original alongside its translation. Selections in other windows are ignored.

- Google online mode detects the source language and offers 17 target languages.
- The Chinese prompt window translates your message into English for review. Sending is an explicit action; copying and pasting is available when direct sending is unsupported.
- Optional offline language packs can be downloaded while online. Each pack covers one direction, such as English → Chinese; the reverse direction is a separate pack.
- If Google is rate-limiting or asking for verification, use **Switch to offline** to retry the current text with a compatible installed pack.
- Pin the window on top, pause selection translation, paste text manually, or copy the translation.

Online translation sends selected text to Google Translate's key-free web endpoint. It is not Gemini or the paid Google Cloud Translation API, and availability is not guaranteed. Offline mode translates on your device and does not silently switch online. Images and scanned documents are not OCR-translated.

### 2. Two-Zone Clock

Set the upper clock to your **original/home region** and the lower clock to your **target region**. This lets you keep track of home time while using a different computer timezone.

- Search directly inside either region selector, using English or Chinese.
- A small vertical desktop panel shows each region's time and date, with daylight saving changes handled by Windows timezone rules.
- Choose 12/24-hour time, seconds, one or two clocks, always-on-top, and a locked position.
- Drag it where you want, or return it to the lower-right corner. Settings persist between launches.

The clock works offline and does **not** change your computer's time or timezone. Choose a fixed home region if you want it to stay independent of future system timezone changes.

### 3. Whale Environment Helper — preview

Use **Measure → Optimize → Measure again**. The first measurement opens a local page in your default browser. Click **Start measurement** there, then return to the helper to see the score and individual results.

- Missing or failed checks remain **unknown**, not zero.
- Optimization requires recent results for the actionable local checks. It skips settings that did not match.
- Matched timezone settings can be changed to UTC; matched Chrome/Edge language preferences to English; verified matching personal fonts can be backed up and removed.
- System fonts remain protected. Network, proxy, DNS, Claude login, chats and credentials remain unchanged.
- Changes are backed up before application. **Undo** restores the previous values while retaining conflicting later edits.

Save your work and close Claude, Chrome and Edge before optimization or undo. Timezone changes affect the whole computer; removing a personal font can temporarily change how documents look. Use the same browser and profile for before/after checks.

The helper measures the browser page, not Claude Desktop's embedded environment. It cannot fix network routing or account restrictions. Version 0.3.0 has compiled and passed isolated scoring checks, but **independent Windows VM end-to-end acceptance is still pending**.

## What stays on your device?

| Tool | Network and local data |
| --- | --- |
| Translator | Online mode sends selected text to Google. Offline translation stays local. Language packs download only when requested. Source text and translations are not saved as history by this app. |
| Clock | No network requests or account access. Only display and region preferences are saved. |
| Environment Helper | The measurement page is served locally. Its disclosed WebRTC check contacts Google STUN; the result contains candidate counts, not raw IP addresses. Configuration backups stay local and may contain private browser data. |

Do not upload your local configuration backups, credentials, diagnostic folders or personal documents to this repository.

## Source and acknowledgements

- [Translator source](apps/claude-live-translator/)
- [Clock source](apps/two-zone-clock/)
- [Environment helper source](apps/whale-claude-helper/)
- [Third-party notices](THIRD_PARTY_NOTICES.md)
- [Release notes](RELEASE_NOTES.md)

The environment scoring rules are adapted from [LinXiaoTao/FuckClaude](https://github.com/LinXiaoTao/FuckClaude). Offline translation uses CTranslate2 and SentencePiece with optional packages from the Argos catalog. See the notices for licenses and scope.
Build instructions: [English / 中文](docs/BUILDING.md).
