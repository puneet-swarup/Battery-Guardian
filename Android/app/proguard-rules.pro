# Keep line numbers for readable crash reports.
-keepattributes SourceFile,LineNumberTable
-renamesourcefileattribute SourceFile

# Keep our model classes used by JSON parsing if reflection is ever added.
-keep class com.puneet.batteryguardian.data.** { *; }

# Coroutines
-dontwarn kotlinx.coroutines.**

# The Tuya LAN SDK bundles a cloud client that uses OkHttp. This app only uses
# the local client, so those classes are never loaded - keep R8 quiet about them
# rather than shipping a megabyte of HTTP stack.
-dontwarn okhttp3.**
-dontwarn okio.**
-dontwarn com.tuyalan.cloud.**
