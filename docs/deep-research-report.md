# Deep Research Report: Repository Commenting and Navigation for AI Agents

This document is a distilled English operational reference based on the original research notes.

Its purpose is not to archive every citation or every theoretical nuance. Its purpose is to help an AI coding agent move through a large game repository quickly, with predictable entry points, low context waste, and stable navigation rules.

Use this document together with:

- [code_map.md](./code_map.md)
- [code_id_index.md](./code_id_index.md)
- [debug_playbooks.md](./debug_playbooks.md)
- [runtime_switches.md](./runtime_switches.md)
- [unity_mcp_tools.md](./unity_mcp_tools.md)
- [repo_map.md](../maps/repo_map.md)

## Executive Summary

The highest-value approach for an AI agent working in a large game repository is a layered navigation system:

1. Structured metadata inside the code.
2. Compact repository maps outside the code.
3. Symptom-first debugging routes.
4. Runtime switch registries.
5. Machine-readable indexes for tools and automation.

The main idea is simple: the agent should almost never start from raw file guessing. It should start from a map, a route, a section ID, or a symptom-to-code playbook.

For game projects, especially Unity and Unreal, this matters more than in smaller application codebases because:

- there are many runtime systems with overlapping responsibilities;
- behavior is split between code, scenes, prefabs, assets, and inspector values;
- context windows are too small to hold large files and scene wiring at once;
- performance debugging is often symptom-driven, not API-driven.

## The Core Navigation Model

An effective AI-friendly repository uses three context layers.

### 1. Routing context

This is the top-level answer to:

- where does this behavior live;
- which module owns it;
- what should be opened first;
- which scene or asset is involved.

In this project, the routing layer is provided by:

- [repo_map.md](../maps/repo_map.md)
- [code_map.md](./code_map.md)
- [code_id_index.md](./code_id_index.md)

### 2. Symbol and file context

This is the answer to:

- which exact file contains the logic;
- which block in the file matters;
- which entrypoints, hot paths, or invariants are relevant.

In this project, the symbol/file layer is provided by:

- file headers in hot files;
- `CODE-ID` tags;
- internal section IDs such as `PENV-*`, `UCOM-*`, `FFLD-*`, `ORCA-*`.

### 3. Task-specific context

This is the answer to:

- what to inspect for a bug;
- which runtime switches change behavior;
- which tools to use first.

In this project, the task-specific layer is provided by:

- [debug_playbooks.md](./debug_playbooks.md)
- [runtime_switches.md](./runtime_switches.md)
- [unity_mcp_tools.md](./unity_mcp_tools.md)

## Why This Works Better Than Raw Documentation Dumps

Large, prose-heavy documentation is weak as a first entry layer for an agent.

The agent needs:

- stable anchors;
- predictable routes;
- short summaries;
- direct links to code;
- a way to zoom in gradually.

The agent does not need:

- a full essay before it can open a file;
- duplicated explanations of the same subsystem in five places;
- long narrative architecture descriptions without direct code entrypoints.

This is why the repository should be "hierarchically compressible":

- a one-line system name;
- a two-line purpose;
- a direct file path;
- optional deep details only when needed.

## Practical Repository Structure

The ideal layout respects engine conventions and adds agent-friendly layers without fighting the engine.

### Universal top-level layout

Recommended roots:

- `docs/` for human-readable navigation, architecture, and operational references;
- `maps/` for compact repo-level maps and machine-readable indexes;
- `tools/` for generators, migration helpers, and automation scripts;
- `tests/` or engine-native test roots;
- `vendor/` or `third_party/` if external code is stored in the repo;
- build output directories excluded from VCS.

### Unity-specific structure

Unity projects should preserve the engine roots:

- `Assets/`
- `Packages/`
- `ProjectSettings/`

Inside `Assets/`, AI navigation becomes much easier when code is grouped by responsibility and compilation boundaries are explicit.

Recommended pattern:

- gameplay code under feature or layer folders;
- clear separation between runtime and editor code;
- assembly definition boundaries where practical;
- avoid random utility dumping grounds;
- avoid spaces in important code/tool paths whenever possible.

