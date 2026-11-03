# Ultimate (FR33THY, MIT) · Advanced/4 Data Execution Prevention.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN
        Write-Host "SECURE BOOT MUST BE OFF FOR 'Disable' TO WORK`n"
        Write-Host "Data Execution Prevention:"



# disable data execution prevention
cmd /c "bcdedit /set nx AlwaysOff >nul 2>&1"
