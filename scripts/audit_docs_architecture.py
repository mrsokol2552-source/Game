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
DEFAULT_MANIFEST = DEFAULT_REPO_ROOT / "docs" / "document_roles_index.json"
TRACKED_MD_ROOTS = ["README.md", "docs", "maps", "supplements"]
MARKDOWN_LINK_RE = re.compile(r"\[(?P<text>[^\]]+)\]\((?P<target>[^)]+)\)")
HEADING_RE = re.compile(r"^(#{1,6})\s+(.*)$", re.M)


@dataclass
class AuditReport:
    ok: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)
    errors: list[str] = field(default_factory=list)

    @property
    def success(self) -> bool:
        return not self.errors

    def add_ok(self, message: str) -> None:
        self.ok.append(message)

    def add_warning(self, message: str) -> None:
        self.warnings.append(message)

    def add_error(self, message: str) -> None:
        self.errors.append(message)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate documentation role boundaries and tracked doc architecture."
    )
    parser.add_argument(
        "--repo-root",
        default=str(DEFAULT_REPO_ROOT),
        help="Repository root to audit. Defaults to the current project root.",
    )
    parser.add_argument(
        "--manifest",
        default=None,
        help="Optional manifest override. Defaults to docs/document_roles_index.json.",
    )
    return parser.parse_args()


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def collect_markdown_docs(repo_root: Path) -> set[str]:
    docs: set[str] = set()
    for relative in TRACKED_MD_ROOTS:
        root = repo_root / relative
        if root.is_file():
            docs.add(root.relative_to(repo_root).as_posix())
        elif root.is_dir():
            for item in sorted(root.rglob("*.md")):
                docs.add(item.relative_to(repo_root).as_posix())
    return docs


def slugify_heading(text: str) -> str:
    text = re.sub(r"`+", "", text.strip())
    text = re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", text)
    text = text.lower()
    text = re.sub(r"[^a-z0-9 _-]", "", text)
    text = re.sub(r"\s+", "-", text.strip())
    text = re.sub(r"-{2,}", "-", text)
    return text.strip("-")


def parse_links(doc_path: Path, repo_root: Path) -> set[str]:
    content = doc_path.read_text(encoding="utf-8")
    resolved_links: set[str] = set()
    for match in MARKDOWN_LINK_RE.finditer(content):
        target = match.group("target").strip()
        if not target or target.startswith(("http://", "https://", "mailto:", "#")):
            continue
        path_part = target.split("#", 1)[0].strip()
        if not path_part:
            continue
        resolved = (doc_path.parent / unquote(path_part)).resolve()
        if resolved.exists():
            try:
                resolved_links.add(resolved.relative_to(repo_root).as_posix())
            except ValueError:
                pass
    return resolved_links


def count_h1(doc_path: Path) -> int:
    content = doc_path.read_text(encoding="utf-8")
    return sum(1 for match in HEADING_RE.finditer(content) if match.group(1) == "#")


def validate_manifest_shape(data: dict[str, Any], report: AuditReport) -> None:
    tracked = data.get("tracked_docs")
    ignored = data.get("ignored_docs")
    if not isinstance(tracked, list) or not tracked:
        report.add_error("document_roles_index.json :: missing or empty tracked_docs")
    if not isinstance(ignored, list):
        report.add_error("document_roles_index.json :: ignored_docs must be a list")


def validate_role_uniqueness(entries: list[dict[str, Any]], report: AuditReport) -> None:
    seen_paths: dict[str, str] = {}
    seen_roles: dict[str, str] = {}
    tag_owners: dict[str, str] = {}

    for entry in entries:
        path = entry.get("path")
        role_id = entry.get("role_id")
        ownership_tags = entry.get("ownership_tags", [])

        if isinstance(path, str):
            previous = seen_paths.get(path)
            if previous is not None:
                report.add_error(f"document_roles_index.json :: duplicate tracked path {path} ({previous}, {role_id})")
            else:
                seen_paths[path] = str(role_id)

        if isinstance(role_id, str):
            previous = seen_roles.get(role_id)
            if previous is not None:
                report.add_error(f"document_roles_index.json :: duplicate role_id {role_id} ({previous}, {path})")
            else:
                seen_roles[role_id] = str(path)

        if isinstance(ownership_tags, list):
            for tag in ownership_tags:
                if not isinstance(tag, str):
                    report.add_error(f"document_roles_index.json :: non-string ownership tag in {path}")
                    continue
                previous = tag_owners.get(tag)
                if previous is not None:
                    report.add_error(f"document_roles_index.json :: ownership tag overlap '{tag}' ({previous}, {path})")
                else:
                    tag_owners[tag] = str(path)


