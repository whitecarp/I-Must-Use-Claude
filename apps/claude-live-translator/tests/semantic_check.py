"""Semantic check against the real Google endpoint (sends only the fixed sample sentences below).

Run from the project folder:  .\\runtime\\python\\python.exe tests\\semantic_check.py
It prints each sample with its translation, marks the ones worth a human look, and saves the same
report to tests\\semantic-report.txt. For samples with links or inline code it also shows what the
0.2 method (translating the pieces between them separately) produced, for comparison.
Stops at the first rate-limit / block / network error; never retries.
"""
import importlib.util
import pathlib
import re
import sys
import time

HERE = pathlib.Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("google_worker", HERE.parent / "backend" / "google_worker.py")
g = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g)

# (id, topic, direction, source, groups that must each match at least once, terms that must not appear)
CASES = [
    ("ui_empty_window", "软件界面·已知问题", "en_zh", "When I open the export panel, the preview shows an empty window.", [["窗口"]], ["窗户", "橱窗"]),
    ("ui_in_the_window", "软件界面·已知问题", "en_zh", "The timeline is in the window on the left; drag the clip into it.", [["窗口"]], ["橱窗", "窗户"]),
    ("ui_close_window", "软件界面", "en_zh", "Close the empty window and try again.", [["窗口"]], ["窗户"]),
    ("real_window", "真实窗户（不能被改成软件窗口）", "en_zh", "She opened the window to let the fresh air in.", [["窗户", "窗子"]], []),
    ("clothing_dress", "穿着·已知问题", "en_zh", "The girl in the video is wearing a light blue dress and white shoes.", [["穿"]], ["戴着"]),
    ("clothing_hat", "穿着", "en_zh", "The whale girl wears a hoodie and a small hat.", [["穿"]], []),
    ("neg_dont_delete", "否定", "en_zh", "Do not delete the original files; only change the last few seconds.", [["不要", "别", "请勿", "切勿", "不得"]], []),
    ("neg_not_yet", "否定", "en_zh", "I haven't rendered the final video yet, so nothing has been published.", [["还没", "尚未", "没有"]], []),
    ("neg_not_that", "否定", "en_zh", "It's not that the music is too loud; the problem is that it stops too suddenly.", [["不是"]], []),
    ("order_before", "时间顺序", "en_zh", "Before you export, check the colour settings, then render a short test clip.", [["之前", "前"], ["然后", "再"]], []),
    ("order_after", "时间顺序", "en_zh", "After the render finishes, I will compare the audio levels and only then upload it.", [["后"], ["然后", "才", "再"]], []),
    ("order_until", "时间顺序", "en_zh", "Wait until Codex replies before you start the next step.", [["等"], ["之前", "再", "然后", "才"]], []),
    ("ui_export_button", "软件界面", "en_zh", "Click the Export button in the top-right corner of the window.", [["导出"], ["窗口"]], ["窗户"]),
    ("ui_dropdown", "软件界面", "en_zh", "Select the correct Claude window from the drop-down list, then press Refresh.", [["下拉"], ["窗口"], ["刷新"]], ["窗户"]),
    ("ui_minimize", "软件界面", "en_zh", "Keep Claude open and don't minimize it, or the reader cannot see the reply.", [["最小化"]], []),
    ("video_trim", "视频剪辑", "en_zh", "Trim the clip so the cut lands on the beat, then add a short fade-out at the end.", [["剪"], ["拍"], ["淡出"]], []),
    ("video_render", "视频剪辑", "en_zh", "The render is 64 seconds long at 1080 by 1920 and 30 frames per second.", [["渲染"], ["帧"]], []),
    ("video_hard_stop", "视频剪辑", "en_zh", "The music stops abruptly on the first frame of the drag shot; should we restore the fade?", [["音乐"], ["淡", "渐"]], []),
    ("mixed_zh_en", "中英混合", "en_zh", "好的，I'll check the 全家福 shot first and keep Claude's hand unchanged.", [["全家福"], ["手"]], []),
    ("code_inline", "代码/链接", "en_zh", "Open `settings.json` in the editor window and set `timeout` to 30.", [["`settings.json`"], ["`timeout`"], ["30"]], ["窗户"]),
    ("code_url", "代码/链接", "en_zh", "Read the guide at https://example.com/docs/setup before you install anything.", [["https://example.com/docs/setup"], ["之前", "前"]], []),
    ("code_block", "代码/链接", "en_zh", "Run this command:\n\n```powershell\n& .\\build.ps1\n```\n\nIf it fails, send me the error message.", [["```powershell\n& .\\build.ps1\n```"], ["错误"]], []),
    ("code_two_inline", "代码/链接", "en_zh", "Drag `clip_03.mp4` into the timeline window, then press `Ctrl+S` to save.", [["`clip_03.mp4`"], ["`Ctrl+S`"], ["窗口"]], ["窗户", "橱窗"]),
    ("zh_keep_file", "中译英·否定", "zh_en", "请先帮我检查这个视频，不要改动原始文件。", [["video"], ["original"], ["not", "n't"]], []),
    ("zh_window", "中译英·软件界面", "zh_en", "预览窗口是空的，导出之后才会显示画面。", [["window"], ["empty", "blank"], ["export"]], ["shop window"]),
    ("zh_order", "中译英·时间顺序", "zh_en", "先把音乐换成淡出，再重新导出，最后发给我看。", [["fade"], ["export"], ["then", "after", "finally"]], []),
    ("zh_link", "中译英·代码/链接", "zh_en", "参考这个链接https://example.com/a，然后告诉我结果。", [["https://example.com/a"], ["result"]], []),
]

