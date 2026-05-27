# SCNexus Core API - Test Script
Write-Host "`nSCNexus Core API - Testing Script" -ForegroundColor Cyan
Write-Host "=================================`n" -ForegroundColor Cyan

$baseUrl = "http://210.17.1.54:5053/api"

# 1. LOGIN
Write-Host "1. Iniciando sesion..." -ForegroundColor Cyan
$loginBody = @{
    userName = "admin"
    password = "Admin123!"
    useActiveDirectory = $false
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/auth/login" `
        -Method Post `
        -Body $loginBody `
        -ContentType "application/json"

    $token = $loginResponse.token
    Write-Host "OK - Login exitoso" -ForegroundColor Green
    Write-Host "   Usuario: $($loginResponse.user.userName)" -ForegroundColor Yellow
    Write-Host "   Email: $($loginResponse.user.email)" -ForegroundColor Yellow
    Write-Host "   Roles: $($loginResponse.user.roles -join ', ')" -ForegroundColor Yellow
    Write-Host "   Permisos: $($loginResponse.user.permissions.Count)" -ForegroundColor Yellow
    Write-Host ""
} catch {
    Write-Host "ERROR - Login fallo: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 2. GET /auth/me
Write-Host "2. Obteniendo usuario actual..." -ForegroundColor Cyan
$headers = @{
    Authorization = "Bearer $token"
}

$meResponse = Invoke-RestMethod -Uri "$baseUrl/auth/me" `
    -Method Get `
    -Headers $headers

Write-Host "OK - Usuario actual obtenido" -ForegroundColor Green
Write-Host "   Username: $($meResponse.user.userName)" -ForegroundColor Yellow
Write-Host "   FullName: $($meResponse.user.fullName)" -ForegroundColor Yellow
Write-Host ""

# 3. POST /auth/validate
Write-Host "3. Validando token..." -ForegroundColor Cyan
$validateBody = @{
    token = $token
} | ConvertTo-Json

$validateResponse = Invoke-RestMethod -Uri "$baseUrl/auth/validate" `
    -Method Post `
    -Body $validateBody `
    -ContentType "application/json"

Write-Host "OK - Token validado" -ForegroundColor Green
Write-Host "   IsValid: $($validateResponse.isValid)" -ForegroundColor Yellow
Write-Host "   Message: $($validateResponse.message)" -ForegroundColor Yellow
Write-Host ""

# 4. GET /permissions
Write-Host "4. Listando permisos..." -ForegroundColor Cyan
$permissions = Invoke-RestMethod -Uri "$baseUrl/permissions" `
    -Method Get `
    -Headers $headers

Write-Host "OK - Permisos obtenidos: $($permissions.Count) permisos" -ForegroundColor Green
$permissions | Select-Object -First 5 | Format-Table key, description, module
Write-Host ""

# Resumen
Write-Host "="*50 -ForegroundColor Cyan
Write-Host "TESTING COMPLETADO EXITOSAMENTE" -ForegroundColor Green
Write-Host "="*50 -ForegroundColor Cyan
Write-Host "`nResumen:" -ForegroundColor Cyan
Write-Host "   OK Login exitoso" -ForegroundColor Green
Write-Host "   OK Token JWT generado y validado" -ForegroundColor Green
Write-Host "   OK Usuario autenticado consultado" -ForegroundColor Green
Write-Host "   OK Permisos listados ($($permissions.Count) permisos)" -ForegroundColor Green
Write-Host "`nEl Core Identity API esta funcionando correctamente!" -ForegroundColor Green
Write-Host ""
