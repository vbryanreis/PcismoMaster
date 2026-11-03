# Ultimate (FR33THY, MIT) · Windows/2 Start Menu Layout.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




# create reg file
$NewStartMenu = @"
Windows Registry Editor Version 5.00

; new start menu
[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\2792562829]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\3036241548]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\734731404]
"EnabledState"=dword:00000002

[HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\FeatureManagement\Overrides\14\762256525]
"EnabledState"=dword:00000002

; set start menu apps view to list
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Start]
"AllAppsViewMode"=dword:00000002
"@
Set-Content -Path "$env:SystemRoot\Temp\newstartmenu.reg" -Value $NewStartMenu -Force

# import reg file
Start-Process -Wait "regedit.exe" -ArgumentList "/S `"$env:SystemRoot\Temp\newstartmenu.reg`"" -WindowStyle Hidden
