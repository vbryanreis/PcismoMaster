# Ultimate (FR33THY, MIT) · Windows/16 Bloatware UWP Apps Check.ps1
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN
		
Start-Process "ms-settings:appsfeatures"

Get-AppXPackage | select name | format-table -autosize
