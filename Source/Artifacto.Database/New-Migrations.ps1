param(
    [Parameter(Mandatory=$true)]
    [string]$MigrationSuffix
)

# Set the folder where your DbContext files are located
$contextsFolder = "$PSScriptRoot/DbContexts"

# Enumerate all .cs files in the folder
Get-ChildItem -Path $contextsFolder -Filter *.cs | ForEach-Object {
    # Use the file base name as the context name (assumes file name matches the class name)
    $contextName = $_.BaseName
    $migrationName = "${contextName}$MigrationSuffix"
    
    Write-Output "Creating migration '$migrationName' for context '$contextName'..."
    
    # Run the migration command (ensure to run this in the project root or set correct working directory)
    dotnet ef migrations add $migrationName --context $contextName --project "$PSScriptRoot/Artifacto.Database.csproj" -o "$PSScriptRoot/Migrations/$contextName"
}
