<#
.SYNOPSIS
    Generates N copies of each source file for ingestion volume testing.

.DESCRIPTION
    Copies each template file into the drop folder with a unique numbered suffix.
    All copies retain the original documentId prefix so the FK to dbo.Document
    is satisfied.  Duplicate data is created intentionally for throughput testing.

.PARAMETER SourceDir
    Folder containing the template JSON files (defaults to C:\ingest\processed).

.PARAMETER DropDir
    Drop folder the ingestion service polls (defaults to C:\ingest\drop).

.PARAMETER CopiesPerFile
    How many copies to create per template file.

.PARAMETER BatchSize
    How many files to drop per batch.  After each batch the script waits until
    the drop folder drains below this threshold before adding the next batch.
    Set to 0 (default) to drop all files at once with no throttling.

.EXAMPLE
    # 1 000 total files (200 copies × 5 templates), no throttling
    .\Generate-VolumeTestFiles.ps1 -CopiesPerFile 200

.EXAMPLE
    # 5 000 total files, dripped in batches of 50 so the service stays current
    .\Generate-VolumeTestFiles.ps1 -CopiesPerFile 1000 -BatchSize 50
#>
param(
    [string] $SourceDir    = "C:\ingest\processed",
    [string] $DropDir      = "C:\ingest\drop",
    [int]    $CopiesPerFile = 100,
    [int]    $BatchSize     = 0   # 0 = drop everything at once
)

# ------------------------------------------------------------------
# Resolve template files (exclude the incompatible 60MB DataRetain file)
# ------------------------------------------------------------------
$templates = Get-ChildItem $SourceDir -Filter "*.json" |
             Where-Object { $_.Name -notlike "*60MB*" }

if ($templates.Count -eq 0) {
    Write-Error "No template files found in '$SourceDir'.  Run the ingest once first so files are in processed/."
    exit 1
}

Write-Host "Templates  : $($templates.Count) files"
Write-Host "Copies each: $CopiesPerFile"
Write-Host "Total files: $($templates.Count * $CopiesPerFile)"
Write-Host "Drop folder: $DropDir"
if ($BatchSize -gt 0) {
    Write-Host "Batch size : $BatchSize (script will pause between batches)"
} else {
    Write-Host "Batch size : unlimited (all files dropped at once)"
}
Write-Host ""

# ------------------------------------------------------------------
# Build the full list of (source, destination) pairs up front
# ------------------------------------------------------------------
$plan = [System.Collections.Generic.List[hashtable]]::new()

foreach ($tpl in $templates) {
    if ($tpl.Name -match '^(\d+)_') {
        $docId = $Matches[1]
    } else {
        Write-Warning "Skipping '$($tpl.Name)' — filename does not start with a documentId."
        continue
    }

    for ($i = 1; $i -le $CopiesPerFile; $i++) {
        $newName = "${docId}_vol_{0:D6}.json" -f $i
        $plan.Add(@{ Src = $tpl.FullName; Dest = Join-Path $DropDir $newName })
    }
}

$total   = $plan.Count
$dropped = 0
$start   = Get-Date

# ------------------------------------------------------------------
# Drop files (optionally throttled)
# ------------------------------------------------------------------
foreach ($item in $plan) {
    Copy-Item -Path $item.Src -Destination $item.Dest -ErrorAction Stop
    $dropped++

    if ($BatchSize -gt 0 -and ($dropped % $BatchSize -eq 0)) {
        $elapsed = (Get-Date) - $start
        Write-Host ("[{0:hh\:mm\:ss}] Dropped {1}/{2} — waiting for drop folder to drain below {3}..." `
                    -f $elapsed, $dropped, $total, $BatchSize)

        # Wait until the drop folder has fewer than BatchSize files remaining
        do {
            Start-Sleep -Seconds 5
            $remaining = (Get-ChildItem $DropDir -Filter "*.json").Count
        } while ($remaining -ge $BatchSize)

        Write-Host ("[{0:hh\:mm\:ss}] Drop folder now has {1} files — continuing." `
                    -f ((Get-Date) - $start), $remaining)
    }
}

$elapsed = (Get-Date) - $start
Write-Host ""
Write-Host ("All {0} files dropped in {1:hh\:mm\:ss}." -f $total, $elapsed)
Write-Host "Watch progress with:  docker logs -f json-ingest"
