# Ultimate (FR33THY, MIT) · Advanced/15 Driver Whql Secure Boot Bypass.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "Driver WHQL Secure Boot Bypass:"



# revert driver whql secure boot bypass
cmd.exe /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy`" /v `"WHQLSettings`" /f >nul 2>&1"
