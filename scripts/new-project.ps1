param(
    [Parameter(Mandatory = $true)]
    [string]$Name
)

$ErrorActionPreference = "Stop"

dotnet new pstack -n $Name
Set-Location $Name

Write-Host "Restoring backend..."
dotnet restore ./dotnet-server

Write-Host "Installing Angular packages..."
npm install --prefix ./angular-client

Write-Host "Initializing git..."
git init
git add .
git commit -m "Initial project from dotnet-pgsql-angular-stack"

Write-Host ""
Write-Host "$Name is ready."
Write-Host "Next: set PostgreSQL connection string and JWT key."
