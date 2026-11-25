$ErrorActionPreference = "Stop"
$repoDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe = Join-Path $repoDir "DMC_KBM_Mod.exe"

if (-not (Test-Path $exe)) {
    throw "No se encontro $exe. Ejecuta build.ps1 primero."
}

[System.Reflection.Assembly]::LoadFrom($exe) | Out-Null

function Assert($cond, $msg) {
    if (-not $cond) { throw "ASSERT FAILED: $msg" }
    Write-Host "  [PASS] $msg" -ForegroundColor Green
}

Write-Host "=== 1. Self-Test CLI ===" -ForegroundColor Cyan
$st = Start-Process -FilePath $exe -ArgumentList "--selftest" -Wait -PassThru
Assert ($st.ExitCode -eq 0) "CLI --selftest exited with code 0"

Write-Host "=== 2. Process Detection & Profile Mapping ===" -ForegroundColor Cyan
$cfg = New-Object DmcKbmMod.ModConfig
$engine = New-Object DmcKbmMod.InputEngine($cfg)
$flags = [System.Reflection.BindingFlags]"NonPublic,Instance"
$procLookup = $engine.GetType().GetField("_processLookup", $flags).GetValue($engine)

Assert ($procLookup.ContainsKey("DevilMayCry4SpecialEdition") -and $procLookup["DevilMayCry4SpecialEdition"].Id -eq "DMC4") "DMC4SE exe detected -> DMC4"
Assert ($procLookup.ContainsKey("DevilMayCry4_DX9") -and $procLookup["DevilMayCry4_DX9"].Id -eq "DMC4") "DMC4 DX9 exe detected -> DMC4"
Assert ($procLookup.ContainsKey("DevilMayCry4_DX10") -and $procLookup["DevilMayCry4_DX10"].Id -eq "DMC4") "DMC4 DX10 exe detected -> DMC4"
Assert ($procLookup.ContainsKey("dmc1") -and $procLookup["dmc1"].Id -eq "DMC1") "dmc1.exe detected -> DMC1"
Assert ($procLookup.ContainsKey("dmc2") -and $procLookup["dmc2"].Id -eq "DMC2") "dmc2.exe detected -> DMC2"
Assert ($procLookup.ContainsKey("dmc3") -and $procLookup["dmc3"].Id -eq "DMC3") "dmc3.exe detected -> DMC3"
Assert (-not $procLookup.ContainsKey("dmcLauncher")) "dmcLauncher.exe intentionally excluded"

Write-Host "=== 3. Preset Switch & Remap Telemetry (DMC4 vs DMC3) ===" -ForegroundColor Cyan
$events = New-Object System.Collections.Generic.List[DmcKbmMod.LiveInputTelemetry]
$handler = [System.Action[DmcKbmMod.LiveInputTelemetry]]{ param($ev) $events.Add($ev) }
$engine.add_TelemetryLogged($handler)

$handleRemap = $engine.GetType().GetMethod("HandleRemappedInput", $flags)
$handlePulse = $engine.GetType().GetMethod("HandlePulseInput", $flags)

[DmcKbmMod.GameProfile]$dmc4 = $cfg.Games[0]
[DmcKbmMod.GameProfile]$dmc3 = $cfg.Games[1]
[DmcKbmMod.GameProfile]$dmc1 = $cfg.Games[2]

