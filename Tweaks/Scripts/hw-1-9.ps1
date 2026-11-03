# Ultimate (FR33THY, MIT) · Hardware/1 Scaling Higher No Accel.ps1 (opção 9)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "Higher Scaling With No Acceleration`n"



# 350

# 6-11 pointer speed
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseSensitivity`" /t REG_SZ /d `"10`" /f >nul 2>&1"

# enable enhance pointer precision
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseSpeed`" /t REG_SZ /d `"1`" /f >nul 2>&1"
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseThreshold1`" /t REG_SZ /d `"6`" /f >nul 2>&1"
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"MouseThreshold2`" /t REG_SZ /d `"10`" /f >nul 2>&1"

# mouse curve 350% scaling
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"SmoothMouseXCurve`" /t REG_BINARY /d `"0000000000000000C0CC2C000000000080995900000000004066860000000000003B300000000000`" /f >nul 2>&1"
cmd /c "reg add `"HKCU\Control Panel\Mouse`" /v `"SmoothMouseYCurve`" /t REG_BINARY /d `"00000000000000000000380000000000000070000000000000A800000000000000E0000000000000`" /f >nul 2>&1"

# use custom scaling
cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"Win8DpiScaling`" /t REG_DWORD /d `"1`" /f >nul 2>&1"

# dpi scaling 350%
cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"LogPixels`" /t REG_DWORD /d `"336`" /f >nul 2>&1"

# enable fix scaling for apps
cmd /c "reg add `"HKCU\Control Panel\Desktop`" /v `"EnablePerProcessSystemDPI`" /t REG_DWORD /d `"1`" /f >nul 2>&1"
