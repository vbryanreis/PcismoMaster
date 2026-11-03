# Ultimate (FR33THY, MIT) · Advanced/18 Start Search Shell Mobsync.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "Start Search Shell Mobsync:"



Write-Host "Start Search Shell Mobsync: Off...`n"

# takeownership of folders
cmd /c "takeown.exe /f $env:SystemRoot\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy >nul 2>&1"
cmd /c "icacls.exe $env:SystemRoot\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
cmd /c "takeown.exe /f $env:SystemRoot\SystemApps\ShellExperienceHost_cw5n1h2txyewy >nul 2>&1"
cmd /c "icacls.exe $env:SystemRoot\SystemApps\ShellExperienceHost_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
cmd /c "takeown.exe /f $env:SystemRoot\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy >nul 2>&1"
cmd /c "icacls.exe $env:SystemRoot\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
cmd /c "takeown.exe /f $env:SystemRoot\System32\mobsync.exe >nul 2>&1"
cmd /c "icacls.exe $env:SystemRoot\System32\mobsync.exe /grant *S-1-3-4:F /t /q >nul 2>&1"

# stop tasks
$stop = "AccountsServiceProduct",
        "AppActions",
        "Copilot",
        "CrossDeviceResume",
        "DesktopSpotlightProduct",
        "DesktopStickerEditorWin32Exe",
        "DiscoveryHubApp",
        "FESearchHost",
        "GameBar",
        "IrisServiceProduct",
        "LogonWebHostProduct",
        "MicrosoftEdgeUpdate",
        "MiniSearchHost",
        "OneDrive",
        "OneDrive.Sync.Service",
        "OneDriveStandaloneUpdater",
        "Resume",
        "RulesEngineProduct",
        "RuntimeBroker",
        "ScreenClippingHost",
        "Search",
        "SearchApp",
        "SearchHost",
        "Setup",
        "ShellExperienceHost",
        "SoftLandingTask",
        "StartMenuExperienceHost",
        "StoreDesktopExtension",
        "TextInputHost",
        "VisualAssistExe",
        "WebExperienceHostApp",
        "WidgetService",
        "Widgets",
        "WindowsBackupClient",
        "WindowsMigration",
        "backgroundTaskHost",
        "explorer",
        "mobsync",
        "msedge",
        "msedgewebview2",
        "smartscreen"
$stop | ForEach-Object { Stop-Process -Name $_ -Force -ErrorAction SilentlyContinue }

# move folders
cmd /c "move /y $env:SystemRoot\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy $env:SystemRoot >nul 2>&1"
cmd /c "move /y $env:SystemRoot\SystemApps\ShellExperienceHost_cw5n1h2txyewy $env:SystemRoot >nul 2>&1"
cmd /c "move /y $env:SystemRoot\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy $env:SystemRoot >nul 2>&1"
cmd /c "move /y $env:SystemRoot\System32\mobsync.exe $env:SystemRoot >nul 2>&1"

# disable uwp search
cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\PolicyManager\default\Search\DisableSearch`" /v `"value`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search`" /v `"DisableSearch`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# disable uwp search box taskbar
cmd /c "reg add `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search`" /v `"SearchboxTaskbarMode`" /t REG_DWORD /d `"0`" /f >nul 2>&1"

# disable legacy search service
cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\WSearch`" /v `"Start`" /t REG_DWORD /d `"4`" /f >nul 2>&1"

# stop explorer
cmd /c "taskkill /F /IM explorer.exe >nul 2>&1"

# start explorer
cmd /c "start explorer.exe >nul 2>&1"

# pause so search service will stop
Start-Sleep 15

# stop legacy search service
cmd /c "sc stop WSearch >nul 2>&1"

# reapply for w10 search
cmd /c "takeown.exe /f $env:SystemRoot\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy >nul 2>&1"
cmd /c "icacls.exe $env:SystemRoot\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
cmd /c "taskkill /F /IM SearchApp.exe >nul 2>&1"
cmd /c "taskkill /F /IM SearchHost.exe >nul 2>&1"
cmd /c "move /y $env:SystemRoot\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy $env:SystemRoot >nul 2>&1"
