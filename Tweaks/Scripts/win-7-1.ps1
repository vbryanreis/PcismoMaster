# Ultimate (FR33THY, MIT) · Windows/7 User Account Pictures Black.ps1 (opção 1)
# Extraído pelo PcismoMaster: sem o menu, as pausas e a elevação (o app já roda como administrador).
$ProgressPreference = 'SilentlyContinue'
        # SCRIPT RUN AS ADMIN

        Write-Host "User Account Pictures:"



# backup user account pictures
if (!(Test-Path "$env:SystemDrive\ProgramData\User Account Pictures")) {
Copy-Item "$env:SystemDrive\ProgramData\Microsoft\User Account Pictures" -Destination "$env:SystemDrive\ProgramData" -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
}

# make all user account pictures black
$accountPicturesPath = "$env:SystemDrive\ProgramData\Microsoft\User Account Pictures"
$images = Get-ChildItem $accountPicturesPath -Include *.png,*.bmp -Recurse
Add-Type -AssemblyName System.Drawing
foreach ($image in $images) {
try {
$bitmap = [System.Drawing.Bitmap]::FromFile($image.FullName)
$width = $bitmap.Width
$height = $bitmap.Height
$bitmap.Dispose()
$newBitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($newBitmap)
$graphics.Clear([System.Drawing.Color]::Black)
$graphics.Dispose()
$newBitmap.Save($image.FullName)
$newBitmap.Dispose()
} catch { }
}
