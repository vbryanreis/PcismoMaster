# Ultimate (FR33THY, MIT) · Hardware/1 Scaling Higher No Accel.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "Higher Scaling With No Acceleration`n"



# 100

# 6-11 pointer speed
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseSensitivity`" /t REG_SZ /d `"10`" /f >nul 2>&1"

# disable enhance pointer precision
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseSpeed`" /t REG_SZ /d `"0`" /f >nul 2>&1"
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseThreshold1`" /t REG_SZ /d `"0`" /f >nul 2>&1"
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseThreshold2`" /t REG_SZ /d `"0`" /f >nul 2>&1"

# mouse curve default
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"SmoothMouseXCurve`" /t REG_BINARY /d `"0000000000000000c0cc0c00000000008099190000000000406626000000000000333300000000000`" /f >nul 2>&1"
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"SmoothMouseYCurve`" /t REG_BINARY /d `"0000000000000000000038000000000000007000000000000000a800000000000000e00000000000`" /f >nul 2>&1"

# use custom scaling
cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"Win8DpiScaling`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# dpi scaling 100%
cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"LogPixels`" /t REG_DWORD /d `"96`" /f >nul 2>&1"

# disable fix scaling for apps
cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"EnablePerProcessSystemDPI`" /t REG_DWORD /d `"0`" /f >nul 2>&1"
