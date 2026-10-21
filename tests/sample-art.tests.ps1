# Generates the sample art of every game into a temp folder (tools\make-sample-art.ps1 -OutDir) and checks it: the four files exist,
# sizes are right, bright games have a bright background and cover. Prints "FAIL ..." lines and one "sample-art: N passed" line.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repo = Split-Path $PSScriptRoot -Parent
$out = Join-Path ([IO.Path]::GetTempPath()) ('sample-art-test-' + [Guid]::NewGuid().ToString('N'))
$passed = 0; $failed = 0
function Check($ok, $msg) { if ($ok) { $script:passed++ } else { $script:failed++; Write-Output "FAIL sample-art: $msg" } }
function Slug([string]$s) { ($s.ToLower() -replace '[^a-z0-9]+', '-').Trim('-') }
function MeanLuminance([string]$path) {
    $bmp = New-Object System.Drawing.Bitmap $path
    try {
        $sum = 0.0; $n = 0
        for ($y = 4; $y -lt $bmp.Height; $y += [Math]::Max(1, [int]($bmp.Height / 24))) { for ($x = 4; $x -lt $bmp.Width; $x += [Math]::Max(1, [int]($bmp.Width / 24))) { $c = $bmp.GetPixel($x, $y); $sum += (0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B) / 255.0; $n++ } }
        $sum / $n
    }
    finally { $bmp.Dispose() }
}
try {
    & pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'tools\make-sample-art.ps1') -OutDir $out -Quiet | Out-Null
    $games = Get-Content (Join-Path $repo 'tools\sample-games.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($g in $games) {
        if ($g.style -eq 'none') { continue }
        $slug = Slug $g.name; if ($slug.Length -gt 40) { $slug = $slug.Substring(0, 40).Trim('-') }
        foreach ($f in 'bg.jpg', 'cover.jpg', 'icon.jpg', 'logo.png') { Check (Test-Path (Join-Path $out "$slug-$f")) "$($g.name): $slug-$f missing" }
        $bgPath = Join-Path $out "$slug-bg.jpg"
        if (Test-Path $bgPath) {
            $bg = [System.Drawing.Image]::FromFile($bgPath); Check ($bg.Width -eq 1920 -and $bg.Height -eq 1080) "$($g.name): background is $($bg.Width)x$($bg.Height)"; $bg.Dispose()
            if ($g.tone -eq 'light') { $l = MeanLuminance $bgPath; Check ($l -gt 0.55) ("{0}: background luminance {1:N2} is not bright" -f $g.name, $l) }
        }
        $cvPath = Join-Path $out "$slug-cover.jpg"
        if (Test-Path $cvPath) {
            $cv = [System.Drawing.Image]::FromFile($cvPath); Check ($cv.Width -eq 600 -and $cv.Height -eq 900) "$($g.name): cover is $($cv.Width)x$($cv.Height)"; $cv.Dispose()
            if ($g.tone -eq 'light') { $l = MeanLuminance $cvPath; Check ($l -gt 0.55) ("{0}: cover luminance {1:N2} is not bright" -f $g.name, $l) }
        }
    }
}
finally { Remove-Item -LiteralPath $out -Recurse -Force -ErrorAction SilentlyContinue }
Write-Output "sample-art: $passed passed, $failed failed"
if ($failed -gt 0) { exit 1 }
