# Ultimate (FR33THY, MIT) · 3 Setup/1 BitLocker.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




Write-Host "BitLocker: On..."

# open settings
Start-Process control.exe -ArgumentList "/name microsoft.bitlockerdriveencryption"

manage-bde -status
