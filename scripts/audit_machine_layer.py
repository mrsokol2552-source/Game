#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any
from urllib.parse import unquote


DEFAULT_REPO_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = DEFAULT_REPO_ROOT
JSON_FILES: list[Path] = []
MARKDOWN_ROOTS: list[Path] = []
CODE_ROOTS: list[Path] = []

OBSOLETE_PATTERNS = {
    "legacy_cyrillic_supplements_path": "Подолнения",
    "obsolete_editor_path": "Assets/Scripts/Editor",
}

MARKDOWN_LINK_RE = re.compile(r"\[(?P<text>[^\]]+)\]\((?P<target>[^)]+)\)")
MARKDOWN_LINK_OR_IMAGE_RE = re.compile(r"!?(\[[^\]]*\]\([^)]+\))")
URL_RE = re.compile(r"https?://\S+|mailto:\S+")
FILE_REF_RE = re.compile(
    r"(?P<ref>(?:\.\.?/)?(?:[A-Za-z0-9_.-]+\.(?:md|json|py|ps1|cs|unity|docx)|(?:docs|maps|supplements|scripts|My project|Sprites|LOGS UNITY)/(?:[A-Za-z0-9_ %.-]+/)*[A-Za-z0-9_ %.-]+\.(?:md|json|py|ps1|cs|unity|docx)))"
)
REPO_PATH_PREFIXES = (
    "README.md",
    "docs/",
    "maps/",
    "supplements/",
    "My project/",
    "Sprites/",
    "LOGS UNITY/",
    "scripts/",
)

CODE_ID_RE = re.compile(r"^\s*//\s*\[CODE-ID:\s*([A-Z0-9-]+)\]\s*$", re.M)
HEADER_FILE_RE = re.compile(r"^@file:\s*(.+)$", re.M)
SECTION_ID_DEF_RE = re.compile(r"^\s*//\s*\[([A-Z]{4,6}-\d{2})\]\s*$", re.M)
SECTION_ID_TOKEN_RE = re.compile(r"\b[A-Z]{4,6}-\d{2}\b")
HEADING_RE = re.compile(r"^(#{1,6})\s+(.*)$", re.M)
TILEMAP_BLOCK_READ_RE = re.compile(r"\.GetTilesBlock\s*\(")
LEGACY_TILEMAP_METHOD_RE = re.compile(
    r"^\s*(?:private|protected|public|internal)\s+(?:static\s+)?[^{;\n]*LegacyTilemap[^{;\n]*\)\s*$",
    re.M,
)


def configure_repo_root(repo_root: Path) -> None:
    global REPO_ROOT, JSON_FILES, MARKDOWN_ROOTS, CODE_ROOTS

    REPO_ROOT = repo_root.resolve()
    JSON_FILES = [
        REPO_ROOT / "docs" / "code_index.json",
        REPO_ROOT / "docs" / "document_roles_index.json",
        REPO_ROOT / "maps" / "repo_map.json",
        REPO_ROOT / "supplements" / "supplements_index.json",
        REPO_ROOT / "scripts" / "runtime_config_manifest.json",
        REPO_ROOT / "docs" / "runtime_config_export.json",
    ]
    MARKDOWN_ROOTS = [
        REPO_ROOT / "README.md",
        REPO_ROOT / "docs",
        REPO_ROOT / "maps",
        REPO_ROOT / "supplements",
    ]
    CODE_ROOTS = [
        REPO_ROOT / "My project" / "Assets" / "Scripts",
        REPO_ROOT / "My project" / "Assets" / "Editor",
        REPO_ROOT / "My project" / "Assets" / "Tests",
    ]


configure_repo_root(DEFAULT_REPO_ROOT)


@dataclass
class AuditReport:
    ok: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    errors: list[str] = field(default_factory=list)

    def add_ok(self, message: str) -> None:
        self.ok.append(message)

    def add_warning(self, message: str) -> None:
        self.warnings.append(message)

    def add_error(self, message: str) -> None:
        self.errors.append(message)

    @property
    def success(self) -> bool:
        return not self.errors


