# Ultimate (FR33THY, MIT) · Hardware/4 Controller Overclock.ps1
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        # SCRIPT CHECK INTERNET
        if (!(Test-Connection -ComputerName "8.8.8.8" -Count 1 -Quiet -ErrorAction SilentlyContinue)) {
        Write-Host "Internet Connection Required`n" -ForegroundColor Red
        }

        # SCRIPT SILENT
        $progresspreference = 'silentlycontinue'

Write-Host "Installing: hidusbf..."

# download hidusbf
IWR "https://github.com/FR33THYFR33THY/Ultimate/releases/download/Files/hidusbf.zip" -OutFile "$env:SystemRoot\Temp\hidusbf.zip"

# extract file
Expand-Archive -Path "$env:SystemRoot\Temp\hidusbf.zip" -DestinationPath "$env:SystemDrive\Program Files (x86)\hidusbf" -Force

# move files
Move-Item -Path "$env:SystemDrive\Program Files (x86)\hidusbf\hidusbf (BB11.5.25)\*" -Destination "$env:SystemDrive\Program Files (x86)\hidusbf" -Force

# delete folder
Remove-Item "$env:SystemDrive\Program Files (x86)\hidusbf\hidusbf (BB11.5.25)" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null

# create desktop shortcut
$WshShell = New-Object -comObject WScript.Shell
$Desktop = (New-Object -ComObject Shell.Application).Namespace('shell:Desktop').Self.Path
$Shortcut = $WshShell.CreateShortcut("$Desktop\Setup.lnk")
$Shortcut.TargetPath = "$env:SystemDrive\Program Files (x86)\hidusbf\DRIVER\Setup.exe"
$Shortcut.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\hidusbf\DRIVER"
$Shortcut.Save()

# create start menu shortcut
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Setup.lnk")
$Shortcut.TargetPath = "$env:SystemDrive\Program Files (x86)\hidusbf\DRIVER\Setup.exe"
$Shortcut.WorkingDirectory = "$env:SystemDrive\Program Files (x86)\hidusbf\DRIVER"
$Shortcut.Save()

# install hidusbf_as.inf
Start-Process -FilePath "rundll32.exe" -ArgumentList "setupapi.dll,InstallHinfSection DefaultInstall 132 $env:SystemDrive\Program Files (x86)\hidusbf\DRIVER\HIDUSBF_AS.INF" -Wait
