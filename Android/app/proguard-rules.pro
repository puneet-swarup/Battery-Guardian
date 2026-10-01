# Keep line numbers for readable crash reports.
-keepattributes SourceFile,LineNumberTable
-renamesourcefileattribute SourceFile

# Keep our model classes used by JSON parsing if reflection is ever added.
-keep class com.puneet.batteryguardian.data.** { *; }

# Coroutines
-dontwarn kotlinx.coroutines.**