OLD_PROTECTED = re.compile(r"https?://[^\s]+|www\.[^\s]+|`[^`\n]+`", re.I)

# Mirror GlossaryStore.Defaults(), without reading the user's saved dictionary.
# test_semantic_check.py checks these entries against the C# defaults for drift.
DEFAULT_NAMES = (
    "Claude", "Claude Code", "Claude Desktop", "Codex", "Skills", "Anthropic",
    "OpenAI", "ChatGPT", "Gemini", "DeepSeek", "GitHub", "Git", "MCP", "API",
    "CLI", "SDK", "JSON", "YAML", "TOML", "Markdown", "Python", "JavaScript",
    "TypeScript", "PowerShell", "Node.js", "Docker", "React", "UTF-8",
    "Cockpit Tools", "CC Switch", "Opus", "Sonnet", "Haiku",
)
DEFAULT_PHRASES = (
    ("context window", "上下文窗口"), ("system prompt", "系统提示词"),
    ("tool call", "工具调用"), ("tool calls", "工具调用"),
    ("rate limit", "速率限制"), ("rate limiting", "限流"),
    ("code review", "代码审查"), ("pull request", "拉取请求"),
    ("merge conflict", "合并冲突"), ("unit test", "单元测试"),
    ("regression test", "回归测试"), ("environment variable", "环境变量"),
    ("API key", "API 密钥"), ("access token", "访问令牌"),
    ("command line", "命令行"), ("working directory", "工作目录"),
    ("source code", "源代码"), ("read-only", "只读"),
)
DEFAULT_REVERSE = (
    ("上下文窗口", "context window"), ("系统提示词", "system prompt"),
    ("工具调用", "tool call"), ("速率限制", "rate limit"),
    ("单元测试", "unit test"), ("回归测试", "regression test"),
    ("代码审查", "code review"), ("环境变量", "environment variable"),
)


def default_glossary():
    rows = [dict(source=name, replacement=name, target="*") for name in DEFAULT_NAMES]
    for source, replacement in DEFAULT_PHRASES:
        rows.append(dict(source=source, replacement=replacement, target="zh-CN"))
        initial = source[0].upper() + source[1:]
        if initial != source:
            rows.append(dict(source=initial, replacement=replacement, target="zh-CN"))
    rows.extend(dict(source=source, replacement=replacement, target="en")
                for source, replacement in DEFAULT_REVERSE)
    return rows


