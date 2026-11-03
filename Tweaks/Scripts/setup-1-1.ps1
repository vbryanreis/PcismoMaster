# Ultimate (FR33THY, MIT) · 3 Setup/1 BitLocker.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




Write-Host "BitLocker: Off..."

# disable bitlocker
try {
Get-BitLockerVolume |
Where-Object {
$_.ProtectionStatus -eq "On" -or $_.VolumeStatus -ne "FullyDecrypted"
} |
ForEach-Object {
Disable-BitLocker -MountPoint $_.MountPoint -ErrorAction SilentlyContinue | Out-Null
}
} catch { }

# open settings
Start-Process control.exe -ArgumentList "/name microsoft.bitlockerdriveencryption"

manage-bde -status
