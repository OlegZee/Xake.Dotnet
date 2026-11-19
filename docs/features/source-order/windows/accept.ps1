# The Windows acceptance of the OS-independent source order (README.md here). Run in a copy of
# fixture/ with the package under test in pkg/:
#   powershell -NoProfile -ExecutionPolicy Bypass -File accept.ps1 <fixture copy>
param([Parameter(Mandatory = $true)][string]$Dir)
Set-Location $Dir
Remove-Item -Recurse -Force locks, obj, out, .xake, globs\obj -ErrorAction SilentlyContinue
foreach ($name in 'record', 'build') {
  $out = & dotnet fsi accept.fsx -- -- $name 2>&1 | Out-String
  Write-Host "== $name exit=$LASTEXITCODE"
  ([regex]::Matches($out, '\[ERROR\][^\r\n]{0,300}') | ForEach-Object { $_.Value }) | Sort-Object -Unique | ForEach-Object { Write-Host $_ }
}
Get-FileHash locks\globs\Release.json, out\Release\net8.0\globs\globs.dll -Algorithm SHA256 | ForEach-Object { Write-Output "$($_.Hash) $($_.Path.Substring($Dir.Length))" }
