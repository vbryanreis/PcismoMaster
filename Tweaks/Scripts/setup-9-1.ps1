# Ultimate (FR33THY, MIT) · 3 Setup/9 Background Apps.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




# disable background apps regedit
cmd /c "reg add `"HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy`" /v `"LetAppsRunInBackground`" /t REG_DWORD /d `"2`" /f >nul 2>&1"

# disable background apps global regedit
cmd /c "reg add `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Search`" /v `"BackgroundAppGlobalToggle`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
cmd /c "reg add `"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications`" /v `"GlobalUserDisabled`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# open settings
Start-Process ms-settings:privacy-backgroundapps
