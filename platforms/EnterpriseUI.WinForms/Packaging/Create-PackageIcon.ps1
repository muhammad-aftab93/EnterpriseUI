param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'package-icon.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$bitmap = [System.Drawing.Bitmap]::new(128, 128)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$background = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#202628'))
$header = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#20B9AD'))
$cell = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F3F6F5'))
$selected = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F0B429'))

try {
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.FillRectangle($background, 4, 4, 120, 120)
    $graphics.FillRectangle($header, 24, 24, 80, 18)

    for ($rowIndex = 0; $rowIndex -lt 3; $rowIndex++) {
        for ($columnIndex = 0; $columnIndex -lt 3; $columnIndex++) {
            $brush = $cell
            if ($rowIndex -eq 1 -and $columnIndex -eq 1) {
                $brush = $selected
            }
            $graphics.FillRectangle($brush, (24 + $columnIndex * 28), (48 + $rowIndex * 20), 24, 14)
        }
    }

    $bitmap.Save([System.IO.Path]::GetFullPath($OutputPath), [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $selected.Dispose()
    $cell.Dispose()
    $header.Dispose()
    $background.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Output "Created 128 x 128 package icon: $OutputPath"