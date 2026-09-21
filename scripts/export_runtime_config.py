#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


DEFAULT_REPO_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = DEFAULT_REPO_ROOT
DOC_SPLIT_RE = re.compile(r"^--- !u!(?P<class_id>\d+) &(?P<file_id>-?\d+)\s*$", re.M)
GUID_RE = re.compile(r"^guid:\s*([0-9a-f]+)\s*$", re.M)
CODE_ID_RE = re.compile(r"^\s*//\s*\[CODE-ID:\s*([A-Z0-9-]+)\]\s*$", re.M)
FILE_HEADER_RE = re.compile(r"^@file:\s*(.+)$", re.M)


def configure_repo_root(repo_root: Path) -> None:
    global REPO_ROOT
    REPO_ROOT = repo_root.resolve()


@dataclass
class ScriptInfo:
    guid: str
    path: str
    code_id: str | None
    header_file: str | None


@dataclass
class UnityDocument:
    class_id: int
    file_id: str
    component_type: str
    data: dict[str, Any]


def split_top_level(value: str, delimiter: str = ",") -> list[str]:
    parts: list[str] = []
    current: list[str] = []
    depth = 0
    for char in value:
        if char in "{[":
            depth += 1
        elif char in "}]":
            depth = max(0, depth - 1)
        if char == delimiter and depth == 0:
            parts.append("".join(current).strip())
            current = []
            continue
        current.append(char)
    tail = "".join(current).strip()
    if tail:
        parts.append(tail)
    return parts


def parse_scalar(value: str) -> Any:
    value = value.strip()
    if not value:
        return ""
    if value in {"[]", "{}"}:
        return [] if value == "[]" else {}
    if value.startswith("{") and value.endswith("}"):
        inner = value[1:-1].strip()
        if not inner:
            return {}
        result: dict[str, Any] = {}
        for part in split_top_level(inner):
            key, _, raw = part.partition(":")
            result[key.strip()] = parse_scalar(raw)
        return result
    if value.startswith("[") and value.endswith("]"):
        inner = value[1:-1].strip()
        if not inner:
            return []
        return [parse_scalar(part) for part in split_top_level(inner)]
    if value.startswith(("'", '"')) and value.endswith(("'", '"')) and len(value) >= 2:
        return value[1:-1]
    if value in {"true", "false"}:
        return value == "true"
    if re.fullmatch(r"-?\d+", value):
        try:
            return int(value)
        except ValueError:
            return value
    if re.fullmatch(r"-?\d+\.\d+(e[-+]?\d+)?", value, flags=re.I):
        try:
            return float(value)
        except ValueError:
            return value
    return value


def tokenize_yaml_lines(lines: list[str]) -> list[tuple[int, str]]:
    tokens: list[tuple[int, str]] = []
    for raw in lines:
        if not raw.strip():
            continue
        indent = len(raw) - len(raw.lstrip(" "))
        tokens.append((indent, raw.strip()))
    return tokens


def parse_key_value(text: str) -> tuple[str, str | None]:
    key, _, remainder = text.partition(":")
    remainder = remainder.strip()
    return key.strip(), (None if remainder == "" else remainder)