# DMC5 Preset checks on DMC4
$engine.SetPreset([DmcKbmMod.PresetType]::Dmc5Style, $false)
$events.Clear()
$handleRemap.Invoke($engine, [object[]]@($dmc4, [DmcKbmMod.InputCode]::MouseRight, $true, $true)) | Out-Null
$handleRemap.Invoke($engine, [object[]]@($dmc4, [DmcKbmMod.InputCode]::MouseRight, $false, $true)) | Out-Null
$handleRemap.Invoke($engine, [object[]]@($dmc4, [DmcKbmMod.InputCode]::MouseX1, $true, $true)) | Out-Null
$handleRemap.Invoke($engine, [object[]]@($dmc4, [DmcKbmMod.InputCode]::MouseX1, $false, $true)) | Out-Null
$handleRemap.Invoke($engine, [object[]]@($dmc4, [DmcKbmMod.InputCode]::Space, $true, $true)) | Out-Null
$handleRemap.Invoke($engine, [object[]]@($dmc4, [DmcKbmMod.InputCode]::LShift, $true, $true)) | Out-Null
$handleRemap.Invoke($engine, [object[]]@($dmc4, [DmcKbmMod.InputCode]::F, $true, $true)) | Out-Null
$handleRemap.Invoke($engine, [object[]]@($dmc4, [DmcKbmMod.InputCode]::MouseX2, $true, $true)) | Out-Null
$handlePulse.Invoke($engine, [object[]]@($dmc4, [DmcKbmMod.InputCode]::MouseWheelDown, $true)) | Out-Null

Assert ($events[0].TargetKey -eq "L" -and $events[0].ScanCodeHex -eq "0x26") "DMC5 preset: RMB -> Style/Buster (L / 0x26)"
Assert ($events[2].TargetKey -eq "J" -and $events[2].ScanCodeHex -eq "0x24") "DMC5 preset: Mouse4 -> Gun (J / 0x24)"
Assert ($events[4].TargetKey -eq "K" -and $events[4].ScanCodeHex -eq "0x25") "Space -> Jump (K / 0x25)"
Assert (($events[5].TargetKey -eq "Space" -or $events[5].TargetKey -eq "Espacio") -and $events[5].ScanCodeHex -eq "0x39") "LShift -> Lock-On (Space / 0x39)"
Assert ($events[6].TargetKey -eq "N" -and $events[6].ScanCodeHex -eq "0x31") "DMC4: F -> DevilTrigger (N / 0x31)"
Assert ($events[7].TargetKey -eq "P" -and $events[7].ScanCodeHex -eq "0x19") "DMC4: Mouse5 -> CameraReset (P / 0x19)"
Assert ($events[8].TargetKey -eq "E" -and $events[8].ScanCodeHex -eq "0x12") "WheelDown -> ChangeSword (E / 0x12)"

# Toggle to Classic Preset and test on DMC3
$engine.TogglePreset($false)
Assert ($cfg.ActivePreset -eq [DmcKbmMod.PresetType]::ClassicStyle) "TogglePreset switched to ClassicStyle"
$events.Clear()
$handleRemap.Invoke($engine, [object[]]@($dmc3, [DmcKbmMod.InputCode]::MouseRight, $true, $true)) | Out-Null
$handleRemap.Invoke($engine, [object[]]@($dmc3, [DmcKbmMod.InputCode]::MouseX1, $true, $true)) | Out-Null
$handleRemap.Invoke($engine, [object[]]@($dmc3, [DmcKbmMod.InputCode]::F, $true, $true)) | Out-Null
$handleRemap.Invoke($engine, [object[]]@($dmc3, [DmcKbmMod.InputCode]::MouseX2, $true, $true)) | Out-Null

Assert ($events[0].TargetKey -eq "J" -and $events[0].ScanCodeHex -eq "0x24") "Classic preset: RMB -> Gun (J / 0x24)"
Assert ($events[1].TargetKey -eq "L" -and $events[1].ScanCodeHex -eq "0x26") "Classic preset: Mouse4 -> Style/Buster (L / 0x26)"
Assert ($events[2].TargetKey -eq "F" -and $events[2].ScanCodeHex -eq "0x21") "DMC3: F -> DevilTrigger (F / 0x21)"
Assert ($events[3].TargetKey -eq "C" -and $events[3].ScanCodeHex -eq "0x2E") "DMC3: Mouse5 -> CameraReset (C / 0x2E)"

Write-Host "=== 4. Mouse Camera Engine (Deadzone, Inversion, Horizontal-Only, Release Timer) ===" -ForegroundColor Cyan
$engine.BypassForegroundCheckForTests = $true
$engine.TestModeActive = $true
$engine.TestModeProfile = $dmc4

