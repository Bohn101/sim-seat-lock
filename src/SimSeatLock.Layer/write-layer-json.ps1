# Rewrites publish/layer JSON with an absolute library_path.
# Called from install-layer.cmd. No XR_API_LAYER_PATH.
param(
    [Parameter(Mandatory = $true)][string]$LayerDir
)

$dll = Join-Path $LayerDir "XR_APILAYER_NOVENDOR_sim_seat_lock.dll"
$jsonPath = Join-Path $LayerDir "XR_APILAYER_NOVENDOR_sim_seat_lock.json"
if (-not (Test-Path -LiteralPath $dll)) {
    Write-Error "Missing $dll"
    exit 1
}

$dllAbs = (Resolve-Path -LiteralPath $dll).Path
$dllJson = $dllAbs.Replace('\', '\\')
$text = @"
{
  "file_format_version": "1.0.0",
  "api_layer": {
    "name": "XR_APILAYER_NOVENDOR_sim_seat_lock",
    "library_path": "$dllJson",
    "api_version": "1.0",
    "implementation_version": "1",
    "description": "SimSeatLock seat-lock OpenXR layer. Identity xrLocateViews + Game.v1, then inv(T_rig) about CoR when Rig.v1 Armed.",
    "functions": {
      "xrNegotiateLoaderApiLayerInterface": "xrNegotiateLoaderApiLayerInterface"
    },
    "disable_environment": "DISABLE_XR_APILAYER_NOVENDOR_sim_seat_lock"
  }
}
"@
[System.IO.File]::WriteAllText($jsonPath, $text)
Write-Output $jsonPath
exit 0