@dataclass
class CodeFileRecord:
    path: Path
    code_id: str | None
    header_file: str | None
    section_ids: set[str]


def collect_markdown_files() -> list[Path]:
    files: list[Path] = []
    for root in MARKDOWN_ROOTS:
        if root.is_file():
            files.append(root)
        elif root.is_dir():
            files.extend(sorted(root.rglob("*.md")))
    return files


def collect_code_files() -> list[Path]:
    files: list[Path] = []
    for root in CODE_ROOTS:
        if root.exists():
            files.extend(sorted(root.rglob("*.cs")))
    return files


def load_json(path: Path, report: AuditReport) -> Any | None:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001
        report.add_error(f"Invalid JSON: {path.relative_to(REPO_ROOT)} :: {exc}")
        return None
    report.add_ok(f"JSON parsed: {path.relative_to(REPO_ROOT)}")
    return data


def looks_like_repo_path(value: str) -> bool:
    if not isinstance(value, str):
        return False
    if value.startswith(("http://", "https://", "mailto:", "#")):
        return False
    return value.startswith(REPO_PATH_PREFIXES)


def validate_repo_path(path_str: str, owner: str, report: AuditReport) -> None:
    path = REPO_ROOT / unquote(path_str)
    if not path.exists():
        report.add_error(f"Missing path in {owner}: {path_str}")


def walk_json(value: Any, owner: str, report: AuditReport) -> None:
    if isinstance(value, dict):
        for inner in value.values():
            walk_json(inner, owner, report)
    elif isinstance(value, list):
        for inner in value:
            walk_json(inner, owner, report)
    elif looks_like_repo_path(value):
        validate_repo_path(value, owner, report)


def slugify_heading(text: str) -> str:
    text = re.sub(r"`+", "", text.strip())
    text = re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", text)
    text = text.lower()
    text = re.sub(r"[^a-z0-9 _-]", "", text)
    text = re.sub(r"\s+", "-", text.strip())
    text = re.sub(r"-{2,}", "-", text)
    return text.strip("-")


def build_markdown_anchor_index() -> dict[Path, set[str]]:
    index: dict[Path, set[str]] = {}
    for file in collect_markdown_files():
        content = file.read_text(encoding="utf-8")
        anchors: set[str] = set()
        for match in HEADING_RE.finditer(content):
            anchor = slugify_heading(match.group(2))
            if anchor:
                anchors.add(anchor)
        index[file.resolve()] = anchors
    return index


def parse_code_inventory() -> dict[Path, CodeFileRecord]:
    inventory: dict[Path, CodeFileRecord] = {}
    for file in collect_code_files():
        content = file.read_text(encoding="utf-8", errors="ignore")
        code_id_match = CODE_ID_RE.search(content)
        header_file_match = HEADER_FILE_RE.search(content)
        section_ids = set(SECTION_ID_DEF_RE.findall(content))
        inventory[file.relative_to(REPO_ROOT)] = CodeFileRecord(
            path=file.relative_to(REPO_ROOT),
            code_id=code_id_match.group(1) if code_id_match else None,
            header_file=header_file_match.group(1).strip() if header_file_match else None,
            section_ids=section_ids,
        )
    return inventory


def validate_markdown_links(report: AuditReport) -> None:
    broken_paths: list[str] = []
    broken_anchors: list[str] = []
    anchor_index = build_markdown_anchor_index()

    for file in collect_markdown_files():
        content = file.read_text(encoding="utf-8")
        for match in MARKDOWN_LINK_RE.finditer(content):
            target = match.group("target")
            if re.match(r"^(https?:|mailto:)", target):
                continue

            path_part, fragment = (target.split("#", 1) + [""])[:2]
            path_part = path_part.strip()
            fragment = fragment.strip()

            if not path_part:
                resolved = file.resolve()
            else:
                resolved = (file.parent / unquote(path_part)).resolve()

            if not resolved.exists():
                try:
                    rel = resolved.relative_to(REPO_ROOT)
                except ValueError:
                    rel = resolved
                broken_paths.append(f"{file.relative_to(REPO_ROOT)} -> {target} -> {rel}")
                continue

            if fragment and resolved.suffix.lower() == ".md":
                anchors = anchor_index.get(resolved, set())
                if fragment not in anchors:
                    broken_anchors.append(
                        f"{file.relative_to(REPO_ROOT)} -> {target} :: missing anchor #{fragment}"
                    )

    if broken_paths:
        for item in broken_paths:
            report.add_error(f"Broken markdown link: {item}")
    if broken_anchors:
        for item in broken_anchors:
            report.add_error(f"Broken markdown anchor: {item}")
    if not broken_paths and not broken_anchors:
        report.add_ok("Markdown links and anchors resolved across README.md, docs/, maps/, supplements/")


