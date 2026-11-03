# Ultimate (FR33THY, MIT) · Windows/13 Bloatware.ps1 (opção 7)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        # SCRIPT CHECK INTERNET
        if (!(Test-Connection -ComputerName "8.8.8.8" -Count 1 -Quiet -ErrorAction SilentlyContinue)) {
        Write-Host "Internet Connection Required`n" -ForegroundColor Red
        }

        # SCRIPT SILENT
        $progresspreference = 'silentlycontinue'

        # ALLOW PASSWORD SIGN IN
        cmd /c "reg add `"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device`" /v `"DevicePasswordLessBuildVersion`" /t REG_DWORD /d `"0`" /f >nul 2>&1"




Write-Host "Installing: One Drive. Please wait..."

# install onedrive w10
cmd /c "C:\Windows\SysWOW64\OneDriveSetup.exe >nul 2>&1"

# install onedrive w11
cmd /c "C:\Windows\System32\OneDriveSetup.exe >nul 2>&1"
