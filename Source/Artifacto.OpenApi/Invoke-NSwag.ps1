<#
    .SYNOPSIS
        Generates Web API controllers and C# client classes and places them
        automatically in the appropriate solutions.
    .EXAMPLE
        ./Invoke-NSwag.ps1
        ./Invoke-NSwag.ps1 -IgnoreClientWarnings 8600, 1591
            This example runs the script and disables the warnings with codes 
            `8600` and `1591` around the generated C# client code.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [int[]] $IgnoreClientWarnings = @(8600)
)

$NSwagConfigPath = "$PSScriptRoot/nswag-cfg.nswag"

nswag run $NSwagConfigPath

if(-not $?)
{
    Write-Error "NSwag failed to generate the client code."
    exit 1
}

$ClientsPath = "$PSScriptRoot/../Artifacto.Client/Clients.cs"
$Clients = (Get-Content $ClientsPath -Raw)

foreach( $warning in $IgnoreClientWarnings) {
    $Clients = "#pragma warning disable $warning`n$Clients`n#pragma warning restore $warning"
}

$Clients | Out-File $ClientsPath