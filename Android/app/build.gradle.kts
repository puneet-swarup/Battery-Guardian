import org.jetbrains.kotlin.gradle.dsl.JvmTarget
import java.util.Properties

plugins {
    // AGP 9.x provides built-in Kotlin support - do NOT apply org.jetbrains.kotlin.android.
    id("com.android.application")
}

/**
 * Derives the app version from the git tag so the Android build stays in sync
 * with the Windows release (which uses MinVer with the same "v" prefix).
 *
 * Resolution order:
 *   1. The exact tag on HEAD (e.g. v1.2.3 on a tagged commit).
 *   2. The nearest reachable tag (e.g. v1.2.3-4-gabc1234 -> 1.2.3).
 *   3. Fallback "1.0.0" when git is unavailable (e.g. a source zip).
 *
 * versionCode is derived as major*10000 + minor*100 + patch, which is
 * monotonically increasing as long as the semantic version increases.
 */
data class AppVersion(val name: String, val code: Int)

fun resolveAppVersion(): AppVersion {
    val fallback = AppVersion("1.0.0", 1)
    return try {
        val process = ProcessBuilder("git", "describe", "--tags", "--always", "--dirty")
            .directory(rootProject.projectDir)
            .redirectErrorStream(true)
            .start()
        val output = process.inputStream.bufferedReader().readText().trim()
        process.waitFor()
        if (output.isEmpty()) return fallback

        // Strip a leading "v" and any -N-gHASH or -dirty suffix.
        val cleaned = output.removePrefix("v").substringBefore("-")
        val parts = cleaned.split(".")
        if (parts.isEmpty()) return fallback

        val major = parts.getOrNull(0)?.toIntOrNull() ?: return fallback
        val minor = parts.getOrNull(1)?.toIntOrNull() ?: 0
        val patch = parts.getOrNull(2)?.toIntOrNull() ?: 0

        val name = "$major.$minor.$patch"
        val code = major * 10000 + minor * 100 + patch
        AppVersion(name, code)
    } catch (t: Throwable) {
        fallback
    }
}

val appVersion = resolveAppVersion()
val releaseStoreFile = System.getenv("BG_STORE_FILE")
val hasReleaseSigning = !releaseStoreFile.isNullOrBlank()

android {
    namespace = "com.puneet.batteryguardian"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.puneet.batteryguardian"
        minSdk = 28          // Android 9 (Pie)
        targetSdk = 36       // AGP 9 defaults targetSdk to compileSdk; declared explicitly
        versionCode = appVersion.code
        versionName = appVersion.name

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        vectorDrawables { useSupportLibrary = true }
    }

    signingConfigs {
        if (hasReleaseSigning) {
            create("release") {
                storeFile = file(releaseStoreFile!!)   // use an ABSOLUTE path in CI
                storePassword = System.getenv("BG_STORE_PASSWORD")
                keyAlias      = System.getenv("BG_KEY_ALIAS")
                keyPassword   = System.getenv("BG_KEY_PASSWORD")

                require(storePassword != null) { "BG_STORE_PASSWORD not set" }
                require(keyAlias != null)      { "BG_KEY_ALIAS not set" }
                require(keyPassword != null)   { "BG_KEY_PASSWORD not set" }
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled   = true
            isShrinkResources = true
            isDebuggable      = false          // explicit; default is already false
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
            if (hasReleaseSigning) {
                signingConfig = signingConfigs.getByName("release")
            }
        }
        debug {
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
            // Enables AGP's built-in JaCoCo coverage for unit tests.
            // Run: gradle createDebugUnitTestCoverageReport
            enableUnitTestCoverage = true
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    buildFeatures {
        viewBinding = true
        buildConfig = true
    }

    testOptions {
        unitTests.isReturnDefaultValues = true
        // Required by Robolectric so unit tests can access merged resources,
        // the manifest and Android framework shadows.
        unitTests.isIncludeAndroidResources = true
    }
}

// AGP 9 replaces android.kotlinOptions with this top-level block.
kotlin {
    compilerOptions {
        jvmTarget.set(JvmTarget.JVM_17)
    }
}

dependencies {
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("com.google.android.material:material:1.12.0")
    implementation("androidx.constraintlayout:constraintlayout:2.1.4")
    implementation("androidx.preference:preference-ktx:1.2.1")

    // Coroutines for background work (update checks etc.)
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.8.1")

    // Unit testing
    testImplementation("junit:junit:4.13.2")
    testImplementation("org.jetbrains.kotlinx:kotlinx-coroutines-test:1.8.1")
    testImplementation("io.mockk:mockk:1.13.12")
    // Robolectric lets JVM unit tests exercise Android framework classes
    // (SharedPreferences, Context, notifications) without a device.
    testImplementation("org.robolectric:robolectric:4.14.1")
    testImplementation("androidx.test:core:1.6.1")
    testImplementation("androidx.test.ext:junit:1.2.1")
    // FragmentScenario for Robolectric fragment tests.
    debugImplementation("androidx.fragment:fragment-testing:1.8.5")
    testImplementation("androidx.fragment:fragment-testing:1.8.5")

    // Instrumented testing
    androidTestImplementation("androidx.test.ext:junit:1.2.1")
    androidTestImplementation("androidx.test.espresso:espresso-core:3.6.1")
}
