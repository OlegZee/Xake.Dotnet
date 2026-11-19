# The Windows acceptance of the legacy-project overlay (README.md here). Run in a copy of fixture/
# with the package under test in pkg/ and docs/legacy-projects.md beside accept.fsx:
#   powershell -NoProfile -ExecutionPolicy Bypass -File accept.ps1 <fixture copy>
param([Parameter(Mandatory = $true)][string]$Dir)
Set-Location $Dir
Remove-Item -Recurse -Force locks, obj, out, build, .xake, legacy\obj, legacy\bin, legacy\xake.project.json -ErrorAction SilentlyContinue
$installed = Test-Path 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\mscorlib.dll'
Write-Output "4.7.2 targeting pack installed: $installed; SDK $(dotnet --version)"
function Step($name) {
  $out = & dotnet fsi accept.fsx -- -- $name 2>&1 | Out-String
  $ok = $LASTEXITCODE -eq 0
  Write-Host "== $name exit=$LASTEXITCODE"
  ([regex]::Matches($out, '\[ERROR\][^\r\n]{0,400}') | ForEach-Object { $_.Value }) | Sort-Object -Unique | ForEach-Object { Write-Host $_ }
  return @{ Ok = $ok; Out = $out }
}
$bare = Step 'bare'
$guard = $bare.Out.Contains('legacy (non-SDK) project; import needs TargetFramework and PackageReference resolution') -and $bare.Out.Contains('(missing: A framework, B package resolution, D reference assemblies)')
Remove-Item -Recurse -Force locks, obj, legacy\obj, legacy\xake.project.json -ErrorAction SilentlyContinue
$record = Step 'record'
$lock = if (Test-Path locks\legacy\Release.json) { Get-Content locks\legacy\Release.json -Raw } else { '' }
$refasm = $lock.Contains('pkg:microsoft.netframework.referenceassemblies.net472/1.0.3/build/.NETFramework/v4.7.2/mscorlib.dll')
$package = $lock.Contains('pkg:newtonsoft.json/13.0.3/')
$baml = $lock.Contains('Legacy.g.resources')
$programFiles = $lock.Contains('Reference Assemblies')
Write-Output "lock: pkg refasm=$refasm newtonsoft=$package g.resources=$baml Program Files=$programFiles"
$build = Step 'build'
$dll = Test-Path out\Release\net472\legacy\Legacy.dll
Write-Output "RESULT bare-guard=$guard record=$($record.Ok) lock=$($refasm -and $package -and $baml -and -not $programFiles) build=$($build.Ok -and $dll) (expected: True True True True)"