def validate_markdown_file_references(report: AuditReport) -> None:
    offenders: list[str] = []
    for file in collect_markdown_files():
        content = file.read_text(encoding="utf-8")
        in_fence = False
        for line_number, raw_line in enumerate(content.splitlines(), start=1):
            stripped = raw_line.strip()
            if stripped.startswith("```"):
                in_fence = not in_fence
                continue
            if in_fence:
                continue

            scrubbed = MARKDOWN_LINK_OR_IMAGE_RE.sub(" ", raw_line)
            scrubbed = URL_RE.sub(" ", scrubbed)

            for match in FILE_REF_RE.finditer(scrubbed):
                ref = match.group("ref").strip("`'\".,;:()[]{}<>")
                if not ref:
                    continue

                resolved = (file.parent / unquote(ref)).resolve()
                if not resolved.exists():
                    root_resolved = (REPO_ROOT / unquote(ref)).resolve()
                    if root_resolved.exists():
                        resolved = root_resolved
                    else:
                        continue

                offenders.append(f"{file.relative_to(REPO_ROOT)}:{line_number} -> {ref}")

    if offenders:
        for item in offenders:
            report.add_error(f"Plain file reference without Markdown link: {item}")
    else:
        report.add_ok("All file references in markdown layer use Markdown links")


def validate_obsolete_strings(report: AuditReport) -> None:
    found = False
    for file in collect_markdown_files():
        content = file.read_text(encoding="utf-8")
        for label, pattern in OBSOLETE_PATTERNS.items():
            if pattern in content:
                found = True
                report.add_error(
                    f"Obsolete string '{pattern}' ({label}) found in {file.relative_to(REPO_ROOT)}"
                )
    if not found:
        report.add_ok("No obsolete routing strings found in markdown layer")


def validate_duplicate_code_ids(inventory: dict[Path, CodeFileRecord], report: AuditReport) -> None:
    seen: dict[str, list[str]] = {}
    for record in inventory.values():
        if not record.code_id:
            continue
        seen.setdefault(record.code_id, []).append(record.path.as_posix())

    duplicates = {code_id: paths for code_id, paths in seen.items() if len(paths) > 1}
    if duplicates:
        for code_id, paths in duplicates.items():
            report.add_error(f"Duplicate CODE-ID {code_id}: {', '.join(paths)}")
    else:
        report.add_ok("No duplicate CODE-ID markers found in code layer")


def validate_header_file_paths(inventory: dict[Path, CodeFileRecord], report: AuditReport) -> None:
    mismatches: list[str] = []
    for record in inventory.values():
        if not record.header_file:
            continue
        header_norm = record.header_file.replace("\\", "/").strip()
        actual_norm = record.path.as_posix()
        if header_norm != actual_norm:
            mismatches.append(f"{actual_norm} :: @file={header_norm}")

    if mismatches:
        for item in mismatches:
            report.add_error(f"Header @file mismatch: {item}")
    else:
        report.add_ok("All declared @file headers match real code paths")


def validate_repo_map(data: dict[str, Any], report: AuditReport) -> None:
    roots = data.get("roots", [])
    docs = data.get("docs", [])
    if not isinstance(roots, list) or not roots:
        report.add_error("maps/repo_map.json :: missing or empty 'roots'")
    if not isinstance(docs, list) or not docs:
        report.add_error("maps/repo_map.json :: missing or empty 'docs'")


