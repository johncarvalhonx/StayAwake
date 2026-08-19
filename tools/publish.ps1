# Gera os executaveis distribuiveis do StayAwake em dist/.
param(
    [switch]$SomentePortatil
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $raiz "dist"

Set-Location $raiz

# Versao portatil: roda em qualquer Windows 10/11, sem instalar runtime.
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -o (Join-Path $dist "portatil")

if (-not $SomentePortatil) {
    # Versao leve: exige o .NET 8 Desktop Runtime instalado.
    dotnet publish -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=true `
        -p:DebugType=none `
        -o (Join-Path $dist "leve")
}

Write-Output ""
Write-Output "Executaveis gerados:"
Get-ChildItem $dist -Recurse -Filter StayAwake.exe |
    ForEach-Object {
        "{0,-52} {1,8:N1} MB" -f $_.FullName.Replace($raiz + "\", ""), ($_.Length / 1MB)
    }
