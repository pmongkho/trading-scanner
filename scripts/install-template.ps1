$ErrorActionPreference = "Stop"

Write-Host "Installing dotnet-pgsql-angular-stack as 'pstack'..."
dotnet new install ..

Write-Host ""
Write-Host "Installed."
Write-Host "Create a project with:"
Write-Host "dotnet new pstack -n MyProject"
