# SCNexus Core - Identity & Authentication API

## 🎯 Arquitectura Desacoplada

Este Core API funciona como **proveedor de identidad centralizado** para todos los módulos del sistema SCNexus.

```
[ CORE / Identity API ]
     ↓   (JWT)
[ MÓDULOS Independientes ]
     ↓   (Frontends o servicios)
```

**Principios:**
- El Core **solo autentica** y emite JWT, nada más
- Los módulos **no conocen el Core internamente**
- Los módulos solo reciben el token y validan roles/permisos
- Sin acoplamiento, sin dependencias cruzadas

---

## 🔑 Endpoints del Core

### 1. POST `/api/auth/login`

Autentica un usuario y retorna JWT con roles y permisos.

**Request:**
```json
{
  "userName": "admin",
  "password": "Admin123!",
  "useActiveDirectory": false
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 10800,
  "user": {
    "id": "123",
    "userName": "admin",
    "email": "admin@example.com",
    "fullName": "Administrator",
    "roles": ["Admin"],
    "permissions": ["users.view", "users.create", "depositos.view", "depositos.create"]
  }
}
```

**El token JWT contiene:**
- `sub`: User ID
- `name`: Username
- `email`: Email
- `role`: Roles (puede haber múltiples)
- `permission`: Permisos (puede haber múltiples)
- `exp`: Expiración

---

### 2. GET `/api/auth/me`

Retorna información del usuario autenticado (requiere token).

**Headers:**
```
Authorization: Bearer <token>
```

**Response:**
```json
{
  "user": {
    "id": "123",
    "userName": "admin",
    "email": "admin@example.com",
    "fullName": "Administrator",
    "roles": ["Admin"],
    "permissions": ["users.view", "users.create", "depositos.view"]
  }
}
```

**Uso:** Los módulos pueden llamar este endpoint para obtener datos actualizados del usuario.

---

### 3. POST `/api/auth/validate`

Valida si un token es válido y retorna información del usuario (opcional).

**Request:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Response (válido):**
```json
{
  "isValid": true,
  "message": "Token is valid",
  "user": {
    "id": "123",
    "userName": "admin",
    "email": "admin@example.com",
    "fullName": "Administrator",
    "roles": ["Admin"],
    "permissions": ["users.view", "users.create"]
  }
}
```

**Response (inválido):**
```json
{
  "isValid": false,
  "message": "Token has expired",
  "user": null
}
```

**Uso:** Los módulos pueden llamar este endpoint si quieren validar el token en el servidor (aunque lo ideal es validarlo localmente con la misma secret key).

---

## 🧩 Cómo los Módulos se Conectan

### Paso A: Login en el Core

El frontend siempre inicia sesión llamando al Core:

```javascript
// Frontend
const response = await fetch('https://core-api.example.com/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    userName: 'admin',
    password: 'Admin123!',
    useActiveDirectory: false
  })
});

const data = await response.json();
localStorage.setItem('token', data.token);
localStorage.setItem('user', JSON.stringify(data.user));
```

### Paso B: Llamadas a Módulos

Cada request a un módulo debe enviar el token:

```javascript
// Frontend llamando a módulo "Depósitos"
const response = await fetch('https://depositos-api.example.com/api/depositos', {
  method: 'GET',
  headers: {
    'Authorization': `Bearer ${localStorage.getItem('token')}`,
    'Content-Type': 'application/json'
  }
});
```

### Paso C: Validación en el Módulo

El módulo valida el JWT **internamente** sin llamar al Core:

#### Opción 1: Validación en .NET (recomendada)

```csharp
// En Program.cs del módulo
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "https://core-api.example.com",
            ValidAudience = "https://core-api.example.com",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("LA_MISMA_SECRET_KEY_DEL_CORE")
            )
        };
    });

// En el Controller
[Authorize(Policy = "RequireDepositosView")]
public class DepositosController : ControllerBase
{
    [HttpGet]
    public IActionResult GetDepositos()
    {
        // El token ya fue validado por el middleware
        var permissions = User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();
        
        if (!permissions.Contains("depositos.view"))
            return Forbid();
        
        // Lógica del endpoint...
    }
}
```

#### Opción 2: Validación en Node.js

```javascript
const jwt = require('jsonwebtoken');

function validateToken(req, res, next) {
  const token = req.headers.authorization?.split(' ')[1];
  
  if (!token) {
    return res.status(401).json({ message: 'No token provided' });
  }
  
  try {
    const decoded = jwt.verify(token, 'LA_MISMA_SECRET_KEY_DEL_CORE');
    req.user = decoded;
    next();
  } catch (err) {
    return res.status(401).json({ message: 'Invalid token' });
  }
}

app.get('/api/depositos', validateToken, (req, res) => {
  const permissions = req.user.permission || [];
  
  if (!permissions.includes('depositos.view')) {
    return res.status(403).json({ message: 'Insufficient permissions' });
  }
  
  // Lógica del endpoint...
});
```

---

## 🔐 Roles y Permisos

### Gestión de Permisos (Solo Admin del Core)

Los permisos se crean y asignan **solo en el Core**.

**Crear un permiso:**
```http
POST /api/permissions
Authorization: Bearer <admin-token>

{
  "key": "depositos.view",
  "description": "Ver depósitos",
  "module": "depositos"
}
```

