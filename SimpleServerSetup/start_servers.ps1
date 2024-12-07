Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "SimpleServerSetup", "--", "5001" -NoNewWindow -PassThru
Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "SimpleServerSetup", "--", "5002" -NoNewWindow -PassThru
Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "SimpleServerSetup", "--", "5003" -NoNewWindow -PassThru
