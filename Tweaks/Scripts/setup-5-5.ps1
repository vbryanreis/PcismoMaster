# Ultimate (FR33THY, MIT) · 3 Setup/5 Updates Drivers Block.ps1 (opção 5)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




Write-Host "Blocked: Updates (Bootable USB)"

# create setupcomplete.cmd
$SetupCompleteCmd = @'
@echo off
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "DoNotConnectToWindowsUpdateInternetLocations" /t REG_DWORD /d 1 /f
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "UpdateServiceUrlAlternate" /t REG_SZ /d "https://fuckyoumicrosoft.com/" /f
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "WUStatusServer" /t REG_SZ /d "https://fuckyoumicrosoft.com/" /f
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "WUServer" /t REG_SZ /d "https://fuckyoumicrosoft.com/" /f
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "SetDisableUXWUAccess" /t REG_DWORD /d 1 /f
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" /v "ExcludeWUDriversInQualityUpdate" /t REG_DWORD /d 1 /f
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU" /v "NoAutoUpdate" /t REG_DWORD /d 1 /f
reg add "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate\AU" /v "UseWUServer" /t REG_DWORD /d 1 /f
shutdown /r /t 0
'@
Set-Content -Path "$env:SystemRoot\Temp\setupcomplete.cmd" -Value $SetupCompleteCmd -Force

# user input select usb
$destination = $env:PM_USB
$destination += ":\"

# create scripts folder
New-Item -Path "$destination\sources\`$OEM`$\`$`$\Setup\Scripts" -ItemType Directory -Force | Out-Null

# move setupcomplete.cmd to usb
Move-Item -Path "$env:SystemRoot\Temp\setupcomplete.cmd" -Destination "$destination\sources\`$OEM`$\`$`$\Setup\Scripts" -Force

# open usb directory to confirm
Start-Process "$destination\sources\`$OEM`$\`$`$\Setup\Scripts"
