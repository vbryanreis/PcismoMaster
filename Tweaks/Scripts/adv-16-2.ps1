# Ultimate (FR33THY, MIT) · Advanced/16 Keyboard Shortcuts.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




Write-Host "Keyboard Shortcuts: Off`n" -ForegroundColor Red
Write-Host "- disable all keyboard shortcuts"
Write-Host "- to prevent tabbing out of a game"
Write-Host "- cut copy paste will function"
Write-Host "- esc rebound to =`n"


# disable media keys (human interface device service)
reg add "HKLM\SYSTEM\ControlSet001\Services\hidserv" /v "Start" /t REG_DWORD /d "4" /f | Out-Null

# disable windows key hotkeys
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer" /v "NoWinKeys" /t REG_DWORD /d "1" /f | Out-Null

# disable shortcut keys
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" /v "DisabledHotkeys" /t REG_DWORD /d "1" /f | Out-Null

# disable win alt esc keys
# esc rebound to =
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layout" /v "Scancode Map" /t REG_BINARY /d "00000000000000000700000000005be000005ce000003800000038e00000010001000d0000000000" /f | Out-Null
