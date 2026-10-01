@echo off
REM Battery Guardian (Android) - build helper
REM Uses the locally installed Gradle 9.4.1 + AGP 9.2.0 toolchain.
REM Usage:  build.bat [test^|coverage^|debug^|release^|install^|clean^|lint]

setlocal
cd /d "%~dp0"

if "%~1"=="" goto :usage
if /i "%~1"=="test"     goto :test
if /i "%~1"=="coverage" goto :coverage
if /i "%~1"=="debug"    goto :debug
if /i "%~1"=="release"  goto :release
if /i "%~1"=="install"  goto :install
if /i "%~1"=="clean"    goto :clean
if /i "%~1"=="lint"     goto :lint
goto :usage

:test
echo ==^> Running unit tests...
call gradle testDebugUnitTest --no-daemon
goto :end

:coverage
echo ==^> Running unit tests with coverage...
call gradle testDebugUnitTest createDebugUnitTestCoverageReport --no-daemon
echo.
echo Coverage report: app\build\reports\coverage\test\debug\index.html
goto :end

:debug
echo ==^> Building debug APK...
call gradle assembleDebug --no-daemon
echo.
echo Debug APK: app\build\outputs\apk\debug\app-debug.apk
goto :end

:release
echo ==^> Building release APK (requires signing env vars)...
if "%BG_STORE_FILE%"=="" (
    echo WARNING: BG_STORE_FILE is not set. The release APK will be unsigned.
)
call gradle assembleRelease --no-daemon
echo.
echo Release APK: app\build\outputs\apk\release\app-release.apk
goto :end

:install
echo ==^> Building and installing on the connected device...
call gradle installDebug --no-daemon
goto :end

:clean
echo ==^> Cleaning build outputs...
call gradle clean --no-daemon
goto :end

:lint
echo ==^> Running Android lint...
call gradle lintDebug --no-daemon
echo.
echo Report: app\build\reports\lint-results-debug.html
goto :end

:usage
echo Usage: build.bat [test^|coverage^|debug^|release^|install^|clean^|lint]
echo.
echo   test      Run JVM unit tests
echo   coverage  Run unit tests and generate a JaCoCo coverage report
echo   debug     Build the debug APK
echo   release   Build the release APK (needs signing env vars)
echo   install   Build and install the debug APK on a connected device
echo   clean     Remove build outputs
echo   lint      Run Android lint
exit /b 1

:end
endlocal
