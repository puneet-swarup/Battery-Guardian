# commit-backlog.ps1
#
# Turns the uncommitted backlog into three commits: Windows, Android, then
# CI and docs. Run from the repository root:
#
#     powershell -ExecutionPolicy Bypass -File .\commit-backlog.ps1
#
# Messages are written to a temp file and passed with `git commit -F`, so no
# quoting rule can mangle them. Stops at the first failure.

$ErrorActionPreference = 'Stop'

function Commit-Group {
    param(
        [string]   $Subject,
        [string]   $Body,
        [string[]] $PathSpecs
    )

    if ($PathSpecs) {
        git add -A -- @PathSpecs
    } else {
        git add -A
    }
    if ($LASTEXITCODE -ne 0) { throw "git add failed for: $Subject" }

    $staged = git diff --cached --name-only
    if (-not $staged) {
        Write-Host "  skip (nothing staged): $Subject"
        return
    }

    $file = Join-Path $env:TEMP ("commit-" + [guid]::NewGuid().ToString("N") + ".txt")
    [System.IO.File]::WriteAllText($file, $Subject + "`n`n" + $Body + "`n",
        (New-Object System.Text.UTF8Encoding($false)))

    git commit -F $file
    $code = $LASTEXITCODE
    Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue
    if ($code -ne 0) { throw "git commit failed for: $Subject" }

    Write-Host "  committed: $Subject"
    Write-Host ""
}

if (-not (Test-Path '.git')) {
    Write-Host "Run this from the repository root." -ForegroundColor Red
    exit 1
}

# ---------------------------------------------------------------- 1. Windows
$windowsBody = @'
The Windows sources sat at the repository root, so every new stack had to work
around them. They now live in Windows/ beside Android/, and the root holds only
the shared documents, docs/ and .github/.

Alongside the move:

- Smart-plug control uses the Tuya LAN SDK instead of a private copy of the
  protocol. That copy was built from a description that does not match 3.4/3.5
  firmware - a frame with no reserved bytes, IV, tag or footer, the 3.4
  session-key derivation on a 3.5 device, control on 0x07 with a bare dps body -
  so it could never have switched a physical plug. It is verified against real
  hardware in the SDK repository instead.
- Battery history no longer rewrites the whole file on every reading. Appends
  inside a two-second window are coalesced and Flush() on shutdown writes
  anything pending. Two unit tests went from four minutes to milliseconds.
- A plug timeout is classified as a timeout rather than as unreachable.
- TuyaLocalKey rejects anything that is not a 16-byte AES key. A 32-character
  ASCII key used to be accepted silently and could never have worked.
'@

Commit-Group -Subject 'refactor(windows): move under Windows/ and use the Tuya LAN SDK' `
             -Body $windowsBody `
             -PathSpecs @('.', ':!Android', ':!Android/**', ':!.github', ':!.github/**',
                          ':!docs', ':!docs/**', ':!README.md', ':!CHANGELOG.md',
                          ':!CONTRIBUTING.md', ':!RELEASING.md')

# ---------------------------------------------------------------- 2. Android
$androidBody = @'
Same move as Windows. TuyaSmartPlug.kt is an adapter over
com.tuyalan.devices.TuyaLocalDevice, vendored as app/libs/tuya-lan-2.0.0.aar
because the SDK is not on Maven yet; swapping that line for a coordinate is the
only change needed once it is published.

- Removed TuyaCrypto, TuyaMessageCodec, TuyaMessageCipher and TuyaSession, plus
  the two test files that asserted the old wire format.
- SmartPlug and RelayState moved out of the implementation file.
- The adapter honours the protocol version already stored in Settings, and
  passes the device id the SDK needs.
- proguard-rules.pro carries -dontwarn entries for the SDK's unused cloud
  client, so release builds stay clean without shipping an HTTP stack.
- org.ow2.asm:asm:9.9.1 is pinned on the test classpath: Robolectric 4.14.1
  cannot read class files from JDKs newer than 21 and failed a third of the
  suite with ClassReader errors in tests that had nothing to do with the change.
'@

Commit-Group -Subject 'refactor(android): use the Tuya LAN SDK' `
             -Body $androidBody `
             -PathSpecs @('Android')

# ------------------------------------------------------------ 3. CI and docs
$ciBody = @'
- Release bodies were a bare compare link: GitHub's generated notes list merged
  pull requests, and this project pushes straight to main. The body now comes
  from the matching CHANGELOG.md section.
- The signed AAB is attached to the release as well as uploaded as an artifact,
  so it can be handed to a store without the keystore.
- CI runs the committed Gradle wrapper instead of installing its own Gradle,
  and each workflow only runs for its own folder. Tags ignore path filters, so
  a release still builds both platforms.
- RELEASING.md documents the version derivation, the four steps, the signing
  secrets and what lands where.
- CHANGELOG gains the missing 2.3.0 entry, and CONTRIBUTING is rewritten around
  the new layout and both test suites.
'@

Commit-Group -Subject 'ci: release notes from the changelog, and attach the AAB' `
             -Body $ciBody

# ------------------------------------------------------------------- summary
Write-Host ""
Write-Host "Recent commits:"
git log --oneline -4

Write-Host ""
$left = git status --short
if ($left) {
    Write-Host "Still uncommitted:" -ForegroundColor Yellow
    $left
} else {
    Write-Host "Working tree is clean. Push with:  git push" -ForegroundColor Green
}
