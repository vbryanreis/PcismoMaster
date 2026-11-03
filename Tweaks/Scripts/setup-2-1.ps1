# Ultimate (FR33THY, MIT) · 3 Setup/2 Memory Compression.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN




Write-Host "Memory Compression: Off"


# disable memory compression
Disable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue | Out-Null
