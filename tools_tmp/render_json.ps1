param([string]$JsonPath)

$ErrorActionPreference = 'Stop'

$wshell = New-Object -ComObject WScript.Shell
try { $wshell.AppActivate('Autodesk Civil 3D 2025') | Out-Null } catch {}
try { $wshell.SendKeys('{ESC}') } catch {}
Start-Sleep -Milliseconds 400
try { $wshell.SendKeys('{ESC}') } catch {}
Start-Sleep -Seconds 2

$app = [Runtime.InteropServices.Marshal]::GetActiveObject('AutoCAD.Application')
$doc = $app.ActiveDocument
if ($doc.Name -ne 'Lusaran As-built.dwg') {
    throw "Unexpected document: $($doc.Name) - aborting without changes"
}
Write-Output ("DOC: " + $doc.FullName)

$ms = $doc.ModelSpace
for ($e = $ms.Count - 1; $e -ge 0; $e--) { try { $ms.Item($e).Delete() } catch {} }
Write-Output ("MODEL cleaned: " + $ms.Count)

try { $doc.Linetypes.Load('DASHED', 'acad.lin') } catch {}
$doc.SetVariable('LTSCALE', 0.003)

try { $doc.Layouts.Item('ZZTEST').Delete() } catch {}

$data = Get-Content -Raw -Path $JsonPath | ConvertFrom-Json
$failCount = 0
$createdCount = 0

foreach ($sheetProp in $data.sheets.PSObject.Properties) {
    $name = $sheetProp.Name
    $entities = $sheetProp.Value.entities

    $layout = $null
    for ($i = 0; $i -lt $doc.Layouts.Count; $i++) {
        if ($doc.Layouts.Item($i).Name -eq $name) { $layout = $doc.Layouts.Item($i); break }
    }
    if ($null -eq $layout) { $layout = $doc.Layouts.Add($name) }

    $blk = $layout.Block
    for ($e = $blk.Count - 1; $e -ge 0; $e--) {
        try { $blk.Item($e).Delete() } catch {}
    }

    $curLayer = '0'

    foreach ($ent in $entities) {
        try {
            $t = [string]$ent.t
            if ($t -eq 'ltscale') {
                $doc.SetVariable('LTSCALE', [double]$ent.v)
                continue
            }
            if ($t -eq 'layerdef') {
                $ln = [string]$ent.name
                $ly = $null
                try { $ly = $doc.Layers.Add($ln) } catch { $ly = $doc.Layers.Item($ln) }
                if ($null -ne $ent.color) { try { $ly.color = [int]$ent.color } catch {} }
                if ($null -ne $ent.ltype) { try { $ly.Linetype = [string]$ent.ltype } catch {} }
                continue
            }
            if ($t -eq 'layer') {
                $ln = [string]$ent.name
                try { $doc.Layers.Item($ln) | Out-Null } catch { try { $doc.Layers.Add($ln) | Out-Null } catch {} }
                $curLayer = $ln
                continue
            }
            $obj = $null
            if ($t -eq 'line') {
                $obj = $blk.AddLine([double[]]@([double]$ent.x1, [double]$ent.y1, 0),
                                    [double[]]@([double]$ent.x2, [double]$ent.y2, 0))
            } elseif ($t -eq 'rect') {
                $flat = New-Object 'System.Collections.Generic.List[double]'
                $flat.Add([double]$ent.x1); $flat.Add([double]$ent.y1)
                $flat.Add([double]$ent.x1); $flat.Add([double]$ent.y2)
                $flat.Add([double]$ent.x2); $flat.Add([double]$ent.y2)
                $flat.Add([double]$ent.x2); $flat.Add([double]$ent.y1)
                $obj = $blk.AddLightWeightPolyline([double[]]$flat.ToArray())
                $obj.Closed = $true
            } elseif ($t -eq 'pline') {
                $flat = New-Object 'System.Collections.Generic.List[double]'
                foreach ($pt in $ent.pts) { $flat.Add([double]$pt[0]); $flat.Add([double]$pt[1]) }
                $obj = $blk.AddLightWeightPolyline([double[]]$flat.ToArray())
                if ($ent.closed) { $obj.Closed = $true }
            } elseif ($t -eq 'circle') {
                $obj = $blk.AddCircle([double[]]@([double]$ent.cx, [double]$ent.cy, 0), [double]$ent.r)
            } elseif ($t -eq 'text') {
                $obj = $blk.AddText([string]$ent.s, [double[]]@([double]$ent.x, [double]$ent.y, 0), [double]$ent.h)
                if ($null -ne $ent.rot -and [double]$ent.rot -ne 0.0) {
                    $obj.Rotation = [double]$ent.rot * [Math]::PI / 180.0
                }
            } else {
                continue
            }
            try { $obj.Layer = $curLayer } catch {}
            $createdCount++
        } catch {
            $failCount++
            if ($failCount -le 10) {
                Write-Output ("FAIL [" + $name + "] " + $_.Exception.Message)
            }
        }
    }
    Write-Output ("RENDERED " + $name + " entities=" + $blk.Count + " (cumulative failures=" + $failCount + ")")
}

$doc.Save()
Write-Output ("ALL-SHEETS-DONE created=" + $createdCount + " failures=" + $failCount)
