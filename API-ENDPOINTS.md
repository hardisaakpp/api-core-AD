# 📘 SCNexus Core API - Documentación de Endpoints

**Base URL:** `http://210.17.1.53:5053/api`

**Swagger:** `http://210.17.1.53:5053/swagger`

---

## 🔐 Autenticación

### POST /auth/login
Iniciar sesión con credenciales locales o Active Directory.

**Request Body:**
```json
{
  "userName": "admin",
  "password": "Admin123!",
  "useActiveDirectory": false
}
```

**Response (200):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 10800,
  "user": {
    "id": "832a3f7a-53a7-43de-9317-93d4d8ab45e5",
    "userName": "admin",
    "email": "admin@scnexus.com",
    "fullName": "Administrator",
    "isActive": true,
    "roles": ["Admin"],
    "permissions": ["users.read", "users.write", "permissions.manage"]
  }
}
```

**Errores:**
- `400` - Datos inválidos
- `401` - Credenciales incorrectas

---

### GET /auth/me
Obtener información del usuario autenticado.

**Headers:**
```
Authorization: Bearer {token}
```

**Response (200):**
```json
{
  "id": "832a3f7a-53a7-43de-9317-93d4d8ab45e5",
  "userName": "admin",
  "email": "admin@scnexus.com",
  "fullName": "Administrator",
  "isActive": true,
  "roles": ["Admin"],
  "permissions": ["users.read", "users.write", "permissions.manage"]
}
```

---

## 👥 Usuarios

### GET /users
Listar todos los usuarios (solo Admin).

**Headers:**
```
Authorization: Bearer {token}
```

**Response (200):**
```json
[
  {
    "id": "832a3f7a-53a7-43de-9317-93d4d8ab45e5",
    "userName": "admin",
    "email": "admin@scnexus.com",
    "fullName": "Administrator",
    "isActive": true,
    "externalProvider": null,
    "externalId": null,
    "createdAt": "2025-11-05T05:32:09.000Z"
  },
  {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "userName": "jdoe@company.com",
    "email": "jdoe@company.com",
    "fullName": "John Doe",
    "isActive": true,
    "externalProvider": "LDAP",
    "externalId": "jdoe",
    "createdAt": "2025-11-25T10:15:30.000Z"
  }
]
```

---

### GET /users/{id}
Obtener un usuario por ID (solo Admin).

**Headers:**
```
Authorization: Bearer {token}
```

**Response (200):**
```json
{
  "id": "832a3f7a-53a7-43de-9317-93d4d8ab45e5",
  "userName": "admin",
  "email": "admin@scnexus.com",
  "fullName": "Administrator",
  "isActive": true,
  "externalProvider": null,
  "externalId": null,
  "createdAt": "2025-11-05T05:32:09.000Z"
}
```

**Errores:**
- `404` - Usuario no encontrado

---

### GET /users/role/{roleName}
Listar usuarios con un rol específico (solo Admin).

**Ejemplo:** `GET /users/role/Admin`

**Headers:**
```
Authorization: Bearer {token}
```

**Response (200):**
```json
[
  {
    "id": "832a3f7a-53a7-43de-9317-93d4d8ab45e5",
    "userName": "admin",
    "email": "admin@scnexus.com",
    "fullName": "Administrator",
    "isActive": true
  }
]
```

---

### GET /users/{id}/roles
Obtener roles de un usuario (solo Admin).

**Headers:**
```
Authorization: Bearer {token}
```

**Response (200):**
```json
[
  "Admin",
  "Gerente"
]
```

---

### POST /users/{id}/roles
Asignar un rol a un usuario (solo Admin).

**Headers:**
```
Authorization: Bearer {token}
```

**Request Body:**
```json
{
  "role": "Gerente"
}
```

**Response (200):**
```json
{}
```

**Errores:**
- `400` - Role requerido o error al asignar
- `404` - Usuario no encontrado

---

### DELETE /users/{id}/roles/{roleName}
Remover un rol de un usuario (solo Admin).

**Ejemplo:** `DELETE /users/832a3f7a-53a7-43de-9317-93d4d8ab45e5/roles/Gerente`

**Headers:**
```
Authorization: Bearer {token}
```

**Response (200):**
```json
{}
```

**Errores:**
- `400` - Error al remover rol
- `404` - Usuario no encontrado

---

### POST /users
Crear un nuevo usuario (solo Admin).

**Headers:**
```
Authorization: Bearer {token}
```

**Query Parameters:**
- `password` (string, required) - Contraseña del usuario
- `role` (string, required) - Rol inicial del usuario

**Request Body:**
```json
{
  "userName": "newuser",
  "email": "newuser@company.com",
  "fullName": "New User",
  "isActive": true
}
```

**Response (200):**
```json
{
  "id": "new-user-id",
  "userName": "newuser",
  "email": "newuser@company.com",
  "fullName": "New User",
  "isActive": true
}
```

---

### PUT /users/{id}
Actualizar un usuario (solo Admin).

**Headers:**
```
Authorization: Bearer {token}
```

**Request Body:**
```json
{
  "userName": "updateduser",
  "email": "updated@company.com",
  "fullName": "Updated Name",
  "isActive": false
}
```

**Response (200):**
```json
{
  "id": "832a3f7a-53a7-43de-9317-93d4d8ab45e5",
  "userName": "updateduser",
  "email": "updated@company.com",
  "fullName": "Updated Name",
  "isActive": false
}
```

---

### DELETE /users/{id}
Eliminar un usuario (sin restricción de rol).

**Headers:**
```
Authorization: Bearer {token}
```

**Response (200):**
```json
{}
```

---

### PUT /users/me
Actualizar perfil propio (cualquier usuario autenticado).

**Headers:**
```
Authorization: Bearer {token}
```

**Request Body:**
```json
{
  "userName": "myusername",
  "email": "myemail@company.com",
  "fullName": "My Full Name"
}
```

**Response (200):**
```json
{
  "id": "user-id",
  "userName": "myusername",
  "email": "myemail@company.com",
  "fullName": "My Full Name",
  "isActive": true
}
```

---

### POST /users/me/password
Cambiar contraseña propia (cualquier usuario autenticado).

**Headers:**
```
Authorization: Bearer {token}
```

**Request Body:**
```json
{
  "oldPassword": "OldPass123!",
  "newPassword": "NewPass456!"
}
```

**Response (200):**
```json
{}
```

**Errores:**
- `400` - Contraseña anterior incorrecta o nueva contraseña inválida

---

### POST /users/{id}/password
Resetear contraseña de un usuario (solo Admin).

**Headers:**
```
Authorization: Bearer {token}
```

**Request Body:**
```json
{
  "newPassword": "NewPass123!"
}
```

**Response (200):**
```json
{}
```

---

## 🔑 Permisos

### GET /permissions
Listar todos los permisos disponibles (solo Admin).

**Headers:**
```
Authorization: Bearer {token}
```

**Response (200):**
```json
[
  {
    "id": 1,
    "key": "users.read",
    "description": "Ver usuarios",
    "module": "core",
    "createdAt": "2025-11-25T02:34:36.000Z"
  },
  {
    "id": 2,
    "key": "users.write",
    "description": "Crear y editar usuarios",
    "module": "core",
    "createdAt": "2025-11-25T02:34:36.000Z"
  },
  {
    "id": 3,
    "key": "permissions.manage",
    "description": "Gestionar permisos y roles",
    "module": "core",
    "createdAt": "2025-11-25T02:34:36.000Z"
  }
]
```

---

### GET /permissions/role/{roleName}
Obtener permisos de un rol específico (solo Admin).

**Ejemplo:** `GET /permissions/role/Admin`

**Headers:**
```
Authorization: Bearer {token}
```

**Response (200):**
```json
[
  {
    "id": 1,
    "key": "users.read",
    "description": "Ver usuarios",
    "module": "core"
  },
  {
    "id": 2,
    "key": "users.write",
    "description": "Crear y editar usuarios",
    "module": "core"
  }
]
```

---

### POST /permissions/assign
Asignar un permiso a un rol (solo Admin).

**Headers:**
```
Authorization: Bearer {token}
```

**Request Body:**
```json
{
  "roleName": "Gerente",
  "permissionKey": "users.read"
}
```

**Response (200):**
```json
{
  "message": "Permiso 'users.read' asignado al rol 'Gerente'"
}
```

**Errores:**
- `400` - Datos inválidos
- `404` - Rol o permiso no encontrado

---

### POST /permissions/unassign
Remover un permiso de un rol (solo Admin).

**Headers:**
```
Authorization: Bearer {token}
```

**Request Body:**
```json
{
  "roleName": "Gerente",
  "permissionKey": "users.read"
}
```

**Response (200):**
```json
{
  "message": "Permiso 'users.read' removido del rol 'Gerente'"
}
```

---

## 📊 Logs de Auditoría

### GET /auditlogs
Listar logs de auditoría (solo Admin).

**Headers:**
```
Authorization: Bearer {token}
```

**Query Parameters (opcionales):**
- `action` (string) - Filtrar por acción (Login, CreateUser, UpdateUser, etc.)
- `userId` (string) - Filtrar por ID de usuario
- `startDate` (ISO 8601) - Fecha de inicio
- `endDate` (ISO 8601) - Fecha de fin
- `pageNumber` (int, default: 1) - Número de página
- `pageSize` (int, default: 20) - Tamaño de página

**Ejemplo:** `GET /auditlogs?action=Login&pageNumber=1&pageSize=10`

**Response (200):**
```json
{
  "items": [
    {
      "id": 1,
      "userId": "832a3f7a-53a7-43de-9317-93d4d8ab45e5",
      "userName": "admin",
      "action": "Login",
      "details": "Successful login from 172.30.159.208",
      "timestamp": "2025-12-15T10:30:00.000Z",
      "ipAddress": "172.30.159.208"
    },
    {
      "id": 2,
      "userId": "832a3f7a-53a7-43de-9317-93d4d8ab45e5",
      "userName": "admin",
      "action": "CreateUser",
      "details": "Created user 'newuser'",
      "timestamp": "2025-12-15T10:35:00.000Z",
      "ipAddress": "172.30.159.208"
    }
  ],
  "totalCount": 45,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 5
}
```

---

## 🔒 Autorización

### Roles Disponibles
- **Admin** - Acceso completo al sistema
- **User** - Usuario estándar
- **Gerente** - Permisos de gestión
- **Contabilidad** - Permisos de contabilidad

### Permisos del Sistema
| Permiso | Descripción | Módulo |
|---------|-------------|--------|
| `users.read` | Ver usuarios | core |
| `users.write` | Crear y editar usuarios | core |
| `permissions.manage` | Gestionar permisos y roles | core |
| `contabilidad.read` | Ver información contable | contabilidad |
| `contabilidad.write` | Crear y editar registros contables | contabilidad |
| `contabilidad.approve` | Aprobar transacciones | contabilidad |
| `contabilidad.reports` | Generar reportes | contabilidad |

---

## 🚀 CORS

Orígenes permitidos:
- `http://localhost:3000`
- `http://localhost:5053`
- `http://210.17.1.53:3000`
- `http://210.17.1.53:5053`

