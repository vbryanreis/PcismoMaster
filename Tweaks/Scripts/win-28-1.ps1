# Ultimate (FR33THY, MIT) · Windows/28 Write Cache Buffer Flushing.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "If using 'NVME Faster Driver.ps1' apply that"
        Write-Host "and restart first before proceeding with this`n"



Write-Host "Write Cache Buffer Flushing: Off..."

# turn off windows write-cache buffer flushing on the device on all connected scsi devices
$basePath = "HKLM:\SYSTEM\ControlSet001\Enum\SCSI"
Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "Device Parameters" } | ForEach-Object {
$diskPath = Join-Path $_.PSPath "Disk"
cmd /c "reg add `"$(($diskPath -replace 'Microsoft.PowerShell.Core\\Registry::',''))`" /v `"CacheIsPowerProtected`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
}

# turn off windows write-cache buffer flushing on the device on all connected nvme devices
$basePath = "HKLM:\SYSTEM\ControlSet001\Enum\NVME"
Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "Device Parameters" } | ForEach-Object {
$diskPath = Join-Path $_.PSPath "Disk"
cmd /c "reg add `"$(($diskPath -replace 'Microsoft.PowerShell.Core\\Registry::',''))`" /v `"CacheIsPowerProtected`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
}
