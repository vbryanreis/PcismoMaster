# Ultimate (FR33THY, MIT) · Windows/15 Bloatware Legacy Features Check.ps1
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

Start-Process "$env:SystemDrive\Windows\system32\optionalfeatures.exe"

dism /online /get-features /format:table

Write-Host ""
