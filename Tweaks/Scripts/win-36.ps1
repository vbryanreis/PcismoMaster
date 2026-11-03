# Ultimate (FR33THY, MIT) · Windows/36 Restore Point.ps1
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

Write-Host "Creating: Restore Point..."

try {
# allow multiple restore points
cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore`" /v `"SystemRestorePointCreationFrequency`" /t REG_DWORD /d `"0`" /f >nul 2>&1"

# enable restore point
Enable-ComputerRestore -Drive "C:\" -ErrorAction SilentlyContinue | Out-Null

# create restore point
Checkpoint-Computer -Description "backup" -RestorePointType "MODIFY_SETTINGS" -ErrorAction SilentlyContinue | Out-Null
} catch { }

# open system protection
Start-Process "$env:SystemRoot\system32\control.exe" -ArgumentList "sysdm.cpl,,4"

# open system restore
Start-Process "rstrui"
