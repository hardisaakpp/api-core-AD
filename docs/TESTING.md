# Testing del Core Identity API

## 🔐 Credenciales por Defecto

El sistema crea automáticamente un usuario admin:

- **Username:** `admin`
- **Password:** `Admin123!`
- **Role:** `Admin`
- **Permissions:** Todos

---

## 📌 Base URL

```
http://210.17.1.54:5053/api
```

---

## 1️⃣ POST `/auth/login` - Iniciar Sesión

### Request

```http
POST http://210.17.1.54:5053/api/auth/login
Content-Type: application/json

{
  "userName": "admin",
  "password": "Admin123!",
  "useActiveDirectory": false
}
```

### Response (200 OK)

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllciI6IjEyMzQ1Njc4OTAiLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1lIjoiYWRtaW4iLCJodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9lbWFpbGFkZHJlc3MiOiJhZG1pbkBzY25leHVzLmxvY2FsIiwianRpIjoiZjc3Yzk3NjUtZGY5MC00MDc2LTg0YjUtMmEzYzgyNzA2Y2I4IiwiaHR0cDovL3NjaGVtYXMubWljcm9zb2Z0LmNvbS93cy8yMDA4LzA2L2lkZW50aXR5L2NsYWltcy9yb2xlIjoiQWRtaW4iLCJwZXJtaXNzaW9uIjpbInVzZXJzLnZpZXciLCJ1c2Vycy5jcmVhdGUiLCJ1c2Vycy5lZGl0IiwidXNlcnMuZGVsZXRlIiwicm9sZXMudmlldyIsInJvbGVzLm1hbmFnZSIsInBlcm1pc3Npb25zLnZpZXciLCJwZXJtaXNzaW9ucy5tYW5hZ2UiLCJkZXBvc2l0b3MudmlldyIsImRlcG9zaXRvcy5jcmVhdGUiLCJkZXBvc2l0b3MuZWRpdCIsImRlcG9zaXRvcy5kZWxldGUiLCJkZXBvc2l0b3MuYXBwcm92ZSIsImNvbnRhYmlsaWRhZC52aWV3IiwiY29udGFiaWxpZGFkLmNyZWF0ZSIsImNvbnRhYmlsaWRhZC5jbG9zZSIsInJlcG9ydGVzLnZpZXciLCJyZXBvcnRlcy5leHBvcnQiXSwiZXhwIjoxNzMyNTA2Nzc3LCJpc3MiOiJodHRwczovL2NvcmUtYXBpLmV4YW1wbGUuY29tIiwiYXVkIjoiaHR0cHM6Ly9jb3JlLWFwaS5leGFtcGxlLmNvbSJ9.xyz...",
  "expiresIn": 10800,
  "user": {
    "id": "1234567890",
    "userName": "admin",
    "email": "admin@scnexus.local",
    "fullName": "Administrator",
    "roles": ["Admin"],
    "permissions": [
      "users.view",
      "users.create",
      "users.edit",
      "users.delete",
      "roles.view",
      "roles.manage",
      "permissions.view",
      "permissions.manage",
      "depositos.view",
      "depositos.create",
      "depositos.edit",
      "depositos.delete",
      "depositos.approve",
      "contabilidad.view",
      "contabilidad.create",
      "contabilidad.close",
      "reportes.view",
      "reportes.export"
    ]
  }
}
```

### PowerShell

```powershell
$response = Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/auth/login" -Method Post -Body (@{userName="admin"; password="Admin123!"; useActiveDirectory=$false} | ConvertTo-Json) -ContentType "application/json"
$token = $response.token
Write-Host "Token: $token"
```

---

## 2️⃣ GET `/auth/me` - Obtener Usuario Actual

### Request

```http
GET http://210.17.1.54:5053/api/auth/me
Authorization: Bearer <token>
```

### Response (200 OK)

```json
{
  "user": {
    "id": "1234567890",
    "userName": "admin",
    "email": "admin@scnexus.local",
    "fullName": "Administrator",
    "roles": ["Admin"],
    "permissions": [
      "users.view",
      "users.create",
      "depositos.view",
      "depositos.create",
      "depositos.approve"
    ]
  }
}
```

### PowerShell

```powershell
$headers = @{Authorization="Bearer $token"}
Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/auth/me" -Method Get -Headers $headers
```

---

## 3️⃣ POST `/auth/validate` - Validar Token

### Request

```http
POST http://210.17.1.54:5053/api/auth/validate
Content-Type: application/json

