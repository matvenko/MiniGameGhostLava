# Puts the ground back to the palette-driven version (V1): the reference art used
# only as a light-and-shade detail map over the CPU palette bands, rather than as
# the meadow's own colour.
#
# Restores the exact files that were live before the colour version replaced them,
# and clears the colour texture so nothing stale is left behind.
#
#   pwsh Tools/revert_meadow_v1.ps1
#
# Unity picks the change up on focus; if the board still looks wrong, right-click
# the GroundSurface component and pick Rebuild.

$root = Split-Path $PSScriptRoot -Parent
$bk   = Join-Path $root '.meadow-v1-backup'

if (-not (Test-Path $bk)) {
    Write-Error "No backup at $bk - nothing to revert to."
    exit 1
}

$moves = @(
    @{ From = "$bk\Shaders\LowPolyGround_URP.shader";   To = "$root\Assets\Shaders\LowPolyGround_URP.shader" }
    @{ From = "$bk\Materials\M_GroundLowPoly.mat";      To = "$root\Assets\Materials\M_GroundLowPoly.mat" }
    @{ From = "$bk\Textures\T_MeadowDetail.png";        To = "$root\Assets\Textures\Generated\T_MeadowDetail.png" }
    @{ From = "$bk\Textures\T_MeadowDetail.png.meta";   To = "$root\Assets\Textures\Generated\T_MeadowDetail.png.meta" }
)

foreach ($m in $moves) {
    Copy-Item $m.From $m.To -Force
    Write-Output "restored $($m.To.Replace($root + '\', ''))"
}

# The colour map is unreferenced once the V1 material is back; leaving it would
# just be an orphan in the project window.
foreach ($f in @("$root\Assets\Textures\Generated\T_MeadowGrass.png",
                 "$root\Assets\Textures\Generated\T_MeadowGrass.png.meta")) {
    if (Test-Path $f) { Remove-Item $f -Force; Write-Output "removed $($f.Replace($root + '\', ''))" }
}

Write-Output ""
Write-Output "Back on V1. Switch focus to Unity to let it reimport."
