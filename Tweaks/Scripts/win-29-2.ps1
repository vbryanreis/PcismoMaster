# Ultimate (FR33THY, MIT) · Windows/29 Power Plan.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




# restore powerplans
powercfg -restoredefaultschemes

# revert turn off display after 10 min - oled protection
powercfg /requestsoverride PROCESS "chrome.exe" 2>$null
powercfg /requestsoverride PROCESS "Discord.exe" 2>$null

# enable hibernate
cmd /c "powercfg /hibernate on >nul 2>&1"
cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\Power`" /v `"HibernateEnabled`" /f >nul 2>&1"
cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Power`" /v `"HibernateEnabledDefault`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# enable lock & sleep
cmd /c "reg delete `"HKLM\Software\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings`" /f >nul 2>&1"

# enable fast boot
cmd /c "reg add `"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power`" /v `"HiberbootEnabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# enable power throttling
cmd /c "reg delete `"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling`" /f >nul 2>&1"

# hide hub selective suspend timeout
cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\0853a681-27c8-4100-a2fd-82013e970683`" /v `"Attributes`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# hide usb 3 link power management
cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\2a737441-1930-4402-8d77-b2bebba308a3\d4e98f31-5ffe-4ce1-be31-1b38b384c009`" /v `"Attributes`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# hide processor performance core parking min cores
cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583`" /v `"Attributes`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# hide processor performance core parking max cores
cmd /c "reg add `"HKLM\System\ControlSet001\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\ea062031-0e34-4ff1-9b6d-eb1059334028`" /v `"Attributes`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# open settings
Start-Process powercfg.cpl
