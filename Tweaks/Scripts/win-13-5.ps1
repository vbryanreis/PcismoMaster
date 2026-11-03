# Ultimate (FR33THY, MIT) · Windows/13 Bloatware.ps1 (opção 5)
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




Write-Host "Install: UWP Features...`n"
Write-Host "Installing multiple features at once may fail"
Write-Host "If so, restart PC between each feature install`n"

# open uwp optional features
Start-Process "ms-settings:optionalfeatures"

# uwp list
Write-Host ""
Write-Host "---------------------------------------------"
Write-Host "      Default Windows Install List W11"
Write-Host "---------------------------------------------"
Write-Host ""
Write-Host "- Extended Theme Content"
Write-Host "- Facial Recognition (Windows Hello)"
Write-Host "- Internet Explorer mode"
Write-Host "- Math Recognizer"
Write-Host "- Notepad (system)"
Write-Host "- OpenSSH Client"
Write-Host "- Print Management"
Write-Host "- Steps Recorder"
Write-Host "- WMIC"
Write-Host "- Windows Media Player Legacy (App)"
Write-Host "- Windows PowerShell ISE"
Write-Host "- WordPad"
Write-Host ""
Write-Host "---------------------------------------------"
Write-Host "      Default Windows Install List W10"
Write-Host "---------------------------------------------"
Write-Host ""
Write-Host "- Internet Explorer 11"
Write-Host "- Math Recognizer"
Write-Host "- Microsoft Quick Assist (App)"
Write-Host "- Notepad (system)"
Write-Host "- OpenSSH Client"
Write-Host "- Print Management Console"
Write-Host "- Steps Recorder"
Write-Host "- Windows Fax and Scan"
Write-Host "- Windows Hello Face"
Write-Host "- Windows Media Player Legacy (App)"
Write-Host "- Windows PowerShell Integrated Scripting Environment"
Write-Host "- WordPad"
Write-Host ""
