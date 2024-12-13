param (
    [int]$Port
)

Start-Process "dotnet" -ArgumentList "run", "--project", $ProjectPath1, "--", $Port -NoNewWindow -PassThru -ErrorAction Stop
