# Ultimate (FR33THY, MIT) · Windows/13 Bloatware.ps1 (opção 9)
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




Write-Host "Installing: Snipping Tool. Please wait..."
Write-Host ""
Write-Host "Ignore installer error W11"
Write-Host "If installer fails on W10, restart PC and rerun script"
Write-Host ""

# download w10 snipping tool
IWR "https://github.com/FR33THYFR33THY/Ultimate/releases/download/Files/snippingtool.exe" -OutFile "$env:SystemRoot\Temp\snippingtool.exe"

# install w10 snipping tool
cmd /c "$env:SystemRoot\Temp\snippingtool.exe >nul 2>&1"

# install w11 snipping tool
Get-AppXPackage -AllUsers *Microsoft.ScreenSketch* | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register -ErrorAction SilentlyContinue "$($_.InstallLocation)\AppXManifest.xml"}
