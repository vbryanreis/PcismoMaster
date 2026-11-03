# Ultimate (FR33THY, MIT) · Advanced/13 Hardware Composed Independent Flip.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Scheduler`" /v `"ForceFlipTrueImmediateMode`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
