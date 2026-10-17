param(
    [string]$Source = (Join-Path $PSScriptRoot '..\assets\codex-phone-connect-icon-source.png'),
    [string]$Output = (Join-Path $PSScriptRoot '..\assets\codex-phone-connect.ico'),
    [string]$HighResolutionOutput = (Join-Path $PSScriptRoot '..\assets\codex-phone-connect-icon-512.png'),
    [string]$Preview = (Join-Path $PSScriptRoot '..\release\genie-icon-sizes-preview.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-IconFrame([System.Drawing.Image]$Artwork, [int]$Size) {
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
        $graphics.DrawImage($Artwork, [System.Drawing.Rectangle]::new(0, 0, $Size, $Size),
            0, 0, $Artwork.Width, $Artwork.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
        return $bitmap
    } catch {
        $bitmap.Dispose()
        throw
    } finally {
        $attributes.Dispose()
        $graphics.Dispose()
    }
}

foreach ($target in @($Output, $HighResolutionOutput, $Preview)) {
    [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($target)))
}

$artwork = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $Source).Path)
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = [System.Collections.Generic.List[byte[]]]::new()
$contact = [System.Drawing.Bitmap]::new($sizes.Count * 140, 164)
$contactGraphics = [System.Drawing.Graphics]::FromImage($contact)
$font = [System.Drawing.Font]::new('Segoe UI', 11)
try {
    if ($artwork.Width -ne $artwork.Height) { throw 'The icon source must be square.' }
    $contactGraphics.Clear([System.Drawing.Color]::FromArgb(238, 238, 238))
    $contactGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $frame = New-IconFrame $artwork $size
        $png = [System.IO.MemoryStream]::new()
        try {
            $frame.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($png.ToArray())
            $contactGraphics.DrawImage($frame, [System.Drawing.Rectangle]::new($index * 140 + 22, 12, 96, 96))
            $contactGraphics.DrawString("${size}px", $font, [System.Drawing.Brushes]::Black,
                [float]($index * 140 + 40), [float]128)
        } finally {
            $png.Dispose()
            $frame.Dispose()
        }
    }

    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        for ($index = 0; $index -lt $frames.Count; $index++) {
            $sizeByte = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
            $writer.Write([Byte]$sizeByte)
            $writer.Write([Byte]$sizeByte)
            $writer.Write([Byte]0)
            $writer.Write([Byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$frames[$index].Length)
            $writer.Write([UInt32]$offset)
            $offset += $frames[$index].Length
        }
        foreach ($frameBytes in $frames) { $writer.Write($frameBytes) }
        $writer.Flush()
        [System.IO.File]::WriteAllBytes([System.IO.Path]::GetFullPath($Output), $stream.ToArray())
    } finally {
        $writer.Dispose()
        $stream.Dispose()
    }

    $large = New-IconFrame $artwork 512
    try {
        $large.Save([System.IO.Path]::GetFullPath($HighResolutionOutput), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $large.Dispose()
    }
    $contact.Save([System.IO.Path]::GetFullPath($Preview), [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output "Wrote ICO frames: $($sizes -join ', ')"
    Write-Output "Wrote 512px PNG: $HighResolutionOutput"
} finally {
    $font.Dispose()
    $contactGraphics.Dispose()
    $contact.Dispose()
    $artwork.Dispose()
}
