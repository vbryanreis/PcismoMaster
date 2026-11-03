# Ultimate (FR33THY, MIT) · Windows/28 Write Cache Buffer Flushing.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "If using 'NVME Faster Driver.ps1' apply that"
        Write-Host "and restart first before proceeding with this`n"



Write-Host "Write Cache Buffer Flushing: Default..."

# revert turn off windows write-cache buffer flushing on the device on all connected scsi devices
$basePath = "HKLM:\SYSTEM\ControlSet001\Enum\SCSI"
Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "Disk" } | ForEach-Object {
$diskPath = $_.PSPath -replace 'Microsoft.PowerShell.Core\\Registry::', ''
cmd /c "reg delete `"$diskPath`" /f >nul 2>&1"
}

# revert turn off windows write-cache buffer flushing on the device on all connected nvme devices
$basePath = "HKLM:\SYSTEM\ControlSet001\Enum\NVME"
Get-ChildItem -Path $basePath -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -eq "Disk" } | ForEach-Object {
$diskPath = $_.PSPath -replace 'Microsoft.PowerShell.Core\\Registry::', ''
cmd /c "reg delete `"$diskPath`" /f >nul 2>&1"
}