class OldWay(g.GoogleTranslator):
    """0.2 behaviour: text between links/inline code is translated piece by piece."""
    def paragraph(self, block, direction, deadline, glossary=()):
        # Ignore glossary entries for this historical comparison. fragments now
        # accepts explicit replacement text as its third tuple item.
        spans = [(m.start(), m.end(), m.group(0)) for m in OLD_PROTECTED.finditer(block)]
        return self.fragments(block, spans, direction, deadline)

requests = [0]
last_call = [0.0]
def gentle_google(text, direction):
    wait = 0.7 - (time.monotonic() - last_call[0])
    if wait > 0:
        time.sleep(wait)
    last_call[0] = time.monotonic()
    requests[0] += 1
    return g.call_google(text, direction)

STOP_MESSAGES = {
    "rate_limited": "Google 暂时限流。请过几分钟再运行。",
    "google_blocked": "Google 要求人机验证（常见于共用的代理/VPN 线路）。请稍后再试或换条线路。",
    "network_error": "连不上 Google。请检查网络或代理。",
    "access_denied": "Google 拒绝了请求。",
}

def check(case, text):
    _, _, direction, source, groups, avoid = case
    hay = text.lower() if direction == "zh_en" else text
    norm = (lambda s: s.lower()) if direction == "zh_en" else (lambda s: s)
    missing = [" / ".join(group) for group in groups if not any(norm(term) in hay for term in group)]
    # Brand names must retain their exact spelling and case in both directions.
    missing.extend(name for name in DEFAULT_NAMES if name in source and name not in text)
    present = [term for term in avoid if norm(term) in hay]
    return missing, present

def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(errors="replace")
    new, old = g.GoogleTranslator(gentle_google, 0), OldWay(gentle_google, 0)
    glossary = default_glossary()
    lines, flagged, done = [], 0, 0
    def out(text=""):
        print(text, flush=True)
        lines.append(text)
    out("大肥鱼翻译 · 语义检查（真实 Google 请求，只发送本脚本里的固定示例）")
    out("=" * 60)
    out("新版使用程序默认词典（%d 条）；旧方式忽略词典。" % len(glossary))
    stopped = None
    for case in CASES:
        cid, topic, direction, source, _, _ = case
        try:
            text = new.translate(source, direction, glossary)
            previous = old.translate(source, direction, glossary) if OLD_PROTECTED.search(source) else None
        except g.TranslationError as exc:
            stopped = str(exc)
            break
        done += 1
        missing, present = check(case, text)
        verdict = "OK" if not missing and not present else "看一下"
        flagged += verdict != "OK"
        out("[%s] %s（%s · %s）" % (verdict, cid, topic, "英→中" if direction == "en_zh" else "中→英"))
        out("  原文: " + source.replace("\n", "\n        "))
        out("  译文: " + text.replace("\n", "\n        "))
        if previous is not None and previous != text:
            out("  0.2 旧方式: " + previous.replace("\n", "\n        "))
        if missing:
            out("  缺少: " + "；".join(missing))
        if present:
            out("  不该出现: " + "、".join(present))
        out()
    out("=" * 60)
    if stopped:
        out("在第 %d 条停止：%s" % (done + 1, STOP_MESSAGES.get(stopped, "Google 翻译出错：" + stopped)))
    out("完成 %d/%d 条：OK %d，需要看一下 %d。Google 请求 %d 次；占位符被 Google 改坏、退回旧方式 %d 次。" % (
        done, len(CASES), done - flagged, flagged, requests[0], new.fallbacks))
    out("「看一下」只是自动初筛（按关键词），最终以人工判断为准。")
    report = HERE / "semantic-report.txt"
    report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("报告已保存：" + str(report))
    return 1 if stopped else 0

if __name__ == "__main__":
    raise SystemExit(main())