def validate_code_index(data: dict[str, Any], report: AuditReport) -> None:
    documents = data.get("documents", {})
    hot_ids = data.get("hot_ids", [])
    if not isinstance(documents, dict) or not documents:
        report.add_error("docs/code_index.json :: missing or empty 'documents'")
    if not isinstance(hot_ids, list) or not hot_ids:
        report.add_warning("docs/code_index.json :: hot_ids is empty")


def validate_document_roles_index(data: dict[str, Any], report: AuditReport) -> None:
    tracked = data.get("tracked_docs")
    ignored = data.get("ignored_docs")
    if not isinstance(tracked, list) or not tracked:
        report.add_error("docs/document_roles_index.json :: missing or empty tracked_docs")
    if not isinstance(ignored, list):
        report.add_error("docs/document_roles_index.json :: ignored_docs must be a list")


def validate_supplements_index(data: dict[str, Any], report: AuditReport) -> None:
    domains = data.get("domains", {})
    if not isinstance(domains, dict) or not domains:
        report.add_error("supplements/supplements_index.json :: missing or empty 'domains'")
        return

    audio = domains.get("audio", {}) if isinstance(domains, dict) else {}
    soundtrack_banks = audio.get("soundtrack_banks", []) if isinstance(audio, dict) else []
    for bank in soundtrack_banks:
        path_str = bank.get("path")
        expected = bank.get("file_count")
        if isinstance(path_str, str) and isinstance(expected, int):
            actual = len([p for p in (REPO_ROOT / path_str).glob("*") if p.is_file()])
            if actual != expected:
                report.add_error(
                    f"supplements_index mismatch: {path_str} declares {expected} files, actual {actual}"
                )

    sfx_categories = audio.get("sfx_categories", []) if isinstance(audio, dict) else []
    for category in sfx_categories:
        path_str = category.get("path")
        expected = category.get("file_count")
        if isinstance(path_str, str) and isinstance(expected, int):
            actual = len([p for p in (REPO_ROOT / path_str).glob("*") if p.is_file()])
            if actual != expected:
                report.add_error(
                    f"supplements_index mismatch: {path_str} declares {expected} files, actual {actual}"
                )


def validate_runtime_config_export(data: dict[str, Any], report: AuditReport) -> None:
    if not isinstance(data.get("scene_targets"), list) or not isinstance(data.get("prefab_targets"), list):
        report.add_error("docs/runtime_config_export.json :: missing scene_targets or prefab_targets")
    manifest = data.get("manifest")
    if not isinstance(manifest, str) or not (REPO_ROOT / manifest).exists():
        report.add_error("docs/runtime_config_export.json :: manifest path missing or invalid")
    generated_at = data.get("generated_at_utc")
    if not isinstance(generated_at, str) or not generated_at.strip():
        report.add_error("docs/runtime_config_export.json :: missing generated_at_utc")

    for category in ("scene_targets", "prefab_targets"):
        targets = data.get(category, [])
        if not isinstance(targets, list):
            continue
        for target in targets:
            if not isinstance(target, dict):
                report.add_error(f"docs/runtime_config_export.json :: invalid target entry in {category}")
                continue
            path_str = target.get("path")
            if not isinstance(path_str, str) or not (REPO_ROOT / path_str).exists():
                report.add_error(f"docs/runtime_config_export.json :: invalid target path in {category}: {path_str}")
            missing_game_objects = target.get("missing_game_objects")
            if isinstance(missing_game_objects, list) and missing_game_objects:
                report.add_error(
                    f"docs/runtime_config_export.json :: unresolved game objects in {path_str}: {', '.join(map(str, missing_game_objects))}"
                )
            exported = target.get("game_objects")
            if not isinstance(exported, list):
                report.add_error(
                    f"docs/runtime_config_export.json :: missing game_objects list in {path_str}"
                )


