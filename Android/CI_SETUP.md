# Android CI - signing setup

The android.yml workflow builds the APK on every push and attaches a signed
release APK to GitHub Releases when a v* tag is pushed. Release signing needs
four GitHub Secrets.

## 1. Create the release keystore (once, keep forever)

Run this from a terminal (not inside the repo):

    keytool -genkeypair -v -keystore bg-release.jks -alias batteryguardian -keyalg RSA -keysize 2048 -validity 10000

Pick a strong store password and key password. Save both in a password manager.

Android identifies your app by this key. Every future update must be signed with
the same keystore, or devices will refuse to install over the old version.

## 2. Add the secrets to GitHub

Repository Settings, Secrets and variables, Actions, New repository secret.

| Secret name | Value |
|---|---|
| BG_KEYSTORE_BASE64 | base64 of bg-release.jks |
| BG_STORE_PASSWORD | your keystore store password |
| BG_KEY_ALIAS | batteryguardian |
| BG_KEY_PASSWORD | your key password |

## 3. Produce the base64 value

Linux / macOS:

    base64 -w 0 bg-release.jks

Windows PowerShell:

    [Convert]::ToBase64String([IO.File]::ReadAllBytes("bg-release.jks"))

Copy the whole output as the BG_KEYSTORE_BASE64 secret value.

## 4. Release flow

1. Tag a commit: git tag v1.0.1 and push the tag.
2. CI runs tests, builds the debug and release APKs.
3. A GitHub Release is created with the signed APK attached as
   BatteryGuardian-android-v1.0.1.apk.
4. The in-app updater sees the new tag, finds the apk asset, downloads it, and
   prompts the user to install.

## Versioning

The Android version is derived from the git tag in app/build.gradle.kts:

- versionName = tag without the leading v (v1.12.1 becomes 1.12.1)
- versionCode = major*10000 + minor*100 + patch (1.12.1 becomes 11201)

This keeps the Android build in lockstep with the Windows build, which uses
MinVer with the same v prefix.

## Without the secrets

If the keystore secrets are absent, the release build still runs but produces an
unsigned APK. The workflow only decodes the keystore and signs on tag builds, so
normal branch and PR builds are unaffected.
