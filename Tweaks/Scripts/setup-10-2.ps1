# Ultimate (FR33THY, MIT) · 3 Setup/10 Edge Settings.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        # SCRIPT CHECK INTERNET
        if (!(Test-Connection -ComputerName "8.8.8.8" -Count 1 -Quiet -ErrorAction SilentlyContinue)) {
        Write-Host "Internet Connection Required`n" -ForegroundColor Red
        }

        # SCRIPT SILENT
        $progresspreference = 'silentlycontinue'




Write-Host "Edge Settings: Default..."

# remove ublock origin
# remove edge policies
cmd /c "reg delete `"HKLM\SOFTWARE\Policies\Microsoft\Edge`" /f >nul 2>&1"

# stop edge running
Stop-Process -Name "msedge" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# reset edge settings
Start-Process "msedge.exe" -ArgumentList "--restore-last-session --disable-extensions"
Start-Sleep -Seconds 2

# stop edge running
Stop-Process -Name "msedge" -Force -ErrorAction SilentlyContinue

# download edge installer
IWR "https://github.com/FR33THYFR33THY/Ultimate/releases/download/Files/edge.exe" -OutFile "$env:SystemRoot\Temp\edge.exe"

# start edge installer
Start-Process "$env:SystemRoot\Temp\edge.exe"