def parse_yaml_block(tokens: list[tuple[int, str]], index: int, indent: int) -> tuple[Any, int]:
    if index >= len(tokens):
        return {}, index

    if tokens[index][1].startswith("- "):
        items: list[Any] = []
        while index < len(tokens) and tokens[index][0] == indent and tokens[index][1].startswith("- "):
            item_text = tokens[index][1][2:].strip()
            index += 1
            if not item_text:
                if index < len(tokens) and tokens[index][0] > indent:
                    item_value, index = parse_yaml_block(tokens, index, tokens[index][0])
                else:
                    item_value = None
            elif ":" in item_text:
                item_key, item_remainder = parse_key_value(item_text)
                if item_remainder is None:
                    if index < len(tokens) and tokens[index][0] > indent:
                        nested_value, index = parse_yaml_block(tokens, index, tokens[index][0])
                    else:
                        nested_value = {}
                    item_value = {item_key: nested_value}
                else:
                    item_value = {item_key: parse_scalar(item_remainder)}
                    if index < len(tokens) and tokens[index][0] > indent:
                        nested_value, index = parse_yaml_block(tokens, index, tokens[index][0])
                        if isinstance(nested_value, dict):
                            item_value.update(nested_value)
                        else:
                            item_value["_nested"] = nested_value
            else:
                item_value = parse_scalar(item_text)
                if index < len(tokens) and tokens[index][0] > indent:
                    nested_value, index = parse_yaml_block(tokens, index, tokens[index][0])
                    item_value = {"value": item_value, "_nested": nested_value}
            items.append(item_value)
        return items, index

    mapping: dict[str, Any] = {}
    while index < len(tokens) and tokens[index][0] == indent and not tokens[index][1].startswith("- "):
        key, remainder = parse_key_value(tokens[index][1])
        index += 1
        if remainder is None:
            if index < len(tokens) and (
                tokens[index][0] > indent
                or (tokens[index][0] == indent and tokens[index][1].startswith("- "))
            ):
                value, index = parse_yaml_block(tokens, index, tokens[index][0])
            else:
                value = {}
        else:
            value = parse_scalar(remainder)
            if index < len(tokens) and tokens[index][0] > indent:
                nested_value, index = parse_yaml_block(tokens, index, tokens[index][0])
                if isinstance(value, dict) and isinstance(nested_value, dict):
                    value = {**value, **nested_value}
                else:
                    value = {"value": value, "_nested": nested_value}
        mapping[key] = value
    return mapping, index


def parse_document_data(body: str) -> tuple[str, dict[str, Any]]:
    lines = body.splitlines()
    if not lines:
        return "Unknown", {}
    first = lines[0].strip()
    component_type = first[:-1] if first.endswith(":") else first
    tokens = tokenize_yaml_lines(lines[1:])
    if not tokens:
        return component_type, {}
    data, _ = parse_yaml_block(tokens, 0, tokens[0][0])
    return component_type, data if isinstance(data, dict) else {}


def parse_unity_documents(path: Path) -> list[UnityDocument]:
    text = path.read_text(encoding="utf-8", errors="ignore")
    matches = list(DOC_SPLIT_RE.finditer(text))
    documents: list[UnityDocument] = []
    for index, match in enumerate(matches):
        start = match.end()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        body = text[start:end].lstrip("\n")
        component_type, data = parse_document_data(body)
        documents.append(
            UnityDocument(
                class_id=int(match.group("class_id")),
                file_id=match.group("file_id"),
                component_type=component_type,
                data=data,
            )
        )
    return documents


def scan_script_catalog(script_roots: list[Path]) -> dict[str, ScriptInfo]:
    catalog: dict[str, ScriptInfo] = {}
    for root in script_roots:
        if not root.exists():
            continue
        for script_path in root.rglob("*.cs"):
            meta_path = script_path.with_suffix(script_path.suffix + ".meta")
            if not meta_path.exists():
                continue
            meta_text = meta_path.read_text(encoding="utf-8", errors="ignore")
            guid_match = GUID_RE.search(meta_text)
            if not guid_match:
                continue

            source_text = script_path.read_text(encoding="utf-8", errors="ignore")
            code_id_match = CODE_ID_RE.search(source_text)
            header_file_match = FILE_HEADER_RE.search(source_text)
            rel_path = script_path.relative_to(REPO_ROOT).as_posix()
            catalog[guid_match.group(1)] = ScriptInfo(
                guid=guid_match.group(1),
                path=rel_path,
                code_id=code_id_match.group(1) if code_id_match else None,
                header_file=header_file_match.group(1).strip() if header_file_match else None,
            )
    return catalog


