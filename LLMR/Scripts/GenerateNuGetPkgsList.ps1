# GenerateNuGetPkgsList.ps1 (run in Solution folder with ps for NuGetPackagesList.csv)

$outputCsv = "NuGetPackages.csv"
$env:DOTNET_CLI_UI_LANGUAGE = "en"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".net SDK not installed or 'dotnet' not in path."
    exit 1
}

$solutionDir = (Get-Location).Path
Write-Output "Current Directory: $solutionDir"

Write-Output "running 'dotnet list package --include-transitive'..."
$packagesOutput = dotnet list package --include-transitive

if ($LASTEXITCODE -ne 0) {
    Write-Error "failed to retrieve NuGet packages. Ensure you're in the solution directory."
    exit 1
}

Write-Output "Raw 'dotnet list package' Output:"
$packagesOutput | ForEach-Object { Write-Output $_ }
$packages = @()

$currentProject = ""
$inTopLevelSection = $false
$inTransitiveSection = $false

foreach ($line in $packagesOutput) {
    if ($line -match "^Project\s+['`"]?(.+?)['`"]?\s*$") {
        $currentProject = $Matches[1].Trim()
        Write-Output "Detected Project: $currentProject"
        $inTopLevelSection = $false
        $inTransitiveSection = $false
        continue
    }

    if ($line -match "^\s*Top-level Package") {
        $inTopLevelSection = $true
        $inTransitiveSection = $false
        Write-Output "---- toplevel ----"
        continue
    }

    if ($line -match "^\s*Transitive Package") {
        $inTopLevelSection = $false
        $inTransitiveSection = $true
        Write-Output "---- transitive ----."
        continue
    }

    if ($line -match "^\s*-+$") {
        continue
    }

    if ($inTopLevelSection) {
        if ($line -match "^\s*>\s*(\S+)\s+([\d\.]+)\s+.*$") {
            $packageName = $Matches[1]
            $packageVersion = $Matches[2]
            Write-Output "Found Top-level Package: $packageName, Version: $packageVersion in Project: $currentProject"
            $packages += [PSCustomObject]@{
                Project     = $currentProject
                PackageName = $packageName
                Version     = $packageVersion
                Type        = "Top-level"
            }
        }
        continue
    }

    if ($inTransitiveSection) {
        if ($line -match "^\s*>\s*(\S+)\s+([\d\.]+)\s*$") {
            $packageName = $Matches[1]
            $packageVersion = $Matches[2]
            Write-Output "Found Transitive Package: $packageName, Version: $packageVersion in Project: $currentProject"
            $packages += [PSCustomObject]@{
                Project     = $currentProject
                PackageName = $packageName
                Version     = $packageVersion
                Type        = "Transitive"
            }
        }
        continue
    }
}

if ($packages.Count -eq 0) {
    Write-Warning "Not a single NuGet package was found! :("
} else {
    # Export to CSV
    $packages | Select-Object Project, PackageName, Version, Type | Export-Csv -Path $outputCsv -NoTypeInformation
    Write-Output "NuGet packages list has been exported to $outputCsv."
}
