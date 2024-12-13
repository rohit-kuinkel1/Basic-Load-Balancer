param (
    [int]$Port = 5001
)
$ProjectPath = "D:\git\Basic-Load-Balancer\SimpleServerSetup"

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project path does not exist: $ProjectPath"
    exit 1
}

Start-Process "powershell.exe" -ArgumentList "dotnet run --project ""$ProjectPath"" -- $Port" -Wait