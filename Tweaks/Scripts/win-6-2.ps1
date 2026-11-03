# Ultimate (FR33THY, MIT) · Windows/6 Signout Lockscreen Wallpaper Black.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




# revert set image
cmd /c "reg delete `"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP`" /f >nul 2>&1"

# revert black wallpaper
cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"Wallpaper`" /t REG_SZ /d `"C:\Windows\Web\Wallpaper\Windows\img0.jpg`" /f >nul 2>&1"
rundll32.exe user32.dll, UpdatePerUserSystemParameters

# delete image
Remove-Item -Recurse -Force "C:\Windows\Black.jpg" -ErrorAction SilentlyContinue | Out-Null

# revert background blur during sign-in
cmd /c "reg delete `"HKLM\SOFTWARE\Policies\Microsoft\Windows\System`" /v `"DisableAcrylicBackgroundOnLogon`" /f >nul 2>&1"
