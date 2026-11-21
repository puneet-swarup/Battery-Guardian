# Releasing

Two apps ship from this repository on one tag. This is the whole process.

## The version comes from the tag

Nothing in the code holds a version number.

| platform | how the version is decided |
|---|---|
| Windows | [MinVer](https://github.com/adamralph/minver) reads the git tag (`v` prefix) |
| Android | `app/build.gradle.kts` derives `versionName` from the tag, and `versionCode = major*10000 + minor*100 + patch` |

So `v2.4.0` becomes `2.4.0` on both, and Android version code `20400`. Both
workflows need full history and tags, which is why they check out with
`fetch-depth: 0`.

## A fix that is not tagged reaches nobody

The updater compares the running version against the latest release **tag**. So
a change on `main` with no tag is invisible to every installed copy, however
correct it is. Functional work is not finished when it is pushed; it is finished
when it is tagged.

The same rule covers the changelog: the release body is taken from the section
for that tag, and the release job now **fails** when the section is missing. A
tag without an entry produces a red build, not a release page saying
`Release 2.4.1`.

## Which version

| change | bump | example |
|---|---|---|
| a bug fix, no new surface | patch | `2.4.0` to `2.4.1` |
| a new feature or setting | minor | `2.4.1` to `2.5.0` |
| a change that breaks saved settings, the file format, or a documented behaviour | major | `2.5.0` to `3.0.0` |

When in doubt: if a user could notice a difference they did not ask for, it is
at least a minor.

## Steps

1. **Write the changelog entry.** Move what is under `## [Unreleased]` in
   `CHANGELOG.md` into a dated section:

   ```markdown
   ## [2.4.0] - 2026-11-30
   ```

   This is not decoration: the release body is taken from this section, so a
   missing entry means an empty release page.

2. **Commit and push** to `main`. CI runs both suites.

3. **Tag and push the tag:**

   ```cmd
   git tag v2.4.0
   git push origin v2.4.0
   ```

4. **Watch three workflows** (Actions tab). They run in parallel:

   | workflow | produces |
   |---|---|
   | Build and Publish EXE | `BatteryGuardian-x64-SelfContained.zip`, `BatteryGuardian-x64-FrameworkDependent.zip` |
   | Build and Publish Android APK | `BatteryGuardian-android-v2.4.0.apk`, signed with the release key |
   | Build and Sign Android Release | `BatteryGuardian-android-v2.4.0.aab`, signed, attached to the release |

5. **Check the release page.** It should have four assets — two Windows zips,
   the APK and the AAB — and a body taken from the changelog section for that tag.

## Secrets

| secret | used by | what it is |
|---|---|---|
| `BG_KEYSTORE_BASE64` | android.yml, android-release.yml | the release keystore, base64 encoded |
| `BG_STORE_PASSWORD` | both | keystore password |
| `BG_KEY_ALIAS` | both | key alias inside the keystore |
| `BG_KEY_PASSWORD` | both | key password |

`Android/CI_SETUP.md` walks through creating the keystore and producing the
base64 value. Without these four, a **tag** build fails at the signing step;
branch and pull-request builds never touch them.

Windows code signing is separate and not part of CI — see
[CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md).

## Notes on the current setup

- **The AAB is attached to the release** as well as uploaded as a workflow
  artifact. It is the Play upload format, and it is the only signed artifact
  someone without a keystore can hand to a store.
- **Path filters skip irrelevant runs.** A change under `Windows/` does not run
  the Android workflow and vice versa. Path filters are not evaluated for tag
  pushes, so a release always builds both.
- **The release body comes from `CHANGELOG.md`.** GitHub's generated notes only
  list merged pull requests; this project pushes straight to `main`, so they came
  out as a bare compare link. If the tag has no changelog section, the body falls
  back to `Release x.y.z` rather than failing the job.

## When something goes wrong

| symptom | cause |
|---|---|
| no workflows started | the tag does not match `v*` |
| release body is empty or `Release x.y.z` | no `## [x.y.z]` section in `CHANGELOG.md` |
| Android job failed at "Build release APK" | keystore secrets missing or wrong — the keystore decodes to an empty file |
| Windows version is `0.0.0-alpha.0` | the checkout had no tags; `fetch-depth: 0` is required |
| release has no APK | the Android job failed before "Upload release APK" |
| release has no AAB | "Build and Sign Android Release" failed, usually at the keystore decode |
