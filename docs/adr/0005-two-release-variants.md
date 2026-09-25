# 0005: Ship both self-contained and framework-dependent builds

- **Status:** Accepted
- **Date:** 2026-09-24

## Context

Two competing concerns:

- **User convenience**: the app should "just work" when downloaded, without
  requiring users to install anything else.
- **Download size**: the self-contained build is ~200 MB because it bundles the
  entire .NET 10 runtime.

A 200 MB download is a barrier for users on slow connections or metered plans,
even though it minimizes setup friction.

## Decision

Ship **two variants** with every release:

| Variant | Size | Requires |
|---|---|---|
| Self-Contained | ~200 MB | Nothing — the runtime is bundled |
| Framework-Dependent | ~25 MB | .NET 10 Desktop Runtime must be installed |

The README recommends Self-Contained for most users and explains that
Framework-Dependent is 87% smaller for users who already have the runtime.

The CI workflow publishes both, and the GitHub Release attaches both ZIPs.

## Consequences

**Easier:**
- Users who already have .NET 10 can save 175 MB of download.
- Users who don't have .NET 10 or don't know — take the safe self-contained path.
- It's a familiar choice: many .NET desktop apps ship these two options.

**Harder:**
- Two sets of artifacts to test before each release.
- Documentation must explain the difference without confusing users.

**Given up:**
- A single canonical download. Some users will be briefly unsure which to pick.
- Trimming the self-contained build was considered but rejected — WPF relies on
  reflection and is not safely trimmable.

## Retrospective Note

This ADR was written retroactively on 2026-09-25 to document a decision made
earlier in the project. It reflects the reasoning as it existed at the time.