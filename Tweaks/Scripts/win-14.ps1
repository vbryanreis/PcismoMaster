# Ultimate (FR33THY, MIT) · Windows/14 Bloatware Legacy Apps Check.ps1
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

Start-Process "$env:SystemDrive\Windows\system32\appwiz.cpl"

Get-Package | Select-Object Name | Format-Table -AutoSize

Write-Host ""