def validate_runtime_config_manifest(data: dict[str, Any], report: AuditReport) -> None:
    if not isinstance(data.get("output"), str):
        report.add_error("scripts/runtime_config_manifest.json :: missing output")
    else:
        output_path = REPO_ROOT / data["output"]
        if output_path.suffix.lower() != ".json":
            report.add_error("scripts/runtime_config_manifest.json :: output must be a .json file")
    if not isinstance(data.get("scene_targets"), list):
        report.add_error("scripts/runtime_config_manifest.json :: missing scene_targets")
    if not isinstance(data.get("prefab_targets"), list):
        report.add_error("scripts/runtime_config_manifest.json :: missing prefab_targets")
    script_roots = data.get("script_roots")
    if not isinstance(script_roots, list) or not script_roots:
        report.add_error("scripts/runtime_config_manifest.json :: missing or empty script_roots")
    else:
        for root in script_roots:
            if not isinstance(root, str) or not (REPO_ROOT / root).exists():
                report.add_error(f"scripts/runtime_config_manifest.json :: invalid script_root: {root}")

    for category in ("scene_targets", "prefab_targets"):
        targets = data.get(category, [])
        if not isinstance(targets, list):
            continue
        for target in targets:
            if not isinstance(target, dict):
                report.add_error(f"scripts/runtime_config_manifest.json :: invalid target entry in {category}")
                continue
            path_str = target.get("path")
            if not isinstance(path_str, str) or not (REPO_ROOT / path_str).exists():
                report.add_error(f"scripts/runtime_config_manifest.json :: invalid target path in {category}: {path_str}")
            include_component_types = target.get("include_component_types")
            if include_component_types is not None and not isinstance(include_component_types, list):
                report.add_error(
                    f"scripts/runtime_config_manifest.json :: include_component_types must be a list in {path_str}"
                )
            if category == "scene_targets":
                game_objects = target.get("game_objects")
                if game_objects is not None and not isinstance(game_objects, list):
                    report.add_error(
                        f"scripts/runtime_config_manifest.json :: game_objects must be a list in {path_str}"
                    )


def validate_runtime_config_sync(
    manifest: dict[str, Any] | None,
    export: dict[str, Any] | None,
    report: AuditReport,
) -> None:
    if not isinstance(manifest, dict) or not isinstance(export, dict):
        return

    manifest_scene_paths = {
        target["path"]
        for target in manifest.get("scene_targets", [])
        if isinstance(target, dict) and isinstance(target.get("path"), str)
    }
    export_scene_paths = {
        target["path"]
        for target in export.get("scene_targets", [])
        if isinstance(target, dict) and isinstance(target.get("path"), str)
    }
    if manifest_scene_paths != export_scene_paths:
        report.add_error(
            "runtime config sync :: scene target paths differ between manifest and export"
        )

    manifest_prefab_paths = {
        target["path"]
        for target in manifest.get("prefab_targets", [])
        if isinstance(target, dict) and isinstance(target.get("path"), str)
    }
    export_prefab_paths = {
        target["path"]
        for target in export.get("prefab_targets", [])
        if isinstance(target, dict) and isinstance(target.get("path"), str)
    }
    if manifest_prefab_paths != export_prefab_paths:
        report.add_error(
            "runtime config sync :: prefab target paths differ between manifest and export"
        )


def validate_section_id_references(
    inventory: dict[Path, CodeFileRecord],
    code_index: dict[str, Any] | None,
    repo_map: dict[str, Any] | None,
    report: AuditReport,
) -> None:
    known_sections = {section for record in inventory.values() for section in record.section_ids}
    missing: list[str] = []

    for file in collect_markdown_files():
        content = file.read_text(encoding="utf-8")
        for token in SECTION_ID_TOKEN_RE.findall(content):
            if token not in known_sections:
                missing.append(f"{file.relative_to(REPO_ROOT)} -> {token}")

    for owner, data in (
        ("docs/code_index.json", code_index or {}),
        ("maps/repo_map.json", repo_map or {}),
    ):
        raw = json.dumps(data, ensure_ascii=False)
        for token in SECTION_ID_TOKEN_RE.findall(raw):
            if token not in known_sections:
                missing.append(f"{owner} -> {token}")

    if missing:
        for item in sorted(set(missing)):
            report.add_error(f"Unknown section ID reference: {item}")
    else:
        report.add_ok("All section-ID references in docs and machine indexes resolve to real code markers")


