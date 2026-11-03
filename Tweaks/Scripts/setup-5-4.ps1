# Ultimate (FR33THY, MIT) · 3 Setup/5 Updates Drivers Block.ps1 (opção 4)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




Write-Host "Blocked: Updates"

# block all windows updates
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "DoNotConnectToWindowsUpdateInternetLocations" /t REG_DWORD /d 1 /f | Out-Null
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "UpdateServiceUrlAlternate" /t REG_SZ /d "https://fuckyoumicrosoft.com/" /f | Out-Null
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "WUStatusServer" /t REG_SZ /d "https://fuckyoumicrosoft.com/" /f | Out-Null
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "WUServer" /t REG_SZ /d "https://fuckyoumicrosoft.com/" /f | Out-Null
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "SetDisableUXWUAccess" /t REG_DWORD /d 1 /f | Out-Null
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "ExcludeWUDriversInQualityUpdate" /t REG_DWORD /d 1 /f | Out-Null
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU" /v "NoAutoUpdate" /t REG_DWORD /d 1 /f | Out-Null
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU" /v "UseWUServer" /t REG_DWORD /d 1 /f | Out-Null