---

## 🔧 Códigos de Estado HTTP

| Código | Descripción |
|--------|-------------|
| `200` | Éxito |
| `400` | Solicitud inválida |
| `401` | No autorizado (token inválido o faltante) |
| `403` | Prohibido (sin permisos suficientes) |
| `404` | Recurso no encontrado |
| `500` | Error interno del servidor |

---

## 📝 Notas

1. **Autenticación JWT**: Todos los endpoints (excepto `/auth/login`) requieren un token JWT válido en el header `Authorization: Bearer {token}`
2. **Expiración de Tokens**: Los tokens expiran después de 3 horas (10800 segundos)
3. **Active Directory**: Usuarios de AD se identifican con `externalProvider: "LDAP"`
4. **Formato de Fechas**: ISO 8601 (ejemplo: `2025-12-15T10:30:00.000Z`)
5. **IDs de Usuario**: UUID v4 (ejemplo: `832a3f7a-53a7-43de-9317-93d4d8ab45e5`)

---

## 🧪 Ejemplos de Uso

### Login y obtener información del usuario

```javascript
// 1. Login
const loginResponse = await fetch('http://210.17.1.53:5053/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    userName: 'admin',
    password: 'Admin123!',
    useActiveDirectory: false
  })
});

const { token } = await loginResponse.json();

// 2. Obtener información del usuario
const meResponse = await fetch('http://210.17.1.53:5053/api/auth/me', {
  headers: { 'Authorization': `Bearer ${token}` }
});

const user = await meResponse.json();
console.log(user);
```

### Asignar rol a usuario

```javascript
const userId = '832a3f7a-53a7-43de-9317-93d4d8ab45e5';

const response = await fetch(`http://210.17.1.53:5053/api/users/${userId}/roles`, {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({ role: 'Gerente' })
});
```

### Asignar permiso a rol

```javascript
const response = await fetch('http://210.17.1.53:5053/api/permissions/assign', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    roleName: 'Gerente',
    permissionKey: 'contabilidad.read'
  })
});
```
