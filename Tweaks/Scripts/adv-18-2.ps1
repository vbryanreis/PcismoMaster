# Ultimate (FR33THY, MIT) · Advanced/18 Start Search Shell Mobsync.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "Start Search Shell Mobsync:"



Write-Host "Start Search Shell Mobsync: Default...`n"

# takeownership of folders
cmd /c "takeown.exe /f $env:SystemRoot\Microsoft.Windows.Search_cw5n1h2txyewy >nul 2>&1"
cmd /c "icacls.exe $env:SystemRoot\Microsoft.Windows.Search_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
cmd /c "takeown.exe /f $env:SystemRoot\ShellExperienceHost_cw5n1h2txyewy >nul 2>&1"
cmd /c "icacls.exe $env:SystemRoot\ShellExperienceHost_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
cmd /c "takeown.exe /f $env:SystemRoot\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy >nul 2>&1"
cmd /c "icacls.exe $env:SystemRoot\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy /grant *S-1-3-4:F /t /q >nul 2>&1"
cmd /c "takeown.exe /f $env:SystemRoot\mobsync.exe >nul 2>&1"
cmd /c "icacls.exe $env:SystemRoot\mobsync.exe /grant *S-1-3-4:F /t /q >nul 2>&1"

# move folders
cmd /c "move /y $env:SystemRoot\Microsoft.Windows.Search_cw5n1h2txyewy $env:SystemRoot\SystemApps >nul 2>&1"
cmd /c "move /y $env:SystemRoot\ShellExperienceHost_cw5n1h2txyewy $env:SystemRoot\SystemApps >nul 2>&1"
cmd /c "move /y $env:SystemRoot\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy $env:SystemRoot\SystemApps >nul 2>&1"
cmd /c "move /y $env:SystemRoot\mobsync.exe $env:SystemRoot\System32 >nul 2>&1"

# enable uwp search completely
cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\PolicyManager\default\Search\DisableSearch`" /v `"value`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
cmd /c "reg delete `"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search`" /f >nul 2>&1"

# enable uwp search box taskbar
cmd /c "reg delete `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search`" /v `"SearchboxTaskbarMode`" /f >nul 2>&1"

# enable legacy search service
cmd /c "reg add `"HKLM\SYSTEM\ControlSet001\Services\WSearch`" /v `"Start`" /t REG_DWORD /d `"2`" /f >nul 2>&1"

# stop explorer
cmd /c "taskkill /F /IM explorer.exe >nul 2>&1"

# start explorer
cmd /c "start explorer.exe >nul 2>&1"

# pause so search service will start
Start-Sleep 15

# start legacy search service
cmd /c "sc stop WSearch >nul 2>&1"

# start explorer
cmd /c "start explorer.exe >nul 2>&1"
