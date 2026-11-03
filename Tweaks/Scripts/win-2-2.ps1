# Ultimate (FR33THY, MIT) · Windows/2 Start Menu Layout.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




# create reg file
$OldStartMenu = @"
Windows Registry Editor Version 5.00

; old start menu
[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\2792562829]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\3036241548]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\734731404]
"EnabledState"=-

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\762256525]
"EnabledState"=-

; set start menu apps view to category
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"AllAppsViewMode"=dword:00000000
"@
Set-Content -Path "$env:SystemRoot\Temp\oldstartmenu.reg" -Value $OldStartMenu -Force

# import reg file
Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\oldstartmenu.reg`"" -WindowStyle Hidden
