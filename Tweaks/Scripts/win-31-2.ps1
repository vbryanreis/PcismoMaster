# Ultimate (FR33THY, MIT) · Windows/31 UAC.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




# enable uac
cmd /c "reg add `"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System`" /v `"EnableLUA`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
