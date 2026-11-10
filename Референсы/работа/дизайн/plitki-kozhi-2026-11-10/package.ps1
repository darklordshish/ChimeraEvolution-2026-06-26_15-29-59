param([Parameter(Mandatory=$true)][string]$Manifest)
# Только упаковка результатов ImageGen: разделение панелей, размер, контактные листы.
Add-Type -AssemblyName System.Drawing
$root = $PSScriptRoot
$items = Get-Content -LiteralPath $Manifest -Encoding UTF8 | ConvertFrom-Json
foreach ($item in $items) {
    $source = [System.Drawing.Bitmap]::FromFile($item.final)
    $half = [int]($source.Width / 2)
    foreach ($entry in @(@('cvet',0), @('rel',$half))) {
        $tile = [System.Drawing.Bitmap]::new(1024,1024,[System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $g = [System.Drawing.Graphics]::FromImage($tile)
        $g.Clear([System.Drawing.Color]::White)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.DrawImage($source, [System.Drawing.Rectangle]::new(0,0,1024,1024), [System.Drawing.Rectangle]::new([int]$entry[1],0,$half,$source.Height), [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()
        $tile.Save((Join-Path $root ($item.name+'-'+$entry[0]+'.png')), [System.Drawing.Imaging.ImageFormat]::Png)
        $repeat = [System.Drawing.Bitmap]::new(3072,3072,[System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $rg = [System.Drawing.Graphics]::FromImage($repeat)
        $rg.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        for ($y=0; $y -lt 3; $y++) { for ($x=0; $x -lt 3; $x++) { $rg.DrawImageUnscaled($tile, $x*1024, $y*1024) } }
        $rg.Dispose()
        $suffix = if ($entry[0] -eq 'cvet') { '-3x3.png' } else { '-rel-3x3.png' }
        $repeat.Save((Join-Path $root ($item.name+$suffix)), [System.Drawing.Imaging.ImageFormat]::Png)
        $repeat.Dispose()
        $tile.Dispose()
    }
    $source.Dispose()
    Copy-Item -LiteralPath $item.final -Destination (Join-Path $root ('sources/'+$item.name+'-r2.png'))
    Copy-Item -LiteralPath $item.initial -Destination (Join-Path $root ('sources/'+$item.name+'-r1.png'))
}
