# Ultimate (FR33THY, MIT) · Windows/13 Bloatware.ps1 (opção 6)
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




Write-Host "Install: Legacy Features..."

# open legacy optional features
Start-Process "C:\Windows\System32\OptionalFeatures.exe"

# legacy list
Write-Host ""
Write-Host "---------------------------------------------"
Write-Host "      Default Windows Install List W11"
Write-Host "---------------------------------------------"
Write-Host ""
Write-Host "- .Net Framework 4.8 Advanced Services +"
Write-Host "- WCF Services +"
Write-Host "- TCP Port Sharing"
Write-Host "- Media Features +"
Write-Host "- Windows Media Player Legacy (App)"
Write-Host "- Microsoft Print to PDF"
Write-Host "- Print and Document Services +"
Write-Host "- Internet Printing Client"
Write-Host "- Remote Differential Compression API Support"
Write-Host "- SMB Direct"
Write-Host "- Windows PowerShell 2.0 +"
Write-Host "- Windows PowerShell 2.0 Engine"
Write-Host "- Work Folders Client"
Write-Host ""
Write-Host "---------------------------------------------"
Write-Host "      Default Windows Install List W10"
Write-Host "---------------------------------------------"
Write-Host ""
Write-Host "- .Net Framework 4.8 Advanced Services +"
Write-Host "- WCF Services +"
Write-Host "- TCP Port Sharing"
Write-Host "- Internet Explorer 11"
Write-Host "- Media Features +"
Write-Host "- Windows Media Player"
Write-Host "- Microsoft Print to PDF"
Write-Host "- Microsoft XPS Document Writer"
Write-Host "- Print and Document Services +"
Write-Host "- Internet Printing Client"
Write-Host "- Remote Differential Compression API Support"
Write-Host "- SMB 1.0/CIFS File Sharing Support +"
Write-Host "- SMB 1.0/CIFS Automatic Removal"
Write-Host "- SMB 1.0/CIFS Client"
Write-Host "- SMB Direct"
Write-Host "- Windows PowerShell 2.0 +"
Write-Host "- Windows PowerShell 2.0 Engine"
Write-Host "- Work Folders Client"
Write-Host ""
