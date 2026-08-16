param(
    [string]$DisplayVersion = '1.2.0.0',
    [string]$ProductVersion = '1.2.0',
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$installerWorkDirectory = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '.local\installer'))
$publishDirectory = Join-Path $repositoryRoot '.local\installer\publish'
$outputDirectory = Join-Path $repositoryRoot '.local\installer\output'
$installerProject = Join-Path $repositoryRoot 'installer\Buckettie.Installer\Buckettie.Installer.wixproj'

if (-not $NoRestore) {
    dotnet restore (Join-Path $repositoryRoot 'Buckettie.slnx') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Solution restore failed.' }
    dotnet restore $installerProject --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Installer restore failed.' }
}

foreach ($directory in @($publishDirectory, $outputDirectory)) {
    $resolvedDirectory = [IO.Path]::GetFullPath($directory)
    if (-not $resolvedDirectory.StartsWith("$installerWorkDirectory\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove a directory outside the installer work directory: $resolvedDirectory"
    }
}

if (Test-Path -LiteralPath $publishDirectory) { Remove-Item -LiteralPath $publishDirectory -Recurse -Force }
if (Test-Path -LiteralPath $outputDirectory) { Remove-Item -LiteralPath $outputDirectory -Recurse -Force }
New-Item -ItemType Directory -Path $publishDirectory, $outputDirectory -Force | Out-Null

$projects = @(
    'src\Buckettie.Cli\Buckettie.Cli.csproj',
    'src\Buckettie.Server\Buckettie.Server.csproj',
    'src\Buckettie.AskPass\Buckettie.AskPass.csproj'
)
foreach ($project in $projects) {
    dotnet publish (Join-Path $repositoryRoot $project) -c Release -r $RuntimeIdentifier --self-contained true -o $publishDirectory --nologo --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $project" }
}

dotnet build $installerProject -c Release --nologo --no-restore -p:DisplayVersion=$DisplayVersion -p:ProductVersion=$ProductVersion -p:PublishDir=$publishDirectory -p:OutputPath=$outputDirectory
if ($LASTEXITCODE -ne 0) { throw 'MSI build failed.' }

$msiPath = Join-Path $outputDirectory "Buckettie-$DisplayVersion-win-x64.msi"
if (-not (Test-Path -LiteralPath $msiPath)) { throw "MSI was not created: $msiPath" }
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $msiPath
$hashLine = "$($hash.Hash)  $([IO.Path]::GetFileName($msiPath))"
[IO.File]::WriteAllText("$msiPath.sha256", "$hashLine`r`n", [Text.Encoding]::ASCII)
Write-Output $msiPath
Write-Output $hashLine
