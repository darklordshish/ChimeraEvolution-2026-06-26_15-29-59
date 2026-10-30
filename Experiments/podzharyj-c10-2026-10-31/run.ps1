param([string]$Blender = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe')
$ErrorActionPreference = 'Stop'
$taskDir = $PSScriptRoot
function Invoke-BlenderTask([string]$Script, [string[]]$Arguments) {
    & $Blender -b --factory-startup --python-exit-code 1 -P (Join-Path $taskDir $Script) -- @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Blender failed: $Script ($LASTEXITCODE)" }
}
Invoke-BlenderTask 'refine_c10.py' @("$taskDir/c9.blend", "$taskDir/c10b.blend", "$taskDir/c10b-changes.json", '--chest')
Invoke-BlenderTask 'pose_guard.py' @("$taskDir/c10b.blend", "$taskDir/c12.blend", "$taskDir/c12-pose-guard.json", '--multi')
Invoke-BlenderTask 'build_c13.py' @("$taskDir/c10b.blend", "$taskDir/c12.blend", "$taskDir/c13.blend", "$taskDir/c13-composition.json")
Invoke-BlenderTask 'verify_invariants.py' @("$taskDir/c9.blend", "$taskDir/c13.blend", "$taskDir/c13-invariants.json", '--allow-psd')
Invoke-BlenderTask 'eksport_k2.py' @("$taskDir/c13.blend", "$taskDir/c13.fbx")
Invoke-BlenderTask 'stend_k2.py' @("$taskDir/c13.fbx", '--kluch', 'Поджарый', '--ves', '1', '--kadr', "$taskDir/c13", '--otchet', "$taskDir/c13-report.json")
Invoke-BlenderTask 'stend_k2.py' @("$taskDir/c13.fbx", '--kluch', 'Поджарый', '--ves', '1', '--test_pose', 'raised', '--kluchi', 'Поджарый·ПодъёмПлеч:1', '--kadr', "$taskDir/c13-raised", '--otchet', "$taskDir/c13-raised.json")
