"""Check language files without starting the game or evaluating .NET regex rules."""

import argparse
import json
import re
from collections import Counter
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
LANGUAGE_ID = re.compile(r"[a-zA-Z][a-zA-Z0-9-]{0,31}\Z")
PLACEHOLDERS = re.compile(r"\{\{?\w+(?:[^{}]*?)\}\}?")
MARKUP = re.compile(r"</?(?:b|i|u|s|color|size|font|sprite|link|align|br|sub|sup|anim)(?:[:= ][^>]*)?/?>")
NUMBERS = re.compile(r"\d+(?:[.,]\d+)*")
LEADING_SPACE = re.compile(r"^\s*")
TRAILING_SPACE = re.compile(r"\s*$")
MAX_TEXT_LENGTH = 32768


def reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON key: {key[:120]}")
        result[key] = value
    return result


def read_pack(path: Path) -> dict[str, Any]:
    if path.stat().st_size > 16 * 1024 * 1024:
        raise ValueError("Pack exceeds 16 MiB")
    pack = json.loads(
        path.read_text(encoding="utf-8-sig"), object_pairs_hook=reject_duplicate_keys
    )
    if not isinstance(pack, dict):
        raise ValueError("Pack must be a JSON object")
    return pack


def validate_metadata(pack: dict[str, Any], path: Path) -> list[str]:
    issues = []
    language = pack.get("id")
    if pack.get("schemaVersion") != 1:
        issues.append("schemaVersion must be 1")
    if not isinstance(language, str) or not LANGUAGE_ID.fullmatch(language):
        issues.append("Invalid language id")
    elif language != path.stem:
        issues.append("Language id must match the filename")
    for field in ("name", "nativeName"):
        value = pack.get(field)
        if not isinstance(value, str) or not value.strip() or len(value) > 128:
            issues.append(f"{field} must contain 1 to 128 characters")
    font = pack.get("font", "Arial")
    if not isinstance(font, str) or len(font) > 128:
        issues.append("Invalid font metadata")
    strings = pack.get("strings")
    if not isinstance(strings, dict) or len(strings) > 50000:
        issues.append("strings must be an object with at most 50000 entries")
    rules = pack.get("rules", [])
    if not isinstance(rules, list) or len(rules) > 1024:
        return issues + ["rules must be an array with at most 1024 entries"]
    for index, rule in enumerate(rules):
        if not isinstance(rule, dict):
            issues.append(f"Rule {index}: expected an object")
            continue
        pattern = rule.get("pattern")
        replacement = rule.get("replacement")
        groups = rule.get("translateGroups", [])
        valid_pattern = (
            isinstance(pattern, str)
            and pattern.startswith("^")
            and pattern.endswith("$")
            and len(pattern) <= 4096
        )
        if not valid_pattern:
            issues.append(f"Rule {index}: pattern must be anchored and at most 4096 characters")
        if not isinstance(replacement, str) or len(replacement) > MAX_TEXT_LENGTH:
            issues.append(f"Rule {index}: invalid replacement")
        if not isinstance(groups, list) or any(
            not isinstance(group, str) or not group or group == "0" for group in groups
        ):
            issues.append(f"Rule {index}: invalid translateGroups")
    return issues


def boundary_spaces(text: str) -> tuple[str, str]:
    # Packs normalize nonbreaking and repeated spaces, but must keep separators.
    def normalize(value: str) -> str:
        return re.sub(r"[^\S\r\n]+", " ", value.replace("\r\n", "\n"))

    return normalize(LEADING_SPACE.match(text).group()), normalize(TRAILING_SPACE.search(text).group())


def validate_strings(pack: dict[str, Any], english: dict[str, str]):
    issues = []
    numeric_notes = []
    for source, translated in pack["strings"].items():
        label = repr(source[:120])
        if source not in english:
            issues.append(f"Source key absent from English: {label}")
        if not isinstance(translated, str):
            issues.append(f"Value is not text: {label}")
            continue
        if len(source) > MAX_TEXT_LENGTH or len(translated) > MAX_TEXT_LENGTH:
            issues.append(f"Text exceeds {MAX_TEXT_LENGTH} characters: {label}")
        if not translated:
            continue  # Empty entries deliberately fall back to the original text.
        # Word order may change; each format placeholder must still occur as often.
        if Counter(PLACEHOLDERS.findall(source)) != Counter(PLACEHOLDERS.findall(translated)):
            issues.append(f"Changed format placeholders: {label}")
        if MARKUP.findall(source) != MARKUP.findall(translated):
            issues.append(f"Changed rich text tags: {label}")
        if boundary_spaces(source) != boundary_spaces(translated):
            issues.append(f"Changed fragment boundary whitespace: {label}")
        if pack["id"] != "en" and NUMBERS.findall(source) != NUMBERS.findall(translated):
            numeric_notes.append({
                "language": pack["id"], "source": source, "translation": translated
            })
    return issues, numeric_notes


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--languages", type=Path, default=ROOT / "languages")
    parser.add_argument("--report", type=Path, default=ROOT / "work/numeric-review.json")
    args = parser.parse_args()
    try:
        english = read_pack(args.languages / "en.json")["strings"]
        if not isinstance(english, dict):
            raise ValueError("English strings must be an object")
    except (OSError, ValueError, KeyError) as error:
        print(f"ERROR: Cannot read English template: {error}")
        return 1

    issues = []
    numeric_notes = []
    seen_ids = set()
    for path in sorted(args.languages.glob("*.json")):
        try:
            pack = read_pack(path)
            metadata_issues = validate_metadata(pack, path)
            if metadata_issues:
                issues.extend(f"{path.name}: {issue}" for issue in metadata_issues)
                continue
            identity = pack["id"].casefold()
            if identity in seen_ids:
                issues.append(f"{path.name}: Duplicate language id")
            seen_ids.add(identity)
            string_issues, notes = validate_strings(pack, english)
            issues.extend(f"{path.name}: {issue}" for issue in string_issues)
            numeric_notes.extend(notes)
            print(f"{path.name}: {len(pack['strings'])} strings, {len(pack.get('rules', []))} rules")
        except (OSError, ValueError) as error:
            issues.append(f"{path.name}: {error}")

    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(
        json.dumps(numeric_notes, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    for issue in issues:
        print(f"ERROR: {issue}")
    print(f"{len(issues)} blocking issues; {len(numeric_notes)} numeric changes to review.")
    return int(bool(issues))


if __name__ == "__main__":
    raise SystemExit(main())
