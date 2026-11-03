# Ultimate (FR33THY, MIT) · Windows/21 Notepad Settings.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




Write-Host "Notepad Settings: Default..."

# stop notepad running
Stop-Process -Name "Notepad" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# delete settings.dat
Remove-Item "$env:LocalAppData\Packages\Microsoft.WindowsNotepad_8wekyb3d8bbwe\Settings\settings.dat" -Force
