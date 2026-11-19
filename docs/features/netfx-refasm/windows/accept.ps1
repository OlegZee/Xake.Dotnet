# The Windows acceptance of NetFxReferenceAssemblies (README.md here). Run in a copy of fixture/
# with the package under test in pkg/:
#   powershell -NoProfile -ExecutionPolicy Bypass -File accept.ps1 <fixture copy>
param([Parameter(Mandatory = $true)][string]$Dir)
Set-Location $Dir
Remove-Item -Recurse -Force locks, locks-installed, obj, out, .xake, fx\obj, cpm\obj -ErrorAction SilentlyContinue
$installed = Test-Path 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\mscorlib.dll'
Write-Output "4.7.2 targeting pack installed: $installed"
function Step($name) {
  $out = & dotnet fsi accept.fsx -- -- $name 2>&1 | Out-String
  $ok = $LASTEXITCODE -eq 0
  Write-Host "== $name exit=$LASTEXITCODE"
  ([regex]::Matches($out, '(\[ERROR\][^\r\n]{0,300})|(warning MSB3245[^\r\n]{0,120})') | ForEach-Object { $_.Value }) | Sort-Object -Unique | ForEach-Object { Write-Host $_ }
  return $ok
}
$package = Step 'package'
foreach ($l in Get-ChildItem -Recurse locks -Filter Release.json) {
  $text = Get-Content $l.FullName -Raw
  Write-Output ("{0}: pkg refasm={1} Program Files={2}" -f $l.Directory.Name, $text.Contains('pkg:microsoft.netframework.referenceassemblies.net472/1.0.3/'), $text.Contains('Reference Assemblies'))
}
$build = Step 'build'
Get-ChildItem -Recurse out -Filter *.dll | ForEach-Object { Write-Output "built $($_.FullName.Substring($Dir.Length))" }
$inst = Step 'installed'
Write-Output "RESULT package=$package build=$build installed-fails=$(-not $inst) (expected: True True $installed)"