def simplify_editor_class_identifier(value: Any) -> str | None:
    if not isinstance(value, str) or not value.strip():
        return None
    text = value.strip()
    if "::" in text:
        return text.split("::", 1)[1].strip()
    return text


def normalize_ref(value: Any) -> Any:
    if isinstance(value, dict):
        normalized = {}
        for key, inner in value.items():
            normalized[key] = normalize_ref(inner)
        return normalized
    if isinstance(value, list):
        return [normalize_ref(item) for item in value]
    return value


def extract_component_payload(
    document: UnityDocument,
    include_component_types: set[str],
    script_catalog: dict[str, ScriptInfo],
    mode: str,
) -> dict[str, Any] | None:
    component_type = document.component_type
    if component_type not in include_component_types:
        return None

    data = document.data
    payload: dict[str, Any] = {"component_type": component_type}

    if component_type == "MonoBehaviour":
        script_ref = normalize_ref(data.get("m_Script", {}))
        script_guid = script_ref.get("guid") if isinstance(script_ref, dict) else None
        script_info = script_catalog.get(script_guid) if isinstance(script_guid, str) else None
        script_class = simplify_editor_class_identifier(data.get("m_EditorClassIdentifier"))
        fields = {
            key: normalize_ref(value)
            for key, value in data.items()
            if not key.startswith("m_")
        }
        payload.update(
            {
                "script_guid": script_guid,
                "script_class": script_class,
                "script_path": script_info.path if script_info else None,
                "code_id": script_info.code_id if script_info else None,
                "header_file": script_info.header_file if script_info else None,
                "fields": fields,
            }
        )
        if mode == "full":
            payload["raw_fields"] = normalize_ref(data)
        return payload

    selected_fields: dict[str, Any]
    if component_type == "Transform":
        selected_fields = {
            "m_LocalPosition": normalize_ref(data.get("m_LocalPosition")),
            "m_LocalRotation": normalize_ref(data.get("m_LocalRotation")),
                "m_LocalScale": normalize_ref(data.get("m_LocalScale")),
                "m_LocalEulerAnglesHint": normalize_ref(data.get("m_LocalEulerAnglesHint")),
            }
    elif component_type == "RectTransform":
        selected_fields = {
            "m_LocalPosition": normalize_ref(data.get("m_LocalPosition")),
            "m_LocalRotation": normalize_ref(data.get("m_LocalRotation")),
            "m_LocalScale": normalize_ref(data.get("m_LocalScale")),
            "m_LocalEulerAnglesHint": normalize_ref(data.get("m_LocalEulerAnglesHint")),
            "m_AnchorMin": normalize_ref(data.get("m_AnchorMin")),
            "m_AnchorMax": normalize_ref(data.get("m_AnchorMax")),
            "m_AnchoredPosition": normalize_ref(data.get("m_AnchoredPosition")),
            "m_SizeDelta": normalize_ref(data.get("m_SizeDelta")),
            "m_Pivot": normalize_ref(data.get("m_Pivot")),
        }
    elif component_type == "Camera":
        selected_fields = {
            "orthographic": normalize_ref(data.get("orthographic")),
            "orthographic size": normalize_ref(data.get("orthographic size")),
            "near clip plane": normalize_ref(data.get("near clip plane")),
            "far clip plane": normalize_ref(data.get("far clip plane")),
            "m_BackGroundColor": normalize_ref(data.get("m_BackGroundColor")),
            "m_Depth": normalize_ref(data.get("m_Depth")),
        }
    elif component_type == "Canvas":
        selected_fields = {
            "m_RenderMode": normalize_ref(data.get("m_RenderMode")),
            "m_PixelPerfect": normalize_ref(data.get("m_PixelPerfect")),
            "m_ReceivesEvents": normalize_ref(data.get("m_ReceivesEvents")),
            "m_OverrideSorting": normalize_ref(data.get("m_OverrideSorting")),
            "m_SortingOrder": normalize_ref(data.get("m_SortingOrder")),
            "m_TargetDisplay": normalize_ref(data.get("m_TargetDisplay")),
        }
    elif component_type == "Grid":
        selected_fields = {
            key: normalize_ref(value)
            for key, value in data.items()
            if key in {"m_CellSize", "m_CellGap", "m_CellLayout", "m_CellSwizzle"}
        }
    elif component_type == "Toggle":
        selected_fields = {
            "m_IsOn": normalize_ref(data.get("m_IsOn")),
            "toggleTransition": normalize_ref(data.get("toggleTransition")),
            "m_Group": normalize_ref(data.get("m_Group")),
        }
    elif component_type == "Slider":
        selected_fields = {
            "m_MinValue": normalize_ref(data.get("m_MinValue")),
            "m_MaxValue": normalize_ref(data.get("m_MaxValue")),
            "m_Value": normalize_ref(data.get("m_Value")),
            "m_WholeNumbers": normalize_ref(data.get("m_WholeNumbers")),
            "m_Direction": normalize_ref(data.get("m_Direction")),
        }
    elif component_type == "Image":
        selected_fields = {
            "m_Color": normalize_ref(data.get("m_Color")),
            "m_Sprite": normalize_ref(data.get("m_Sprite")),
            "m_Type": normalize_ref(data.get("m_Type")),
            "m_FillMethod": normalize_ref(data.get("m_FillMethod")),
            "m_FillAmount": normalize_ref(data.get("m_FillAmount")),
        }
    elif component_type in {"Tilemap", "TilemapRenderer", "SpriteRenderer", "Animator"}:
        selected_fields = {
            key: normalize_ref(value)
            for key, value in data.items()
            if key
            in {
                "m_SortingOrder",
                "m_Sprite",
                "m_Color",
                "m_FlipX",
                "m_FlipY",
                "m_Controller",
                "m_Enabled",
                "m_CullingMode",
                "m_TileAnchor",
                "m_Size",
            }
        }
    else:
        selected_fields = {
            key: normalize_ref(value)
            for key, value in data.items()
            if not key.startswith("m_")
        }

    payload["fields"] = selected_fields
    if mode == "full":
        payload["raw_fields"] = normalize_ref(data)
    return payload


