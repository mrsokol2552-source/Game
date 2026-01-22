# Agent Comment Standard

This project uses a lightweight file-header convention on the hottest or most failure-prone source files.

The goal is not to document every line. The goal is to make fast routing possible:

- what this file owns
- where execution enters
- what it depends on
- what data flows through it
- what can break if it is changed carelessly

## File header format

Use this block near the top of a source file, before `using` directives when possible:

```text
/*
@file: <relative/path>
@module: <logical subsystem>
@purpose: <1-2 sentences>
@entry: <main entrypoints or section IDs>
@api: <public/internal/scene MonoBehaviour/etc.>
@deps: <key dependencies>
@data: <main data/state owned or transformed here>
@perf: <hotpath notes>
@thread: <main thread / jobs / mixed>
@tests: <relevant tests or manual verification>
@config: <important inspector/config entrypoints>
@assets: <important asset groups, if any>
@notes: <invariants, gotchas, common failure modes>
*/
```

## Rules

- Keep headers short and practical.
- Prefer stable subsystem names over temporary feature wording.
- Mention section IDs (`PENV-05`, `UCOM-03`, etc.) in `@entry` when a file is large.
- Do not duplicate obvious code.
- Put invariants and failure modes in `@notes`, not in random inline comments.
- Add headers first to hot-path, large, or failure-prone files.

## When to add one

Add or update a header when:

- the file is a routing entrypoint for future work
- the file coordinates multiple systems
- the file is large enough that opening it cold is expensive
- the file owns inspector switches that frequently affect behavior
- the file sits on a performance-critical path

## Relationship to CODE-ID comments

The file header answers:

- what is this file for?
- why should I open it?

The `CODE-ID` and section comments answer:

- where exactly inside the file should I jump?

Use both together.
