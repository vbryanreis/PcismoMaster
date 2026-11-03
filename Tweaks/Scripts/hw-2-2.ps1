# Ultimate (FR33THY, MIT) · Hardware/2 Background Polling Rate Cap.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "Background Polling Rate Cap:`n"



# revert unlock background polling rate cap
cmd /c "reg delete `"HKCU\Control Panel\Mouse`" /v `"RawMouseThrottleEnabled`" /f >nul 2>&1"
