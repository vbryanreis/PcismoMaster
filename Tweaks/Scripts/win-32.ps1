# Ultimate (FR33THY, MIT) · Windows/32 Core Isolation.ps1
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
Start-Process msinfo32
Start-Process windowsdefender://coreisolation/
