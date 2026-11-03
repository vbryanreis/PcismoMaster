# Ultimate (FR33THY, MIT) · Advanced/19 NVME Faster Driver.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "BREAKS MICROSOFT DIRECT STORAGE" -ForegroundColor Red



Write-Host "NVME: Faster Driver..."

# enable new nvme driver
cmd /c "reg add `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Policies\Microsoft\FeatureManagement\Overrides`" /v `"735209102`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
cmd /c "reg add `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Policies\Microsoft\FeatureManagement\Overrides`" /v `"3244671118`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
cmd /c "reg add `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Policies\Microsoft\FeatureManagement\Overrides`" /v `"1853569164`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
cmd /c "reg add `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Policies\Microsoft\FeatureManagement\Overrides`" /v `"156965516`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# enable safe & safe network boot fix for new nvme driver
cmd /c "reg add `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SafeBoot\Network\{75416E63-5912-4DFA-AE8F-3EFACCAFFB14}`" /ve /d `"Storage disks`" /f >nul 2>&1"
cmd /c "reg add `"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal\{75416E63-5912-4DFA-AE8F-3EFACCAFFB14}`" /ve /d `"Storage disks`" /f >nul 2>&1"
