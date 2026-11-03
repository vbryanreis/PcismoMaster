# Ultimate (FR33THY, MIT) · Hardware/5 Controller Polling Rate Test.ps1
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        # SCRIPT CHECK INTERNET
        if (!(Test-Connection -ComputerName "8.8.8.8" -Count 1 -Quiet -ErrorAction SilentlyContinue)) {
        Write-Host "Internet Connection Required`n" -ForegroundColor Red
        }

        # SCRIPT SILENT
        $progresspreference = 'silentlycontinue'

Write-Host "Installing: Polling..."

# new folder
New-Item -Path "$env:SystemDrive\Program Files (x86)\Polling" -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null

# download gamepadla
IWR "https://github.com/FR33THYFR33THY/Ultimate/releases/download/Files/polling.exe" -OutFile "$env:SystemDrive\Program Files (x86)\Polling\Polling.exe"

# create desktop shortcut
$WshShell = New-Object -comObject WScript.Shell
$Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
$Shortcut = $WshShell.CreateShortcut("$Desktop\Polling.lnk")
$Shortcut.TargetPath = "$env:SystemDrive\Program Files (x86)\Polling\Polling.exe"
$Shortcut.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\Polling"
$Shortcut.Save()

# create start menu shortcut
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Polling.lnk")
$Shortcut.TargetPath = "$env:SystemDrive\Program Files (x86)\Polling\Polling.exe"
$Shortcut.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\Polling"
$Shortcut.Save()

# open gamepadla
Start-Process "$env:SystemDrive\Program Files (x86)\Polling\Polling.exe"
