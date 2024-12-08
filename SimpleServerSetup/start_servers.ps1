$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

$ProjectPath1 = $ScriptDir 
$ProjectPath2 = $ScriptDir 
$ProjectPath3 = $ScriptDir

if (-Not (Test-Path $ProjectPath1)) {
    Write-Error "Project path not found: $ProjectPath1"
    exit 1
}

try {
    Start-Process "dotnet" -ArgumentList "run", "--project", $ProjectPath1, "--", "5001" -NoNewWindow -PassThru -ErrorAction Stop
    Start-Process "dotnet" -ArgumentList "run", "--project", $ProjectPath2, "--", "5002" -NoNewWindow -PassThru -ErrorAction Stop
    Start-Process "dotnet" -ArgumentList "run", "--project", $ProjectPath3, "--", "5003" -NoNewWindow -PassThru -ErrorAction Stop
}
catch {
    Write-Error "Failed to start servers: $_"
    exit 1
}