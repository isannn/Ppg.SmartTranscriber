$certPath = "$env:APPDATA\Microsoft\UserSecrets\aspnetcore-https"
$certFile = "$certPath\aspnetapp.pfx"
$password = "YourSecurePassword123!"

# Create directory if it doesn't exist
if (-not (Test-Path $certPath)) {
    New-Item -ItemType Directory -Path $certPath -Force
}

# Export the certificate
dotnet dev-certs https -ep $certFile -p $password

Write-Host "Certificate exported to $certFile"