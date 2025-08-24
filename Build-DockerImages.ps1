param(
    [Parameter(Mandatory = $false)]
    [string]$ImageName = "artifacto",

    [Parameter(Mandatory = $false)]
    [string]$Tag = "latest"
)

Write-Output "Building $($ImageName):$($Tag)"
docker build `
    -t "$($ImageName):$($Tag)" `
    -f "$PSScriptRoot/Deployment/Dockerfile" `
    "$PSScriptRoot"

Write-Output "Building $($ImageName)-webapi:$($Tag)"
docker build `
    -t "$($ImageName)-webapi:$($Tag)" `
    --build-arg BUILD_CONFIGURATION=Release `
    -f "$PSScriptRoot/Source/Artifacto.WebApi/Dockerfile" `
    "$PSScriptRoot/Source"

Write-Output "Building $($ImageName)-webapp:$($Tag)"
docker build `
    -t "$($ImageName)-webapp:$($Tag)" `
    --build-arg BUILD_CONFIGURATION=Release `
    -f "$PSScriptRoot/Source/Artifacto.WebApplication/Dockerfile" `
    "$PSScriptRoot/Source"