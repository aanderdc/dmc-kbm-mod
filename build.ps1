$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) {
    throw "No se encontró el compilador C# de Windows en $csc"
}

$outExe = Join-Path $projectDir "DMC_KBM_Mod.exe"
$sources = @(
    (Join-Path $projectDir "src\NativeInput.cs"),
    (Join-Path $projectDir "src\ConfigModel.cs"),
    (Join-Path $projectDir "src\InputEngine.cs"),
    (Join-Path $projectDir "src\OverlayToast.cs"),
    (Join-Path $projectDir "src\MainForm.cs"),
    (Join-Path $projectDir "src\Program.cs")
)

Write-Host "Compilando DMC_KBM_Mod.exe..." -ForegroundColor Cyan

& $csc `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    /out:$outExe `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    @sources

if ($LASTEXITCODE -ne 0) {
    throw "Error de compilación (código $LASTEXITCODE)."
}

Write-Host "Compilación exitosa: $outExe" -ForegroundColor Green
