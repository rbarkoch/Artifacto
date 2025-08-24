<#
    .SYNOPSIS
        Applied code style formatting to the solution based on the
        '.editorconfig' found in the solution.
    .NOTES
        Code styles will only be applied if they are 'info' level or higher. The
        severity must be specified in the '.editorconfig' file.
    .EXAMPLE
        ./Format-Solution.ps1
#>

[CmdletBinding()]
param()

# --exclude-diagnostics IDE0130 fixes a bug in dotnet format. See https://github.com/dotnet/format/issues/1623 .
dotnet format ./Source/Artifacto.sln style --severity info --verbosity detailed --exclude-diagnostics IDE0130
dotnet format ./Source/Artifacto.sln whitespace --verbosity detailed