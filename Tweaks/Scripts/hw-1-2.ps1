# Ultimate (FR33THY, MIT) · Hardware/1 Scaling Higher No Accel.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "Higher Scaling With No Acceleration`n"



# 125

# 6-11 pointer speed
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseSensitivity`" /t REG_SZ /d `"10`" /f >nul 2>&1"

# enable enhance pointer precision
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseSpeed`" /t REG_SZ /d `"1`" /f >nul 2>&1"
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseThreshold1`" /t REG_SZ /d `"6`" /f >nul 2>&1"
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseThreshold2`" /t REG_SZ /d `"10`" /f >nul 2>&1"

# mouse curve 125% scaling
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"SmoothMouseXCurve`" /t REG_BINARY /d `"00000000000000000000100000000000000020000000000000003000000000000000400000000000`" /f >nul 2>&1"
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"SmoothMouseYCurve`" /t REG_BINARY /d `"00000000000000000000380000000000000070000000000000A800000000000000E0000000000000`" /f >nul 2>&1"

# use custom scaling
cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"Win8DpiScaling`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# dpi scaling 125%
cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"LogPixels`" /t REG_DWORD /d `"120`" /f >nul 2>&1"

# enable fix scaling for apps
cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"EnablePerProcessSystemDPI`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