def normalize_system_entries(value: Any) -> list[dict[str, Any]]:
    if isinstance(value, list):
        return [item for item in value if isinstance(item, dict)]
    if isinstance(value, dict):
        return [item for item in value.values() if isinstance(item, dict)]
    return []


def validate_machine_index_sync(
    inventory: dict[Path, CodeFileRecord],
    code_index: dict[str, Any] | None,
    repo_map: dict[str, Any] | None,
    report: AuditReport,
) -> None:
    errors: list[str] = []

    def validate_system_entries(owner: str, systems: list[dict[str, Any]]) -> None:
        for entry in systems:
            file_path = entry.get("file")
            expected_code_id = entry.get("code_id")
            expected_sections = entry.get("sections", [])
            if not isinstance(file_path, str):
                continue

            rel_path = Path(file_path)
            record = inventory.get(rel_path)
            if record is None:
                errors.append(f"{owner} :: hot file missing from code inventory: {file_path}")
                continue

            if isinstance(expected_code_id, str) and record.code_id != expected_code_id:
                errors.append(
                    f"{owner} :: CODE-ID mismatch for {file_path}: expected {expected_code_id}, actual {record.code_id}"
                )

            if not record.header_file:
                errors.append(f"{owner} :: missing @file header in hot file {file_path}")
            else:
                header_norm = record.header_file.replace("\\", "/").strip()
                if header_norm != rel_path.as_posix():
                    errors.append(
                        f"{owner} :: @file header mismatch for {file_path}: {header_norm}"
                    )

            if isinstance(expected_sections, list):
                missing_sections = [item for item in expected_sections if item not in record.section_ids]
                if missing_sections:
                    errors.append(
                        f"{owner} :: section mismatch for {file_path}: missing {', '.join(missing_sections)}"
                    )

    if isinstance(code_index, dict):
        validate_system_entries("docs/code_index.json", normalize_system_entries(code_index.get("systems", [])))
        for hot_id in code_index.get("hot_ids", []):
            if isinstance(hot_id, str) and hot_id.startswith(("SCRIPTS-", "EDITOR-", "TESTS-")):
                if not any(record.code_id == hot_id for record in inventory.values()):
                    errors.append(f"docs/code_index.json :: hot file id not found in code layer: {hot_id}")

    if isinstance(repo_map, dict):
        validate_system_entries("maps/repo_map.json", normalize_system_entries(repo_map.get("systems", [])))

    if errors:
        for item in errors:
            report.add_error(f"Machine/doc hot-file sync error: {item}")
    else:
        report.add_ok("Machine-layer hot-file entries match real code headers and section markers")


