# Ultimate (FR33THY, MIT) · Windows/17 Bloatware UWP Features Check.ps1
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN
		
Start-Process "ms-settings:optionalfeatures"

Get-WindowsCapability -Online | Select-Object Name, State | Format-Table -AutoSize

Write-Host ""
