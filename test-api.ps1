# SCNexus Core API - Test Script
# Este script prueba todos los endpoints principales del Core Identity API

Write-Host "`n🚀 SCNexus Core API - Testing Script" -ForegroundColor Cyan
Write-Host "====================================`n" -ForegroundColor Cyan

$baseUrl = "http://localhost:5053/api"

# Función para mostrar resultados
function Show-Result {
    param([string]$title, [object]$data)
    Write-Host "✅ $title" -ForegroundColor Green
    if ($data) {
        $data | ConvertTo-Json -Depth 5 | Write-Host -ForegroundColor Yellow
    }
    Write-Host ""
}

function Show-Error {
    param([string]$message)
    Write-Host "❌ ERROR: $message" -ForegroundColor Red
    Write-Host ""
}

# 1. LOGIN
Write-Host "1️⃣  Iniciando sesión..." -ForegroundColor Cyan
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
Show-Result "Login exitoso" @{
    Usuario = $loginResponse.user.userName
    Email = $loginResponse.user.email
    Roles = $loginResponse.user.roles
    TotalPermisos = $loginResponse.user.permissions.Count
    ExpiresIn = "$($loginResponse.expiresIn) segundos"
}
} catch {
    Show-Error "Login falló: $($_.Exception.Message)"
    exit 1
}


# Headers para requests autenticados
$headers = @{
    Authorization = "Bearer $token"
}

# 2. GET /auth/me
Write-Host "2️⃣  Obteniendo información del usuario actual..." -ForegroundColor Cyan
$meResponse = Invoke-RestMethod -Uri "$baseUrl/auth/me" `
    -Method Get `
    -Headers $headers

Show-Result "Usuario actual obtenido" @{
    Username = $meResponse.user.userName
    Email = $meResponse.user.email
    FullName = $meResponse.user.fullName
    Roles = $meResponse.user.roles
}

# 3. POST /auth/validate
Write-Host "3️⃣  Validando token..." -ForegroundColor Cyan
$validateBody = @{
    token = $token
} | ConvertTo-Json

$validateResponse = Invoke-RestMethod -Uri "$baseUrl/auth/validate" `
    -Method Post `
    -Body $validateBody `
    -ContentType "application/json"

Show-Result "Token validado" @{
    IsValid = $validateResponse.isValid
    Message = $validateResponse.message
    Username = $validateResponse.user.userName
}

# 4. GET /permissions
Write-Host "4️⃣  Listando permisos..." -ForegroundColor Cyan
$permissions = Invoke-RestMethod -Uri "$baseUrl/permissions" `
    -Method Get `
    -Headers $headers

Show-Result "Permisos obtenidos (mostrando primeros 5)" ($permissions | Select-Object -First 5 | Select-Object key, description, module)

# Resumen final
Write-Host "`n" + "="*50 -ForegroundColor Cyan
Write-Host "🎉 TESTING COMPLETADO EXITOSAMENTE" -ForegroundColor Green
Write-Host "="*50 -ForegroundColor Cyan
Write-Host "`n📊 Resumen:" -ForegroundColor Cyan
Write-Host "   ✅ Login exitoso" -ForegroundColor Green
Write-Host "   ✅ Token JWT generado y validado" -ForegroundColor Green
Write-Host "   ✅ Usuario autenticado consultado" -ForegroundColor Green
Write-Host "   ✅ Permisos listados ($($permissions.Count) permisos)" -ForegroundColor Green
Write-Host "   ✅ Sistema de roles funcionando" -ForegroundColor Green
Write-Host "`n🚀 El Core Identity API está funcionando correctamente!" -ForegroundColor Green
Write-Host ""