def build_target_export(
    asset_path: str,
    game_objects: list[str] | None,
    include_component_types: set[str],
    script_catalog: dict[str, ScriptInfo],
    mode: str,
) -> dict[str, Any]:
    asset = REPO_ROOT / asset_path
    if not asset.exists():
        raise FileNotFoundError(f"Runtime config target does not exist: {asset_path}")
    documents = parse_unity_documents(asset)
    by_file_id = {document.file_id: document for document in documents}

    game_object_docs = [document for document in documents if document.component_type == "GameObject"]
    name_to_doc = {
        str(document.data.get("m_Name")): document
        for document in game_object_docs
        if document.data.get("m_Name") is not None
    }

    requested_names = game_objects or list(name_to_doc.keys())
    entries: list[dict[str, Any]] = []
    missing: list[str] = []

    for name in requested_names:
        game_object = name_to_doc.get(name)
        if not game_object:
            missing.append(name)
            continue

        component_refs = game_object.data.get("m_Component", [])
        component_ids: list[str] = []
        for item in component_refs if isinstance(component_refs, list) else []:
            if isinstance(item, dict):
                component_ref = item.get("component")
                if isinstance(component_ref, dict) and "fileID" in component_ref:
                    component_ids.append(str(component_ref["fileID"]))

        components: list[dict[str, Any]] = []
        for component_id in component_ids:
            document = by_file_id.get(component_id)
            if not document:
                continue
            payload = extract_component_payload(document, include_component_types, script_catalog, mode)
            if payload is not None:
                payload["file_id"] = component_id
                components.append(payload)

        entries.append(
            {
                "name": name,
                "file_id": game_object.file_id,
                "components": components,
            }
        )

    return {
        "path": asset_path,
        "mode": mode,
        "requested_game_objects": requested_names,
        "missing_game_objects": missing,
        "game_objects": entries,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Export tracked Unity scene/prefab inspector configuration into JSON."
    )
    parser.add_argument(
        "--repo-root",
        default=str(DEFAULT_REPO_ROOT),
        help="Repository root to export from. Defaults to the current project root.",
    )
    parser.add_argument(
        "--manifest",
        default=None,
        help="Optional runtime-config manifest override. Defaults to <repo-root>/scripts/runtime_config_manifest.json.",
    )
    parser.add_argument(
        "--output",
        default=None,
        help="Optional output override. Defaults to the manifest's output path.",
    )
    mode_group = parser.add_mutually_exclusive_group()
    mode_group.add_argument(
        "--strict",
        action="store_true",
        help="Use curated component field export (default).",
    )
    mode_group.add_argument(
        "--full",
        action="store_true",
        help="Export curated fields plus raw serialized component data for included component types.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    configure_repo_root(Path(args.repo_root))
    mode = "full" if args.full else "strict"

    manifest_path = REPO_ROOT / "scripts" / "runtime_config_manifest.json" if args.manifest is None else Path(args.manifest)
    if not manifest_path.is_absolute():
        manifest_path = (REPO_ROOT / manifest_path).resolve()
    if not manifest_path.exists():
        raise FileNotFoundError(f"Manifest not found: {manifest_path}")

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    output_value = manifest.get("output")
    if not isinstance(output_value, str) or not output_value.strip():
        raise ValueError("Manifest must define a non-empty 'output' path")
    script_roots_value = manifest.get("script_roots")
    if not isinstance(script_roots_value, list) or not script_roots_value:
        raise ValueError("Manifest must define a non-empty 'script_roots' list")
    for category in ("scene_targets", "prefab_targets"):
        targets = manifest.get(category)
        if not isinstance(targets, list):
            raise ValueError(f"Manifest field '{category}' must be a list")
        for target in targets:
            if not isinstance(target, dict):
                raise ValueError(f"Manifest field '{category}' contains a non-object target entry")
            path_value = target.get("path")
            if not isinstance(path_value, str) or not path_value.strip():
                raise ValueError(f"Manifest field '{category}' contains a target without a valid 'path'")

    script_roots = [REPO_ROOT / path for path in manifest.get("script_roots", [])]
    script_catalog = scan_script_catalog(script_roots)

    scene_exports: list[dict[str, Any]] = []
    for target in manifest.get("scene_targets", []):
        scene_exports.append(
            build_target_export(
                asset_path=target["path"],
                game_objects=target.get("game_objects"),
                include_component_types=set(target.get("include_component_types", ["MonoBehaviour", "Transform"])),
                script_catalog=script_catalog,
                mode=mode,
            )
        )

    prefab_exports: list[dict[str, Any]] = []
    for target in manifest.get("prefab_targets", []):
        prefab_exports.append(
            build_target_export(
                asset_path=target["path"],
                game_objects=target.get("game_objects"),
                include_component_types=set(target.get("include_component_types", ["MonoBehaviour", "Transform"])),
                script_catalog=script_catalog,
                mode=mode,
            )
        )

    export_data = {
        "version": 1,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "manifest": manifest_path.relative_to(REPO_ROOT).as_posix(),
        "mode": mode,
        "scene_targets": scene_exports,
        "prefab_targets": prefab_exports,
    }

    output_override = args.output
    output_path = REPO_ROOT / output_value if output_override is None else Path(output_override)
    if not output_path.is_absolute():
        output_path = (REPO_ROOT / output_path).resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(export_data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Wrote {output_path.relative_to(REPO_ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