**Asignar permiso a un rol:**
```http
POST /api/permissions/assign
Authorization: Bearer <admin-token>

{
  "roleName": "Contabilidad",
  "permissionId": 1
}
```

**Listar permisos de un rol:**
```http
GET /api/permissions/role/Contabilidad
Authorization: Bearer <admin-token>
```

### Los Módulos NO Gestionan Permisos

Los módulos **solo leen** los permisos desde el token JWT:

```csharp
var permissions = User.Claims
    .Where(c => c.Type == "permission")
    .Select(c => c.Value)
    .ToList();

if (permissions.Contains("depositos.create"))
{
    // Usuario puede crear depósitos
}
```

---

## 🚀 Flujo Completo

```
1. Frontend → POST /api/auth/login (Core)
   → Obtiene JWT con roles y permisos

2. Frontend → guarda token en localStorage

3. Frontend → GET /api/depositos (Módulo Depósitos)
   → Headers: Authorization: Bearer <token>

4. Módulo Depósitos:
   → Valida JWT internamente (misma secret key)
   → Lee roles/permisos del token
   → Verifica si tiene "depositos.view"
   → Retorna datos
```

**No hay llamadas al Core en tiempo real**, todo está en el token.

---

## ⚙️ Configuración

### Core API (appsettings.json)

```json
{
  "Jwt": {
    "Key": "TU_SECRET_KEY_SEGURA_DE_AL_MENOS_32_CARACTERES",
    "Issuer": "https://core-api.example.com",
    "Audience": "https://core-api.example.com"
  }
}
```

### Módulo (appsettings.json)

```json
{
  "Authentication": {
    "CoreApiUrl": "https://core-api.example.com",
    "JwtKey": "LA_MISMA_SECRET_KEY_DEL_CORE",
    "JwtIssuer": "https://core-api.example.com",
    "JwtAudience": "https://core-api.example.com"
  }
}
```

**IMPORTANTE:** La `JwtKey` debe ser **idéntica** en el Core y todos los módulos.

---

## 📦 Estructura de un Módulo

Cada módulo es **completamente independiente**:

```
modulo-depositos/
├── docker-compose.yml
├── appsettings.json
├── Controllers/
│   └── DepositosController.cs
├── Models/
│   └── Deposito.cs (NO AppUser, NO Roles)
├── Data/
│   └── DepositosDbContext.cs (su propia BD)
└── Middlewares/
    └── JwtValidationMiddleware.cs
```

**No tiene:**
- ❌ Tablas de usuarios
- ❌ Tablas de roles
- ❌ Tablas de permisos
- ❌ Referencias al Core

**Solo tiene:**
- ✅ Validación JWT con la misma secret key
- ✅ Lectura de claims del token
- ✅ Su propia lógica de negocio

---

## ✅ Ventajas de Esta Arquitectura

- ✔ Cambiar un módulo sin tocar el Core
- ✔ Agregar nuevos módulos sin modificar nada
- ✔ Compartir autenticación para todos
- ✔ Cada módulo puede ir en su propio servidor o docker
- ✔ Muy simple de mantener
- ✔ Sin acoplamiento entre módulos
- ✔ Escalabilidad horizontal (múltiples instancias de cada módulo)

---

## 🔧 Migración de Base de Datos

Para agregar las tablas de permisos:

```bash
dotnet ef migrations add AddPermissionsSystem
dotnet ef database update
```

---

## 📝 Ejemplo Práctico

### 1. Crear permisos para módulo "Depósitos"

```http
POST /api/permissions
{
  "key": "depositos.view",
  "description": "Ver depósitos",
  "module": "depositos"
}

POST /api/permissions
{
  "key": "depositos.create",
  "description": "Crear depósitos",
  "module": "depositos"
}

POST /api/permissions
{
  "key": "depositos.approve",
  "description": "Aprobar depósitos",
  "module": "depositos"
}
```

### 2. Asignar permisos a roles

```http
POST /api/permissions/assign
{
  "roleName": "Contabilidad",
  "permissionId": 1  // depositos.view
}

POST /api/permissions/assign
{
  "roleName": "Admin",
  "permissionId": 2  // depositos.create
}
```

### 3. Usuario inicia sesión

```http
POST /api/auth/login
{
  "userName": "contador1",
  "password": "Password123!"
}
```

**Respuesta:**
```json
{
  "token": "eyJhbG...",
  "expiresIn": 10800,
  "user": {
    "roles": ["Contabilidad"],
    "permissions": ["depositos.view"]
  }
}
```

### 4. Usuario accede al módulo Depósitos

El módulo valida el token y verifica que tiene `"depositos.view"` → permite acceso.

---

## 🔒 Seguridad

1. **Secret Key:** Debe ser fuerte y compartida entre Core y módulos
2. **HTTPS:** Siempre usar HTTPS en producción
3. **Expiración:** Los tokens expiran en 3 horas (configurable)
4. **Refresh Tokens:** (opcional) Implementar si necesitas sesiones más largas
5. **CORS:** Configurar correctamente para permitir solo dominios confiables

---

## 📞 Soporte

Para agregar más módulos:
1. Crea los permisos en el Core
2. Asígnalos a roles
3. El módulo lee permisos del token
4. ¡Listo!

**Sin dependencias, sin complicaciones.** 🎉