def find_matching_brace(content: str, open_brace: int) -> int:
    depth = 0
    for index in range(open_brace, len(content)):
        char = content[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return index
    return len(content)


def collect_legacy_tilemap_method_ranges(content: str) -> list[tuple[int, int]]:
    ranges: list[tuple[int, int]] = []
    for match in LEGACY_TILEMAP_METHOD_RE.finditer(content):
        open_brace = content.find("{", match.end())
        if open_brace < 0:
            continue
        ranges.append((match.start(), find_matching_brace(content, open_brace)))
    return ranges


def position_in_ranges(position: int, ranges: list[tuple[int, int]]) -> bool:
    return any(start <= position <= end for start, end in ranges)


def line_number_at(content: str, position: int) -> int:
    return content.count("\n", 0, position) + 1


def validate_tilemap_extraction_boundary(report: AuditReport) -> None:
    offenders: list[str] = []
    for file in collect_code_files():
        content = file.read_text(encoding="utf-8", errors="ignore")
        if "GetTilesBlock" not in content:
            continue

        legacy_ranges = collect_legacy_tilemap_method_ranges(content)
        for match in TILEMAP_BLOCK_READ_RE.finditer(content):
            if position_in_ranges(match.start(), legacy_ranges):
                continue
            offenders.append(f"{file.relative_to(REPO_ROOT)}:{line_number_at(content, match.start())}")

    if offenders:
        for item in offenders:
            report.add_error(f"Tilemap block read outside LegacyTilemap extraction helper: {item}")
    else:
        report.add_ok("Tilemap block reads are isolated behind LegacyTilemap extraction helpers")


def render_report(report: AuditReport) -> str:
    lines: list[str] = []
    lines.append("Machine Layer Audit")
    lines.append(f"Repo root: {REPO_ROOT}")
    lines.append("")
    lines.append(f"OK: {len(report.ok)}")
    lines.append(f"Warnings: {len(report.warnings)}")
    lines.append(f"Errors: {len(report.errors)}")
    lines.append("")
    if report.ok:
        lines.append("[OK]")
        lines.extend(f"- {item}" for item in report.ok)
        lines.append("")
    if report.warnings:
        lines.append("[Warnings]")
        lines.extend(f"- {item}" for item in report.warnings)
        lines.append("")
    if report.errors:
        lines.append("[Errors]")
        lines.extend(f"- {item}" for item in report.errors)
        lines.append("")
    lines.append("Result: PASS" if report.success else "Result: FAIL")
    return "\n".join(lines)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate the machine-readable documentation/navigation layer."
    )
    parser.add_argument(
        "--repo-root",
        default=str(DEFAULT_REPO_ROOT),
        help="Repository root to audit. Defaults to the current project root.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    configure_repo_root(Path(args.repo_root))

    report = AuditReport()
    parsed: dict[Path, Any] = {}

    for json_file in JSON_FILES:
        data = load_json(json_file, report)
        if data is not None:
            parsed[json_file] = data
            walk_json(data, str(json_file.relative_to(REPO_ROOT)), report)

    code_index = parsed.get(REPO_ROOT / "docs" / "code_index.json")
    document_roles_index = parsed.get(REPO_ROOT / "docs" / "document_roles_index.json")
    repo_map = parsed.get(REPO_ROOT / "maps" / "repo_map.json")
    supplements_index = parsed.get(REPO_ROOT / "supplements" / "supplements_index.json")
    runtime_manifest = parsed.get(REPO_ROOT / "scripts" / "runtime_config_manifest.json")
    runtime_export = parsed.get(REPO_ROOT / "docs" / "runtime_config_export.json")

    if isinstance(code_index, dict):
        validate_code_index(code_index, report)
    if isinstance(document_roles_index, dict):
        validate_document_roles_index(document_roles_index, report)
    if isinstance(repo_map, dict):
        validate_repo_map(repo_map, report)
    if isinstance(supplements_index, dict):
        validate_supplements_index(supplements_index, report)
    if isinstance(runtime_manifest, dict):
        validate_runtime_config_manifest(runtime_manifest, report)
    if isinstance(runtime_export, dict):
        validate_runtime_config_export(runtime_export, report)
    validate_runtime_config_sync(
        runtime_manifest if isinstance(runtime_manifest, dict) else None,
        runtime_export if isinstance(runtime_export, dict) else None,
        report,
    )

    inventory = parse_code_inventory()

    validate_markdown_links(report)
    validate_markdown_file_references(report)
    validate_obsolete_strings(report)
    validate_duplicate_code_ids(inventory, report)
    validate_header_file_paths(inventory, report)
    validate_section_id_references(inventory, code_index if isinstance(code_index, dict) else None, repo_map if isinstance(repo_map, dict) else None, report)
    validate_machine_index_sync(inventory, code_index if isinstance(code_index, dict) else None, repo_map if isinstance(repo_map, dict) else None, report)
    validate_tilemap_extraction_boundary(report)

    print(render_report(report))
    return 0 if report.success else 1


if __name__ == "__main__":
    raise SystemExit(main())