def validate_doc_registry(
    repo_root: Path,
    entries: list[dict[str, Any]],
    ignored_docs: list[str],
    report: AuditReport,
) -> None:
    tracked_paths = {entry["path"] for entry in entries if isinstance(entry.get("path"), str)}
    ignored_paths = {path for path in ignored_docs if isinstance(path, str)}
    actual_docs = collect_markdown_docs(repo_root)

    missing = tracked_paths - actual_docs
    untracked = actual_docs - tracked_paths - ignored_paths

    for path in sorted(missing):
        report.add_error(f"Tracked doc missing on disk: {path}")
    for path in sorted(untracked):
        report.add_error(f"Untracked markdown doc under governed roots: {path}")

    if not missing and not untracked:
        report.add_ok("All governed markdown docs are registered exactly once")


def validate_docs(repo_root: Path, entries: list[dict[str, Any]], report: AuditReport) -> None:
    for entry in entries:
        path_str = entry.get("path")
        layer = entry.get("layer")
        required_links = entry.get("required_links", [])

        if not isinstance(path_str, str):
            report.add_error("document_roles_index.json :: tracked_docs entry missing string path")
            continue

        doc_path = repo_root / path_str
        if not doc_path.exists():
            continue

        if not isinstance(layer, str) or not layer:
            report.add_error(f"document_roles_index.json :: missing layer for {path_str}")

        if layer == "docs" and not path_str.startswith("docs/"):
            report.add_error(f"document_roles_index.json :: layer/path mismatch for {path_str}")
        if layer == "maps" and not path_str.startswith("maps/"):
            report.add_error(f"document_roles_index.json :: layer/path mismatch for {path_str}")
        if layer == "supplements" and not path_str.startswith("supplements/"):
            report.add_error(f"document_roles_index.json :: layer/path mismatch for {path_str}")
        if layer == "root" and "/" in path_str:
            report.add_error(f"document_roles_index.json :: layer/path mismatch for {path_str}")

        h1_count = count_h1(doc_path)
        if h1_count != 1:
            report.add_error(f"{path_str} :: expected exactly one H1, found {h1_count}")

        if not isinstance(required_links, list):
            report.add_error(f"document_roles_index.json :: required_links must be a list in {path_str}")
            continue

        actual_links = parse_links(doc_path, repo_root)
        for required in required_links:
            if not isinstance(required, str):
                report.add_error(f"document_roles_index.json :: non-string required link in {path_str}")
                continue
            required_path = (repo_root / required).resolve()
            if not required_path.exists():
                report.add_error(f"document_roles_index.json :: required link target does not exist: {required}")
                continue
            normalized = required_path.relative_to(repo_root).as_posix()
            if normalized not in actual_links:
                report.add_error(f"{path_str} :: missing required markdown link to {normalized}")


def render_report(report: AuditReport, repo_root: Path, manifest_path: Path) -> str:
    lines: list[str] = []
    lines.append("Documentation Architecture Audit")
    lines.append(f"Repo root: {repo_root}")
    lines.append(f"Manifest: {manifest_path.relative_to(repo_root).as_posix()}")
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


def main() -> int:
    args = parse_args()
    repo_root = Path(args.repo_root).resolve()
    manifest_path = Path(args.manifest).resolve() if args.manifest else repo_root / "docs" / "document_roles_index.json"
    report = AuditReport()

    if not manifest_path.exists():
        report.add_error(f"Manifest not found: {manifest_path}")
        print(render_report(report, repo_root, manifest_path))
        return 1

    data = load_json(manifest_path)
    validate_manifest_shape(data, report)
    tracked_docs = data.get("tracked_docs", [])
    ignored_docs = data.get("ignored_docs", [])

    if isinstance(tracked_docs, list):
        validate_role_uniqueness(tracked_docs, report)
        validate_doc_registry(repo_root, tracked_docs, ignored_docs if isinstance(ignored_docs, list) else [], report)
        validate_docs(repo_root, tracked_docs, report)

    if report.success:
        report.add_ok("Documentation role boundaries and required cross-links are consistent")

    print(render_report(report, repo_root, manifest_path))
    return 0 if report.success else 1


if __name__ == "__main__":
    raise SystemExit(main())
