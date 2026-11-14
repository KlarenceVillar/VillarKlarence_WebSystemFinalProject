@echo off
REM ==========================================================================
REM  setup-secrets.cmd
REM  Stores the API secrets in "dotnet user-secrets" (in your Windows profile,
REM  OUTSIDE the repository), so they are never committed to GitHub:
REM    Jwt:Key            - a new random 64-byte key generated with PowerShell
REM    Seed:DemoPassword  - Demo@12345 (demo-only password for the seeded accounts)
REM
REM  Usage:
REM    setup-secrets.cmd              (keeps the LocalDB connection string from appsettings.json)
REM    setup-secrets.cmd sqlexpress   (also points the API at .\SQLEXPRESS)
REM ==========================================================================
setlocal EnableExtensions

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] The .NET SDK was not found. Install .NET 10 SDK and try again.
    exit /b 1
)

set "API_DIR=%~dp0src\VillarKlarence_WebSystemFinalProject.Api"
if not exist "%API_DIR%\VillarKlarence_WebSystemFinalProject.Api.csproj" (
    echo [ERROR] Could not find the API project at "%API_DIR%".
    exit /b 1
)

echo Generating a random JWT signing key with PowerShell...
set "JWT_KEY="
for /f "usebackq delims=" %%K in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "$bytes = New-Object byte[] 64; $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($bytes); $rng.Dispose(); [Convert]::ToBase64String($bytes)"`) do set "JWT_KEY=%%K"

if not defined JWT_KEY (
    echo [ERROR] PowerShell could not generate a key.
    exit /b 1
)

pushd "%API_DIR%"

dotnet user-secrets set "Jwt:Key" "%JWT_KEY%" >nul
if errorlevel 1 goto :failed

dotnet user-secrets set "Seed:DemoPassword" "Demo@12345" >nul
if errorlevel 1 goto :failed

if /i "%~1"=="sqlexpress" (
    dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=StudentGradeManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" >nul
    if errorlevel 1 goto :failed
    echo [OK] Connection string set to .\SQLEXPRESS
)

popd
set "JWT_KEY="
echo [OK] Jwt:Key stored in user-secrets (random, 64 bytes).
echo [OK] Seed:DemoPassword stored in user-secrets (demo-only).
echo.
echo Done. Start the API with:  cd src\VillarKlarence_WebSystemFinalProject.Api ^&^& dotnet run
endlocal
exit /b 0

:failed
popd
set "JWT_KEY="
echo [ERROR] dotnet user-secrets failed. Make sure you are running this from the repository root.
endlocal
exit /b 1
