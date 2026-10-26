$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$graph = @{}
$projects = @(Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.csproj)
foreach ($project in $projects) {
    $path = $project.FullName
    $graph[$path] = @()
    [xml]$xml = Get-Content -LiteralPath $path -Raw
    foreach ($reference in $xml.SelectNodes('//ProjectReference')) {
        $target = [IO.Path]::GetFullPath((Join-Path $project.DirectoryName $reference.Include))
        if (-not (Test-Path -LiteralPath $target)) { throw "Missing reference: $target" }
        $graph[$path] += $target
        $name = $project.BaseName
        $dependency = [IO.Path]::GetFileNameWithoutExtension($target)
        $isBlock = $target -match '[\\/]BuildingBlocks[\\/]'
        $isContract = $target -match '[\\/]Contracts[\\/]'
        $allowed = $false
        if ($path -match '[\\/]BuildingBlocks[\\/]') { $allowed = $isBlock }
        elseif ($path -match '[\\/]Contracts[\\/]') { $allowed = $isBlock }
        elseif ($name -eq 'DevFlow.Gateway' -and $path -match '[\\/]Gateway[\\/]') { $allowed = $isBlock }
        elseif ($name -match '^(.*)\.(Domain|Application|Infrastructure|API|Worker|MigrationRunner)$') {
            $module = $Matches[1]
            $layer = $Matches[2]
            if ($layer -eq 'Application' -and $dependency -eq 'DevFlow.AspNetCore') {
                throw "Forbidden dependency: $name -> $dependency (Application must remain independent of ASP.NET Core)"
            }
            $layers = switch ($layer) {
                Domain { @() }
                Application { @('Domain') }
                Infrastructure { @('Application', 'Domain') }
                API { @('Application', 'Infrastructure') }
                Worker { @('Application', 'Infrastructure') }
                MigrationRunner { @('Infrastructure') }
            }
            $allowed = $isBlock -or ($layer -notin @('Domain', 'MigrationRunner') -and $isContract) -or ($dependency -in @($layers | ForEach-Object { "$module.$_" }))
        }
        if (-not $allowed) { throw "Forbidden dependency: $name -> $dependency" }
    }
}
function Visit($node, $ancestors) {
    if ($node -in $ancestors) { throw "Dependency cycle: $node" }
    foreach ($child in $graph[$node]) { Visit $child (@($ancestors) + $node) }
}
foreach ($node in @($graph.Keys)) { Visit $node @() }
Write-Output "Architecture checks passed for $($projects.Count) projects."
