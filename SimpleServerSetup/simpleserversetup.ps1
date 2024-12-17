param (
    [string]$Action, 
    [int]$Port       
)

$ProjectPath = "D:\git\Basic-Load-Balancer\SimpleServerSetup"

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project path does not exist: $ProjectPath"
    exit 1
}

Start-Process "dotnet" -ArgumentList "run --project `"$ProjectPath`" $Action $Port" -Wait
