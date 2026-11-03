# Ultimate (FR33THY, MIT) · Windows/3 Start Menu Shortcuts.ps1
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

# create start menu & startup shortcuts
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Start Menu Shortcuts 1.lnk")
$Shortcut.TargetPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs"
$Shortcut.Save()
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Start Menu Shortcuts 2.lnk")
$Shortcut.TargetPath = "$env:AppData\Microsoft\Windows\Start Menu\Programs"
$Shortcut.Save()
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Startup Programs 1.lnk")
$Shortcut.TargetPath = "$env:AppData\Microsoft\Windows\Start Menu\Programs\Startup"
$Shortcut.Save()
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Startup Programs 2.lnk")
$Shortcut.TargetPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp"
$Shortcut.Save()

# create recycle bin shortcut
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Recycle Bin.lnk")
$Shortcut.TargetPath = '::{645ff040-5081-101b-9f08-00aa002f954e}'
$Shortcut.Save()

# open start menu shortcuts 1 location
Start-Process "$env:ProgramData\Microsoft\Windows\Start Menu\Programs"

# open start menu shortcuts 2 location
Start-Process "$env:AppData\Microsoft\Windows\Start Menu\Programs"
