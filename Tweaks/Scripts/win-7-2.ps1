# Ultimate (FR33THY, MIT) · Windows/7 User Account Pictures Black.ps1 (opção 2)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "User Account Pictures:"



# restore user account pictures
Copy-Item "$env:SystemDrive\ProgramData\User Account Pictures" -Destination "$env:SystemDrive\ProgramData\Microsoft" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