{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

### Response (Token Válido - 200 OK)

```json
{
  "isValid": true,
  "message": "Token is valid",
  "user": {
    "id": "1234567890",
    "userName": "admin",
    "email": "admin@scnexus.local",
    "fullName": "Administrator",
    "roles": ["Admin"],
    "permissions": ["users.view", "users.create"]
  }
}
```

### Response (Token Inválido - 200 OK)

```json
{
  "isValid": false,
  "message": "Token has expired",
  "user": null
}
```

### PowerShell

```powershell
$body = @{token=$token} | ConvertTo-Json
Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/auth/validate" -Method Post -Body $body -ContentType "application/json"
```

---

## 4️⃣ GET `/permissions` - Listar Permisos

### Request

```http
GET http://210.17.1.54:5053/api/permissions
Authorization: Bearer <admin-token>
```

### Response (200 OK)

```json
[
  {
    "id": 1,
    "key": "users.view",
    "description": "Ver usuarios",
    "module": "core",
    "createdAt": "2024-11-24T12:00:00Z"
  },
  {
    "id": 2,
    "key": "users.create",
    "description": "Crear usuarios",
    "module": "core",
    "createdAt": "2024-11-24T12:00:00Z"
  },
  {
    "id": 9,
    "key": "depositos.view",
    "description": "Ver depósitos",
    "module": "depositos",
    "createdAt": "2024-11-24T12:00:00Z"
  }
]
```

### PowerShell

```powershell
$headers = @{Authorization="Bearer $token"}
Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/permissions" -Method Get -Headers $headers
```

---

## 5️⃣ POST `/permissions` - Crear Permiso

### Request

```http
POST http://210.17.1.54:5053/api/permissions
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "key": "inventario.view",
  "description": "Ver inventario",
  "module": "inventario"
}
```

### Response (200 OK)

```json
{
  "id": 19,
  "key": "inventario.view",
  "description": "Ver inventario",
  "module": "inventario",
  "createdAt": "2024-11-24T15:30:00Z"
}
```

### PowerShell

```powershell
$headers = @{Authorization="Bearer $token"}
$body = @{key="inventario.view"; description="Ver inventario"; module="inventario"} | ConvertTo-Json
Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/permissions" -Method Post -Headers $headers -Body $body -ContentType "application/json"
```

---

## 6️⃣ POST `/permissions/assign` - Asignar Permiso a Rol

### Request

```http
POST http://210.17.1.54:5053/api/permissions/assign
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "roleName": "Contabilidad",
  "permissionId": 9
}
```

### Response (200 OK)

```json
{
  "message": "Permission assigned to role successfully."
}
```

### PowerShell

```powershell
$headers = @{Authorization="Bearer $token"}
$body = @{roleName="Contabilidad"; permissionId=9} | ConvertTo-Json
Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/permissions/assign" -Method Post -Headers $headers -Body $body -ContentType "application/json"
```

---

## 7️⃣ GET `/permissions/role/{roleName}` - Permisos de un Rol

### Request

```http
GET http://210.17.1.54:5053/api/permissions/role/Contabilidad
Authorization: Bearer <admin-token>
```

### Response (200 OK)

```json
[
  {
    "id": 9,
    "key": "depositos.view",
    "description": "Ver depósitos",
    "module": "depositos",
    "createdAt": "2024-11-24T12:00:00Z"
  },
  {
    "id": 10,
    "key": "depositos.create",
    "description": "Crear depósitos",
    "module": "depositos",
    "createdAt": "2024-11-24T12:00:00Z"
  }
]
```

### PowerShell

```powershell
$headers = @{Authorization="Bearer $token"}
Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/permissions/role/Contabilidad" -Method Get -Headers $headers
```

---

## 8️⃣ DELETE `/permissions/unassign` - Remover Permiso de Rol

### Request

```http
DELETE http://210.17.1.54:5053/api/permissions/unassign
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "roleName": "Contabilidad",
  "permissionId": 9
}
```

### Response (200 OK)

```json
{
  "message": "Permission removed from role successfully."
}
```

### PowerShell

```powershell
$headers = @{Authorization="Bearer $token"}
$body = @{roleName="Contabilidad"; permissionId=9} | ConvertTo-Json
Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/permissions/unassign" -Method Delete -Headers $headers -Body $body -ContentType "application/json"
```

---

## 🧪 Script de Testing Completo

```powershell
# 1. Login
$loginResponse = Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/auth/login" `
    -Method Post `
    -Body (@{userName="admin"; password="Admin123!"; useActiveDirectory=$false} | ConvertTo-Json) `
    -ContentType "application/json"

$token = $loginResponse.token
Write-Host "✅ Login exitoso" -ForegroundColor Green
Write-Host "Token: $token`n" -ForegroundColor Cyan

# 2. Obtener usuario actual
$headers = @{Authorization="Bearer $token"}
$meResponse = Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/auth/me" -Method Get -Headers $headers
Write-Host "✅ Usuario actual:" -ForegroundColor Green
Write-Host "   Username: $($meResponse.user.userName)" -ForegroundColor Yellow
Write-Host "   Email: $($meResponse.user.email)" -ForegroundColor Yellow
Write-Host "   Roles: $($meResponse.user.roles -join ', ')" -ForegroundColor Yellow
Write-Host "   Permisos: $($meResponse.user.permissions.Count) permisos`n" -ForegroundColor Yellow

# 3. Validar token
$validateResponse = Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/auth/validate" `
    -Method Post `
    -Body (@{token=$token} | ConvertTo-Json) `
    -ContentType "application/json"
Write-Host "✅ Token válido: $($validateResponse.isValid)" -ForegroundColor Green
Write-Host "   Mensaje: $($validateResponse.message)`n" -ForegroundColor Yellow

# 4. Listar permisos
$permissions = Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/permissions" -Method Get -Headers $headers
Write-Host "✅ Total de permisos: $($permissions.Count)" -ForegroundColor Green
$permissions | Select-Object -First 5 | Format-Table key, description, module

# 5. Crear un nuevo permiso
$newPermBody = @{
    key = "inventario.view"
    description = "Ver inventario"
    module = "inventario"
} | ConvertTo-Json

$newPerm = Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/permissions" `
    -Method Post `
    -Headers $headers `
    -Body $newPermBody `
    -ContentType "application/json"

Write-Host "✅ Permiso creado: $($newPerm.key) (ID: $($newPerm.id))`n" -ForegroundColor Green

# 6. Asignar permiso al rol Contabilidad
$assignBody = @{
    roleName = "Contabilidad"
    permissionId = $newPerm.id
} | ConvertTo-Json

$assignResult = Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/permissions/assign" `
    -Method Post `
    -Headers $headers `
    -Body $assignBody `
    -ContentType "application/json"

Write-Host "✅ $($assignResult.message)`n" -ForegroundColor Green

# 7. Listar permisos del rol Contabilidad
$rolePerms = Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/permissions/role/Contabilidad" -Method Get -Headers $headers
Write-Host "✅ Permisos del rol Contabilidad:" -ForegroundColor Green
$rolePerms | Format-Table key, description, module

Write-Host "`n🎉 Testing completado exitosamente!" -ForegroundColor Green
```

---

## 📊 Roles y Permisos Pre-configurados

### Roles

- **Admin:** Todos los permisos
- **User:** Sin permisos específicos (base)
- **Contabilidad:** `depositos.view`, `depositos.create`, `contabilidad.view`, `contabilidad.create`
- **Gerente:** `depositos.view`, `depositos.approve`, `contabilidad.view`, `contabilidad.close`, `reportes.view`, `reportes.export`

### Módulos de Ejemplo

- **core:** Usuarios, roles, permisos
- **depositos:** Gestión de depósitos
- **contabilidad:** Asientos contables
- **reportes:** Generación de reportes

---

## 🔍 Decodificar JWT (PowerShell)

```powershell
function Decode-JWT {
    param([string]$token)
    
    $parts = $token.Split('.')
    $payload = $parts[1]
    
    # Padding base64
    $padding = 4 - ($payload.Length % 4)
    if ($padding -ne 4) {
        $payload += '=' * $padding
    }
    
    $bytes = [Convert]::FromBase64String($payload.Replace('-', '+').Replace('_', '/'))
    $json = [System.Text.Encoding]::UTF8.GetString($bytes)
    
    return $json | ConvertFrom-Json
}

# Usar
$decoded = Decode-JWT -token $token
$decoded | ConvertTo-Json -Depth 10
```

---

## 🚀 Siguiente Paso: Crear un Módulo

Ver `MODULE_EXAMPLE.md` para un ejemplo completo de cómo crear un módulo independiente que consuma este Core Identity API.