For this repository, the effective navigation roots are:

- [Assets/Scripts/Domain](../My%20project/Assets/Scripts/Domain)
- [Assets/Scripts/Application](../My%20project/Assets/Scripts/Application)
- [Assets/Scripts/Infrastructure](../My%20project/Assets/Scripts/Infrastructure)
- [Assets/Scripts/Presentation](../My%20project/Assets/Scripts/Presentation)
- [Assets/Editor](../My%20project/Assets/Editor)
- [Assets/Scenes](../My%20project/Assets/Scenes)
- [Assets/SmallScaleInt](../My%20project/Assets/SmallScaleInt)

### Mixed-stack projects

If the repository later grows non-Unity tooling:

- Python tools should prefer a `src/` layout;
- Node tools should expose entry scripts clearly in `package.json`;
- generated artifacts should never become the source of truth.

The agent should be able to tell at a glance:

- runtime code;
- editor code;
- tests;
- generated content;
- third-party content.

## Commenting Strategy for AI Navigation

The most effective comment strategy is not "more comments". It is "better metadata at predictable locations".

### High-value comment targets

Always document:

- public APIs;
- entrypoints;
- hot paths;
- systems with unusual invariants;
- systems with performance constraints;
- systems that bridge multiple coordinate spaces or data models;
- systems that mix code and inspector state.

### Low-value comment targets

Avoid commenting:

- obvious assignments;
- trivial getters and setters;
- code that already explains itself through names and structure;
- inline noise that repeats the code.

### Recommended metadata fields

For hot files, use a short structured header that answers:

- what this file is;
- why it exists;
- where it is entered from;
- what it depends on;
- what can break if it is changed;
- whether it is a hot path;
- whether it is inspector-driven;
- which scene systems rely on it.

This project already uses this idea in hot files and combines it with `CODE-ID` comments.

## Map Files and Navigation Artifacts

An AI-friendly repository should maintain both human-readable and machine-readable maps.

### Human-readable maps

These should be short, curated, and task-oriented.

In this project:

- [repo_map.md](../maps/repo_map.md)
- [code_map.md](./code_map.md)
- [code_id_index.md](./code_id_index.md)
- [debug_playbooks.md](./debug_playbooks.md)
- [runtime_switches.md](./runtime_switches.md)

### Machine-readable maps

These should be stable inputs for tools, scripts, and automated checks.

In this project:

- [repo_map.json](../maps/repo_map.json)
- [code_index.json](./code_index.json)

### What machine-readable maps should contain

At minimum:

- file path;
- module/layer;
- short purpose;
- key entrypoints;
- hot-path flag;
- related runtime switches;
- related debugging playbooks;
- related scenes/assets when relevant.

Optional but useful:

- caller hints;
- dependency edges;
- tags like `perf`, `io`, `thread`, `editor`, `scene`, `asset`;
- ownership or responsibility labels.

## Recommended Navigation Workflow for an AI Agent

### When the task starts from a symptom

Use:

1. [debug_playbooks.md](./debug_playbooks.md)
2. [code_id_index.md](./code_id_index.md)
3. target file and section IDs
4. [runtime_switches.md](./runtime_switches.md) if the behavior may be inspector-driven

### When the task starts from a requested feature or refactor

Use:

1. [repo_map.md](../maps/repo_map.md)
2. [code_map.md](./code_map.md)
3. [code_id_index.md](./code_id_index.md)
4. hot files and scene objects

### When the task starts from live Unity editor state

Use:

1. [unity_mcp_tools.md](./unity_mcp_tools.md)
2. scene hierarchy and logs
3. [runtime_switches.md](./runtime_switches.md)
4. code entrypoints

## Naming and Stability Rules

Predictability matters more than cleverness.

Recommended rules:

- prefer ASCII names for docs, tools, and critical automation files;
- avoid spaces in tool-sensitive paths when practical;
- keep identifiers stable even if documentation wording changes;
- prefer additive metadata over renaming hot symbols casually;
- use one naming scheme per layer, not many.