# Deadzone check
$engine.OnRawMouseDelta(1, -1)
Assert ($engine.CameraDirX -eq 0 -and $engine.CameraDirY -eq 0) "Camera deadzone filters micro-jitter (dx=1, dy=-1)"

# Active X+Y on DMC4
$engine.OnRawMouseDelta(18, -14)
Assert ($engine.CameraDirX -eq 1 -and $engine.CameraDirY -eq -1) "DMC4 camera moves Right (+1) and Up (-1)"

# Invert X & Y
$cfg.InvertCameraX = $true
$cfg.InvertCameraY = $true
$engine.OnRawMouseDelta(18, -14)
Assert ($engine.CameraDirX -eq -1 -and $engine.CameraDirY -eq 1) "Camera InvertX & InvertY flip directions"
$cfg.InvertCameraX = $false
$cfg.InvertCameraY = $false

# Auto-release after CameraReleaseDelayMs
Start-Sleep -Milliseconds 110
$engine.UpdateTimers()
Assert ($engine.CameraDirX -eq 0 -and $engine.CameraDirY -eq 0) "Camera releases smoothly after delay"

# DMC3 HorizontalOnlyCamera
$engine.TestModeProfile = $dmc3
$engine.OnRawMouseDelta(15, 25)
Assert ($engine.CameraDirX -eq 1 -and $engine.CameraDirY -eq 0) "DMC3 HorizontalOnlyCamera ignores Y axis"
Start-Sleep -Milliseconds 110
$engine.UpdateTimers()

# DMC1 EnableMouseCamera=false
$engine.TestModeProfile = $dmc1
$engine.OnRawMouseDelta(25, 25)
Assert ($engine.CameraDirX -eq 0 -and $engine.CameraDirY -eq 0) "DMC1 camera disabled by default"

$engine.Dispose()

Write-Host "=== 4b. Bilingual Localization (EN / ES) ===" -ForegroundColor Cyan
[DmcKbmMod.Localization]::SetLanguage("en")
Assert ([DmcKbmMod.ModConfig]::GetActionLabel([DmcKbmMod.GameAction]::Melee) -eq "Sword / Melee Attack") "EN action label verified"
Assert ([DmcKbmMod.ModConfig]::GetInputLabel([DmcKbmMod.InputCode]::Space) -eq "Space") "EN input label verified"
Assert ([DmcKbmMod.InputEngine]::GetPresetDisplayName([DmcKbmMod.PresetType]::Dmc5Style) -eq "DMC5 Style") "EN preset label verified"

[DmcKbmMod.Localization]::SetLanguage("es")
Assert ([DmcKbmMod.ModConfig]::GetActionLabel([DmcKbmMod.GameAction]::Melee) -eq "Espada / Ataque Melee") "ES action label verified"
Assert ([DmcKbmMod.ModConfig]::GetInputLabel([DmcKbmMod.InputCode]::Space) -eq "Espacio") "ES input label verified"
Assert ([DmcKbmMod.InputEngine]::GetPresetDisplayName([DmcKbmMod.PresetType]::Dmc5Style) -eq "Estilo DMC5") "ES preset label verified"

Write-Host "=== 5. Real GUI Launch & Clean Exit Smoke Test ===" -ForegroundColor Cyan
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Milliseconds 1500
$proc.Refresh()
Assert (-not $proc.HasExited) "DMC_KBM_Mod.exe GUI running stably"
Assert ($proc.MainWindowHandle -ne [IntPtr]::Zero) "Main window created successfully"
Assert ($proc.MainWindowTitle -like "*DMC KBM Mod*") "Main window title verified: $($proc.MainWindowTitle)"
Assert ($proc.Responding) "GUI message loop responding"

$closed = $proc.CloseMainWindow()
$exited = $proc.WaitForExit(4000)
if (-not $exited) { $proc.Kill() }
Assert ($closed -and $exited -and $proc.ExitCode -eq 0) "GUI closed cleanly with ExitCode 0"

Write-Host "`nALL 32 TESTS PASSED!" -ForegroundColor Green
