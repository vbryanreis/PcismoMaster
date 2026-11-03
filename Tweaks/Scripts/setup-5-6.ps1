# Ultimate (FR33THY, MIT) · 3 Setup/5 Updates Drivers Block.ps1 (opção 6)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




Write-Host "Unblocked: Updates"

# revert block all windows updates
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "DoNotConnectToWindowsUpdateInternetLocations" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "UpdateServiceUrlAlternate" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "WUStatusServer" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "WUServer" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "SetDisableUXWUAccess" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "ExcludeWUDriversInQualityUpdate" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU" /v "NoAutoUpdate" /f | Out-Null
reg delete "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU" /v "UseWUServer" /f | Out-Null
