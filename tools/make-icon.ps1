# Gera Assets/stayawake.ico com varios tamanhos, sem depender de nenhuma arte externa.
Add-Type -AssemblyName System.Drawing

$raiz = Split-Path -Parent $PSScriptRoot
$destino = Join-Path $raiz "Assets\stayawake.ico"
$tamanhos = 16, 24, 32, 48, 64, 128, 256

function New-IconBitmap([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    # Fundo arredondado com leve gradiente
    $raio = [Math]::Max(2, [int]($s * 0.24))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $raio * 2
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($s - $d, 0, $d, $d, 270, 90)
    $path.AddArc($s - $d, $s - $d, $d, $d, 0, 90)
    $path.AddArc(0, $s - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $rect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 32, 38, 56),
        [System.Drawing.Color]::FromArgb(255, 13, 15, 22),
        45.0)
    $g.FillPath($brush, $path)

    # Anel ambar
    $margem = $s * 0.22
    $lado = $s - ($margem * 2)
    $espessura = [Math]::Max(1.4, $s * 0.11)
    $pen = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::FromArgb(255, 255, 184, 77), $espessura)
    $g.DrawEllipse($pen, $margem, $margem, $lado, $lado)

    # Nucleo
    $nucleo = $s * 0.17
    $centro = ($s - $nucleo) / 2
    $bn = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 214, 143))
    $g.FillEllipse($bn, $centro, $centro, $nucleo, $nucleo)

    $g.Dispose(); $pen.Dispose(); $brush.Dispose(); $bn.Dispose(); $path.Dispose()
    return $bmp
}

$frames = @()
foreach ($t in $tamanhos) {
    $bmp = New-IconBitmap $t
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += , @{ size = $t; bytes = $ms.ToArray() }
    $ms.Dispose(); $bmp.Dispose()
}

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($out)
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$frames.Count)

$offset = 6 + (16 * $frames.Count)
foreach ($f in $frames) {
    $dim = if ($f.size -ge 256) { 0 } else { $f.size }
    $w.Write([Byte]$dim); $w.Write([Byte]$dim)
    $w.Write([Byte]0); $w.Write([Byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$f.bytes.Length)
    $w.Write([UInt32]$offset)
    $offset += $f.bytes.Length
}
foreach ($f in $frames) { $w.Write($f.bytes) }
$w.Flush()

New-Item -ItemType Directory -Force (Split-Path $destino) | Out-Null
[System.IO.File]::WriteAllBytes($destino, $out.ToArray())
$w.Dispose(); $out.Dispose()

Write-Output "Icone gerado em $destino"
