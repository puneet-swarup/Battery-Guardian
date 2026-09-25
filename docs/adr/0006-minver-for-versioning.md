# 0006: Derive version from Git tags via MinVer

- **Status:** Accepted
- **Date:** 2026-09-24

## Context

The app needs a version number that is:
- Baked into the executable at build time (visible in `About` dialog).
- Consistent with the Git tag that produced the release.
- Available for the update-checker to compare against GitHub Releases.

Manually maintaining `<Version>` in the `.csproj` and tagging Git separately
meant two sources of truth. Forgetting to bump one produced confusing output
(e.g., a `v1.9.0` release with an internal `1.8.0` version).

## Decision

Adopt **MinVer**, a small NuGet package that reads Git tags at build time and
sets the assembly version automatically.

- On a tagged commit (e.g., `git tag v1.9.0`), the assembly is versioned `1.9.0`.
- On a commit after a tag, the assembly gets a SemVer pre-release version
  (e.g., `1.9.1-alpha.0.5`), which is safe to use locally and clearly not a
  release.
- The `<Version>` property is removed from the `.csproj`.

The CI workflow adds `fetch-depth: 0` to the checkout step so MinVer has access
to the full tag history.

## Consequences

**Easier:**
- One source of truth: the Git tag.
- Local builds have unique, meaningful versions.
- The version-check logic in the app compares cleanly against GitHub releases.

**Harder:**
- Local builds show a pre-release version string, which can be confusing at
  first (`1.9.1-alpha.0.5` instead of a round `1.9.1`).
- Developers unfamiliar with MinVer may wonder where the version is set.

**Given up:**
- The ability to specify the version manually in the project file.
- CI builds without full git history will fall back to `0.0.0`, so the
  `fetch-depth: 0` setting is critical.

## Retrospective Note

This ADR was written retroactively on 2026-09-25 to document a decision made
earlier in the project. It reflects the reasoning as it existed at the time.