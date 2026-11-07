# tools/stop-format.ps1 - Stop-hook guard: check formatting only when a C#/XAML file has uncommitted changes.
$changed = $true
try {
    $status = git status --porcelain 2>$null
    if ($LASTEXITCODE -eq 0) { $changed = [bool]($status | Select-String -Pattern '\.(cs|xaml)$' -Quiet) }
} catch { }   # no git -> check anyway
if ($changed) { dotnet format --verify-no-changes --no-restore }
