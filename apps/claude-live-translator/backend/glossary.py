"""Bounded, literal user glossary. No file access, network, or text logging."""
import re


def normalize_target(code):
    return {"zh-CN": "zh", "zh-TW": "zt"}.get(code, code)


def validated_entries(raw):
    if raw is None:
        return []
    if not isinstance(raw, list) or len(raw) > 100:
        raise ValueError("invalid_glossary")
    result, seen = [], set()
    for entry in raw:
        if not isinstance(entry, dict):
            raise ValueError("invalid_glossary")
        source, replacement, target = (entry.get(k) for k in ("source", "replacement", "target"))
        if (not isinstance(source, str) or not isinstance(replacement, str) or
                not isinstance(target, str) or not source.strip() or not replacement.strip() or
                len(source) > 64 or len(replacement) > 128 or
                any(ord(c) < 32 for c in source + replacement) or
                not re.fullmatch(r"\*|[a-z]{2,3}(?:-[A-Z]{2})?", target)):
            raise ValueError("invalid_glossary")
        source, replacement, target = source.strip(), replacement.strip(), normalize_target(target)
        key = source, target
        if key in seen:
            raise ValueError("invalid_glossary")
        seen.add(key)
        result.append((source, replacement, target))
    return result


def replacement_spans(text, entries, target, protected):
    """Protected code/URL wins. Longest literal wins; target-specific wins over '*'."""
    target = normalize_target(target)
    protected = list(protected)
    candidates = {}
    for source, replacement, language in entries:
        if language == "*":
            candidates[source] = replacement
    for source, replacement, language in entries:
        if language == target:
            candidates[source] = replacement
    spans = [(a, b, text[a:b]) for a, b in protected]
    if not candidates:
        return spans
    patterns = []
    for source in sorted(candidates, key=lambda s: (-len(s), s)):
        # Latin identifiers must not match inside another identifier. Han phrases may be adjacent.
        left = r"(?<![A-Za-z0-9_])" if re.match(r"[A-Za-z0-9_]", source) else ""
        right = r"(?![A-Za-z0-9_])" if re.search(r"[A-Za-z0-9_]$", source) else ""
        patterns.append(left + re.escape(source) + right)
    pattern = re.compile("|".join(patterns))
    cursor = 0
    for start, end in protected + [(len(text), len(text))]:
        for match in pattern.finditer(text, cursor, start):
            spans.append((match.start(), match.end(), candidates[match.group(0)]))
        cursor = end
    return sorted(spans)
