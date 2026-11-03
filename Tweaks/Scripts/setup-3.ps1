# Ultimate (FR33THY, MIT) · 3 Setup/3 Convert Home To Pro.ps1
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

Write-Host "Disable Internet First`n"

# copy key to clipboard
Set-Clipboard -Value "VK7JG-NPHTM-C97JM-9MPGT-3V66T"

Write-Host "Enter: VK7JG-NPHTM-C97JM-9MPGT-3V66T (Or Paste From Clipboard)`n"

# open activation screen
Start-Process ms-settings:activation
& "$env:windir\System32\SystemSettingsAdminFlows.exe" 'EnterProductKey'
