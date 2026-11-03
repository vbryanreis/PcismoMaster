# Ultimate (FR33THY, MIT) · Advanced/19 NVME Faster Driver.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "BREAKS MICROSOFT DIRECT STORAGE" -ForegroundColor Red



Write-Host "NVME: Default..."

# revert new nvme driver
cmd /c "reg delete `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Policies\Microsoft`" /f >nul 2>&1"

# revert safe & safe network boot fix for new nvme driver
cmd /c "reg delete `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SafeBoot\Network\{75416E63-5912-4DFA-AE8F-3EFACCAFFB14}`" /f >nul 2>&1"
cmd /c "reg delete `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal\{75416E63-5912-4DFA-AE8F-3EFACCAFFB14}`" /f >nul 2>&1"