For agent-facing artifacts, the most valuable property is not beauty. It is consistency.

## Practical Rules for Large Files

Large files are unavoidable in game projects, but they must remain navigable.

When a file becomes context-heavy:

1. Add a structured file header.
2. Add section markers with stable IDs.
3. Add a section table in a nearby index document.
4. Separate unrelated responsibilities as soon as practical.

### Good examples for this repository

- [ProceduralEnvironment.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/ProceduralEnvironment.cs)
  - split mentally through `PENV-*`
- [UnitCombat.cs](../My%20project/Assets/Scripts/Presentation/View/UnitCombat.cs)
  - split mentally through `UCOM-*`
- [FlowFieldManager.cs](../My%20project/Assets/Scripts/Presentation/Pathfinding/FlowFieldManager.cs)
  - split mentally through `FFLD-*`

## Performance of the Navigation System Itself

The repository navigation layer can also become too heavy.

Common failure modes:

- one giant architecture doc that nobody can use quickly;
- machine indexes that are never regenerated;
- manual docs that drift away from the code;
- too many overlapping maps with unclear purpose;
- maps without direct links to code.

This repository should keep the navigation stack simple:

- `repo_map` for top-level routing;
- `code_map` for architecture;
- `code_id_index` for direct jump tables;
- `debug_playbooks` for bug-first work;
- `runtime_switches` for inspector-driven behavior;
- [code_index.json](./code_index.json) for machine-readable lookup.

Anything beyond that should justify its existence.

## Automation Recommendations

The navigation system should be cheap to maintain.

Recommended automation goals:

1. Extract file-level `CODE-ID` markers automatically.
2. Verify that index files do not point to missing files.
3. Validate JSON map files.
4. Detect stale navigation references in CI.
5. Keep generated maps separate from handwritten operational docs.

Recommended future tooling:

- a script that regenerates [code_index.json](./code_index.json) from headers and section IDs;
- a script that checks for broken relative Markdown links;
- a script that reports undocumented hot files;
- a script that reports duplicate or inconsistent `CODE-ID` usage.

## Documentation Density Rules

For AI navigation, density is better than volume.

Use this default:

- one-line summary for low-priority files;
- short structured header for hot files;
- section IDs for large files;
- one central routing doc per concern;
- no duplicate essays across many docs.

If a document starts becoming a raw archive of theory, convert it into:

- summary;
- checklist;
- routes;
- references.

That is the model used for this file.

## Security and Safety Notes

Navigation tooling must not accidentally become a leak vector.

Practical rules:

- do not index secret-bearing paths into machine maps;
- respect `.gitignore` and explicit deny-lists;
- do not extract raw sensitive config values into summaries;
- treat generated maps as derived artifacts, not safe public docs by default.

This matters even in game projects because editor scripts, service configs, and third-party integrations can quietly accumulate credentials over time.

## Rollout Strategy for Existing Projects

For a messy existing project, the best rollout order is:

1. Create a compact repo map.
2. Create a code map.
3. Mark hot files with file headers.
4. Add section IDs to the largest systems.
5. Create symptom-first playbooks.
6. Create a runtime switch registry.
7. Add machine-readable indexes.
8. Automate validation later.

Do not try to "fully document everything" first. That creates documentation debt immediately.

## What This Means for This Repository

The most useful next principles for this project are:

- keep using direct Markdown links in active docs;
- prefer ASCII doc filenames;
- keep `CODE-ID` and section IDs stable;
- route all new bug work through [debug_playbooks.md](./debug_playbooks.md);
- route all tuning work through [runtime_switches.md](./runtime_switches.md);
- route all broad feature work through [repo_map.md](../maps/repo_map.md) and [code_map.md](./code_map.md);
- avoid adding new large docs unless they fill a clearly separate role.

## Final Rule

The navigation system exists to reduce thought latency.

If a document, comment format, or map file does not help the agent answer these questions faster:

- where should I look;
- what should I open next;
- what can change behavior here;
- what is risky to touch;

then it is probably noise and should be removed, merged, or simplified.
