# Frontend API Integration Guide

## 📋 Overview

This document provides complete API endpoint documentation for the SCNexus Core Identity Provider. All endpoints require JWT authentication unless explicitly marked as public.

**Base URL:** `http://210.17.1.54:5053/api` (Development)  
**Production URL:** `http://210.17.1.253:5053/api`

## 🔐 Authentication Flow

All protected endpoints require a JWT token in the `Authorization` header:
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### JWT Token Structure

The token includes the following claims:
- `sub`: Username
- `role`: User roles (can be multiple)
- `permission`: User permissions (can be multiple)
- `exp`: Expiration timestamp
- `iss`: Issuer (SCNexus.Core.API)
- `aud`: Audience (SCNexus.Modules)

Example decoded token payload:
```json
{
  "sub": "admin",
  "role": ["Admin"],
  "permission": [
    "core.users.view",
    "core.users.edit",
    "core.roles.manage",
    "depositos.view",
    "depositos.edit",
    "contabilidad.view"
  ],
  "exp": 1699564800,
  "iss": "SCNexus.Core.API",
  "aud": "SCNexus.Modules"
}
```

---

## 🚀 Authentication Endpoints

### 1. Login (Local or AD)

**Endpoint:** `POST /api/auth/login`  
**Public:** Yes (No authentication required)  
**Description:** Authenticates a user with local credentials or Active Directory and returns a JWT token.

**Request Body:**
```json
{
  "username": "admin",
  "password": "Admin123!"
}
```

**Success Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userName": "admin",
    "email": "admin@sunsetcorp.local",
    "roles": ["Admin"],
    "permissions": [
      "core.users.view",
      "core.users.edit",
      "core.roles.manage",
      "depositos.view",
      "depositos.edit",
      "contabilidad.view",
      "contabilidad.edit",
      "reportes.view"
    ],
    "externalProvider": null,
    "externalId": null
  }
}
```

**Error Responses:**

**401 Unauthorized** (Invalid credentials):
```json
{
  "message": "Usuario o contraseña incorrectos"
}
```

**Axios Example:**
```javascript
import axios from 'axios';

const login = async (username, password) => {
  try {
    const response = await axios.post('http://210.17.1.54:5053/api/auth/login', {
      username,
      password
    });
    
    // Store token
    localStorage.setItem('token', response.data.token);
    
    return response.data;
  } catch (error) {
    if (error.response?.status === 401) {
      throw new Error('Credenciales inválidas');
    }
    throw error;
  }
};
```

**Fetch Example:**
```javascript
const login = async (username, password) => {
  const response = await fetch('http://210.17.1.54:5053/api/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ username, password })
  });
  
  if (!response.ok) {
    throw new Error('Login failed');
  }
  
  return await response.json();
};
```

---

### 2. Get Current User

**Endpoint:** `GET /api/auth/me`  
**Authentication:** Required  
**Description:** Returns the authenticated user's information with roles and permissions.

**Request Headers:**
```
Authorization: Bearer <your-jwt-token>
```

**Success Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userName": "admin",
  "email": "admin@sunsetcorp.local",
  "roles": ["Admin"],
  "permissions": [
    "core.users.view",
    "core.users.edit",
    "core.roles.manage",
    "depositos.view"
  ],
  "externalProvider": null,
  "externalId": null
}
```

**Error Responses:**

**401 Unauthorized** (No token or invalid token):
```json
{
  "message": "Unauthorized"
}
```

**Axios Example:**
```javascript
const getCurrentUser = async () => {
  const token = localStorage.getItem('token');
  
  const response = await axios.get('http://210.17.1.54:5053/api/auth/me', {
    headers: {
      'Authorization': `Bearer ${token}`
    }
  });
  
  return response.data;
};
```

---

### 3. Validate Token

**Endpoint:** `POST /api/auth/validate`  
**Authentication:** Required  
**Description:** Validates a JWT token and returns validation result.

**Request Headers:**
```
Authorization: Bearer <token-to-validate>
```

**Success Response (200 OK):**
```json
{
  "isValid": true,
  "username": "admin",
  "roles": ["Admin"],
  "permissions": [
    "core.users.view",
    "core.users.edit"
  ],
  "expiresAt": "2024-11-09T12:00:00Z"
}
```

**Error Response (401 Unauthorized):**
```json
{
  "isValid": false,
  "message": "Token is invalid or expired"
}
```

**Axios Example:**
```javascript
const validateToken = async (token) => {
  try {
    const response = await axios.post(
      'http://210.17.1.54:5053/api/auth/validate',
      {},
      {
        headers: {
          'Authorization': `Bearer ${token}`
        }
      }
    );
    
    return response.data.isValid;
  } catch (error) {
    return false;
  }
};
```

---

## 👥 User Management Endpoints

### 4. Get All Users

**Endpoint:** `GET /api/users`  
**Authentication:** Required  
**Permission:** `core.users.view`  
**Description:** Returns a list of all users in the system.

**Request Headers:**
```
Authorization: Bearer <your-jwt-token>
```

**Success Response (200 OK):**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userName": "admin",
    "email": "admin@sunsetcorp.local",
    "roles": ["Admin"],
    "externalProvider": null,
    "externalId": null
  },
  {
    "id": "4gb96g75-6828-5673-c4gd-3d074g77bgb7",
    "userName": "jdoe",
    "email": "jdoe@sunsetcorp.local",
    "roles": ["User", "Contabilidad"],
    "externalProvider": "ActiveDirectory",
    "externalId": "CN=John Doe,OU=Users,DC=sunsetcorp,DC=local"
  }
]
```

**Error Responses:**

**401 Unauthorized** (No token):
```json
{
  "message": "Unauthorized"
}
```

**403 Forbidden** (No permission):
```json
{
  "message": "Forbidden: You don't have permission to access this resource"
}
```

**Axios Example:**
```javascript
const getAllUsers = async () => {
  const token = localStorage.getItem('token');
  
  const response = await axios.get('http://210.17.1.54:5053/api/users', {
    headers: {
      'Authorization': `Bearer ${token}`
    }
  });
  
  return response.data;
};
```

---

### 5. Get User by ID

**Endpoint:** `GET /api/users/{id}`  
**Authentication:** Required  
**Permission:** `core.users.view`  
**Description:** Returns a specific user by ID.

**Request Headers:**
```
Authorization: Bearer <your-jwt-token>
```

**Success Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userName": "admin",
  "email": "admin@sunsetcorp.local",
  "roles": ["Admin"],
  "permissions": [
    "core.users.view",
    "core.users.edit"
  ],
  "externalProvider": null,
  "externalId": null
}
```

**Error Response (404 Not Found):**
```json
{
  "message": "User not found"
}
```

---

### 6. Create User

**Endpoint:** `POST /api/users`  
**Authentication:** Required  
**Permission:** `core.users.edit`  
**Description:** Creates a new local user.

**Request Body:**
```json
{
  "userName": "newuser",
  "email": "newuser@sunsetcorp.local",
  "password": "SecurePass123!",
  "roles": ["User"]
}
```

**Success Response (201 Created):**
```json
{
  "id": "5hc07h86-7939-6784-d5he-4e185h88chc8",
  "userName": "newuser",
  "email": "newuser@sunsetcorp.local",
  "roles": ["User"],
  "externalProvider": null,
  "externalId": null
}
```

**Error Responses:**

**400 Bad Request** (Validation error):
```json
{
  "message": "El nombre de usuario ya existe"
}
```

**Axios Example:**
```javascript
const createUser = async (userData) => {
  const token = localStorage.getItem('token');
  
  const response = await axios.post(
    'http://210.17.1.54:5053/api/users',
    userData,
    {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      }
    }
  );
  
  return response.data;
};
```

---

### 7. Update User

**Endpoint:** `PUT /api/users/{id}`  
**Authentication:** Required  
**Permission:** `core.users.edit`  
**Description:** Updates an existing user.

**Request Body:**
```json
{
  "email": "updated@sunsetcorp.local",
  "roles": ["User", "Contabilidad"]
}
```

**Success Response (200 OK):**
```json
{
  "id": "5hc07h86-7939-6784-d5he-4e185h88chc8",
  "userName": "newuser",
  "email": "updated@sunsetcorp.local",
  "roles": ["User", "Contabilidad"],
  "externalProvider": null,
  "externalId": null
}
```

---

### 8. Delete User

**Endpoint:** `DELETE /api/users/{id}`  
**Authentication:** Required  
**Permission:** `core.users.edit`  
**Description:** Deletes a user from the system.

**Success Response (204 No Content):**
```
(No body)
```

**Error Response (404 Not Found):**
```json
{
  "message": "User not found"
}
```

**Axios Example:**
```javascript
const deleteUser = async (userId) => {
  const token = localStorage.getItem('token');
  
  await axios.delete(`http://210.17.1.54:5053/api/users/${userId}`, {
    headers: {
      'Authorization': `Bearer ${token}`
    }
  });
};
```

---

### 9. Assign Role to User

**Endpoint:** `POST /api/users/{id}/roles`  
**Authentication:** Required  
**Permission:** `core.roles.manage`  
**Description:** Assigns one or more roles to a user.

**Request Body:**
```json
{
  "roles": ["Contabilidad", "Gerente"]
}
```

**Success Response (200 OK):**
```json
{
  "message": "Roles assigned successfully",
  "user": {
    "id": "5hc07h86-7939-6784-d5he-4e185h88chc8",
    "userName": "newuser",
    "roles": ["User", "Contabilidad", "Gerente"]
  }
}
```

---

### 10. Remove Role from User

**Endpoint:** `DELETE /api/users/{id}/roles/{roleName}`  
**Authentication:** Required  
**Permission:** `core.roles.manage`  
**Description:** Removes a role from a user.

**Success Response (200 OK):**
```json
{
  "message": "Role removed successfully"
}
```

---

## 🔑 Permission Management Endpoints

### 11. Get All Permissions

**Endpoint:** `GET /api/permissions`  
**Authentication:** Required  
**Permission:** `core.roles.manage` (Admin only)  
**Description:** Returns all available permissions in the system.

**Success Response (200 OK):**
```json
[
  {
    "id": "1",
    "key": "core.users.view",
    "description": "Ver usuarios del sistema",
    "module": "core",
    "createdAt": "2024-11-05T05:32:09Z"
  },
  {
    "id": "2",
    "key": "core.users.edit",
    "description": "Crear, editar y eliminar usuarios",
    "module": "core",
    "createdAt": "2024-11-05T05:32:09Z"
  },
  {
    "id": "3",
    "key": "depositos.view",
    "description": "Ver depósitos",
    "module": "depositos",
    "createdAt": "2024-11-05T05:32:09Z"
  }
]
```

**Axios Example:**
```javascript
const getAllPermissions = async () => {
  const token = localStorage.getItem('token');
  
  const response = await axios.get('http://210.17.1.54:5053/api/permissions', {
    headers: {
      'Authorization': `Bearer ${token}`
    }
  });
  
  return response.data;
};
```

---

### 12. Create Permission

**Endpoint:** `POST /api/permissions`  
**Authentication:** Required  
**Permission:** `core.roles.manage` (Admin only)  
**Description:** Creates a new permission.

**Request Body:**
```json
{
  "key": "inventario.edit",
  "description": "Crear y editar artículos del inventario",
  "module": "inventario"
}
```

**Success Response (201 Created):**
```json
{
  "id": "19",
  "key": "inventario.edit",
  "description": "Crear y editar artículos del inventario",
  "module": "inventario",
  "createdAt": "2024-11-09T10:30:00Z"
}
```

**Error Response (400 Bad Request):**
```json
{
  "message": "Permission key already exists"
}
```

---

### 13. Get Permissions by Role

**Endpoint:** `GET /api/permissions/role/{roleName}`  
**Authentication:** Required  
**Permission:** `core.roles.manage` (Admin only)  
**Description:** Returns all permissions assigned to a specific role.

**Success Response (200 OK):**
```json
{
  "roleName": "Contabilidad",
  "permissions": [
    {
      "id": "5",
      "key": "contabilidad.view",
      "description": "Ver registros contables",
      "module": "contabilidad"
    },
    {
      "id": "6",
      "key": "contabilidad.edit",
      "description": "Crear y editar registros contables",
      "module": "contabilidad"
    },
    {
      "id": "7",
      "key": "reportes.view",
      "description": "Ver reportes",
      "module": "reportes"
    }
  ]
}
```

**Axios Example:**
```javascript
const getRolePermissions = async (roleName) => {
  const token = localStorage.getItem('token');
  
  const response = await axios.get(
    `http://210.17.1.54:5053/api/permissions/role/${roleName}`,
    {
      headers: {
        'Authorization': `Bearer ${token}`
      }
    }
  );
  
  return response.data;
};
```

---

### 14. Assign Permission to Role

**Endpoint:** `POST /api/permissions/assign`  
**Authentication:** Required  
**Permission:** `core.roles.manage` (Admin only)  
**Description:** Assigns a permission to a role.

**Request Body:**
```json
{
  "roleName": "Gerente",
  "permissionKey": "inventario.edit"
}
```

**Success Response (200 OK):**
```json
{
  "message": "Permission assigned successfully",
  "roleName": "Gerente",
  "permissionKey": "inventario.edit"
}
```

**Error Response (400 Bad Request):**
```json
{
  "message": "Permission is already assigned to this role"
}
```

**Axios Example:**
```javascript
const assignPermissionToRole = async (roleName, permissionKey) => {
  const token = localStorage.getItem('token');
  
  const response = await axios.post(
    'http://210.17.1.54:5053/api/permissions/assign',
    { roleName, permissionKey },
    {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      }
    }
  );
  
  return response.data;
};
```

---

### 15. Unassign Permission from Role

**Endpoint:** `DELETE /api/permissions/unassign`  
**Authentication:** Required  
**Permission:** `core.roles.manage` (Admin only)  
**Description:** Removes a permission from a role.

**Request Body:**
```json
{
  "roleName": "Gerente",
  "permissionKey": "inventario.edit"
}
```

**Success Response (200 OK):**
```json
{
  "message": "Permission removed successfully"
}
```

---

## 🛡️ Admin Endpoints

### 16. Get All Roles

**Endpoint:** `GET /api/admin/roles`  
**Authentication:** Required  
**Permission:** `core.roles.manage` (Admin only)  
**Description:** Returns all available roles in the system.

**Success Response (200 OK):**
```json
[
  {
    "id": "1",
    "name": "Admin",
    "normalizedName": "ADMIN",
    "permissionCount": 18
  },
  {
    "id": "2",
    "name": "User",
    "normalizedName": "USER",
    "permissionCount": 2
  },
  {
    "id": "3",
    "name": "Contabilidad",
    "normalizedName": "CONTABILIDAD",
    "permissionCount": 5
  },
  {
    "id": "4",
    "name": "Gerente",
    "normalizedName": "GERENTE",
    "permissionCount": 8
  }
]
```

---

### 17. Create Role

**Endpoint:** `POST /api/admin/roles`  
**Authentication:** Required  
**Permission:** `core.roles.manage` (Admin only)  
**Description:** Creates a new role.

**Request Body:**
```json
{
  "name": "Ventas"
}
```

**Success Response (201 Created):**
```json
{
  "id": "5",
  "name": "Ventas",
  "normalizedName": "VENTAS"
}
```

---

### 18. Sync Active Directory Users

**Endpoint:** `POST /api/admin/sync-ad`  
**Authentication:** Required  
**Permission:** `core.roles.manage` (Admin only)  
**Description:** Triggers synchronization of users from Active Directory.

**Success Response (200 OK):**
```json
{
  "message": "AD sync completed successfully",
  "usersAdded": 15,
  "usersUpdated": 3,
  "errors": []
}
```

**Axios Example:**
```javascript
const syncActiveDirectory = async () => {
  const token = localStorage.getItem('token');
  
  const response = await axios.post(
    'http://210.17.1.54:5053/api/admin/sync-ad',
    {},
    {
      headers: {
        'Authorization': `Bearer ${token}`
      }
    }
  );
  
  return response.data;
};
```

---

## 📊 Audit Log Endpoints

### 19. Get Audit Logs

**Endpoint:** `GET /api/auditlogs`  
**Authentication:** Required  
**Permission:** `core.users.view` (Admin only)  
**Description:** Returns audit logs of system activities.

**Query Parameters:**
- `pageNumber` (optional): Page number for pagination (default: 1)
- `pageSize` (optional): Number of records per page (default: 50)
- `userId` (optional): Filter by user ID
- `action` (optional): Filter by action type

**Example Request:**
```
GET /api/auditlogs?pageNumber=1&pageSize=20&action=Login
```

**Success Response (200 OK):**
```json
{
  "totalRecords": 150,
  "pageNumber": 1,
  "pageSize": 20,
  "data": [
    {
      "id": "1",
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "userName": "admin",
      "action": "Login",
      "details": "Successful login from 192.168.1.100",
      "timestamp": "2024-11-09T08:30:15Z"
    },
    {
      "id": "2",
      "userId": "4gb96g75-6828-5673-c4gd-3d074g77bgb7",
      "userName": "jdoe",
      "action": "UserCreated",
      "details": "Created user: newuser",
      "timestamp": "2024-11-09T09:15:42Z"
    }
  ]
}
```

---

## 🌐 CORS Configuration

The API is configured to accept requests from the following origins:

- `http://210.17.1.54:3000` (Vue dev server)
- `http://210.17.1.54:5053` (API itself)
- `http://210.17.1.253:3000` (Production frontend)
- `http://210.17.1.253:5053` (Production API)

**Allowed Methods:** GET, POST, PUT, DELETE, OPTIONS  
**Allowed Headers:** Authorization, Content-Type, Accept  
**Credentials:** Allowed

### Frontend Axios Configuration

To handle CORS properly, configure Axios with credentials:

```javascript
// src/services/api.js
import axios from 'axios';

const api = axios.create({
  baseURL: 'http://210.17.1.54:5053/api',
  headers: {
    'Content-Type': 'application/json',
  },
  withCredentials: true
});

// Add JWT token to all requests
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Handle 401 responses globally
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Token expired or invalid - redirect to login
      localStorage.removeItem('token');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default api;
```

---

## 🎯 Permission System

### Permission Naming Convention

Permissions follow the format: `<module>.<resource>.<action>`

Examples:
- `core.users.view` - View users
- `core.users.edit` - Create/edit/delete users
- `core.roles.manage` - Manage roles and permissions
- `depositos.view` - View deposits
- `depositos.edit` - Create/edit deposits
- `contabilidad.view` - View accounting records
- `contabilidad.edit` - Create/edit accounting records
- `reportes.view` - View reports

### Available Modules

1. **core** - User, role, and permission management
2. **depositos** - Deposit management module
3. **contabilidad** - Accounting module
4. **reportes** - Reports module

### Checking Permissions in Frontend

```javascript
// In Vue component
import { useAuthStore } from '@/stores/auth';

const authStore = useAuthStore();

// Check single permission
const canViewUsers = authStore.hasPermission('core.users.view');

// Check multiple permissions (requires ALL)
const canManageUsers = authStore.hasAllPermissions([
  'core.users.view',
  'core.users.edit'
]);

// Check if user has ANY of the permissions
const canAccessAccounting = authStore.hasAnyPermission([
  'contabilidad.view',
  'contabilidad.edit'
]);
```

---

## 🔄 Default Seeded Data

On first startup, the API seeds the following data:

### Roles
1. **Admin** - Full system access (all 18 permissions)
2. **User** - Basic access (core.users.view, depositos.view)
3. **Contabilidad** - Accounting access (contabilidad.*, reportes.view)
4. **Gerente** - Manager access (view all modules + core.roles.manage)

### Default Admin User
- **Username:** `admin`
- **Password:** `Admin123!`
- **Email:** `admin@sunsetcorp.local`
- **Role:** Admin

### Permissions (18 total)
```
core.users.view
core.users.edit
core.roles.manage
depositos.view
depositos.edit
depositos.delete
contabilidad.view
contabilidad.edit
contabilidad.delete
reportes.view
reportes.edit
reportes.export
```

---

## 🚨 Error Handling

### Standard Error Response Format

```json
{
  "message": "Error description",
  "errors": {
    "fieldName": ["Error message 1", "Error message 2"]
  }
}
```

### Common HTTP Status Codes

- **200 OK** - Request succeeded
- **201 Created** - Resource created successfully
- **204 No Content** - Request succeeded with no response body
- **400 Bad Request** - Validation error or invalid request
- **401 Unauthorized** - Missing or invalid authentication token
- **403 Forbidden** - User doesn't have required permission
- **404 Not Found** - Resource not found
- **500 Internal Server Error** - Server error

### Frontend Error Handling Example

```javascript
try {
  const users = await getAllUsers();
  console.log(users);
} catch (error) {
  if (error.response) {
    // Server responded with error status
    switch (error.response.status) {
      case 401:
        console.error('Not authenticated');
        // Redirect to login
        break;
      case 403:
        console.error('No permission to access this resource');
        break;
      case 404:
        console.error('Resource not found');
        break;
      default:
        console.error('Server error:', error.response.data.message);
    }
  } else if (error.request) {
    // Request made but no response
    console.error('Network error - no response from server');
  } else {
    // Error setting up request
    console.error('Error:', error.message);
  }
}
```

---

## 📦 Complete Integration Example

### Full Authentication Flow

```javascript
// src/services/authService.js
import api from './api';

export const authService = {
  async login(username, password) {
    const response = await api.post('/auth/login', { username, password });
    
    // Store token
    localStorage.setItem('token', response.data.token);
    
    // Store user data
    localStorage.setItem('user', JSON.stringify(response.data.user));
    
    return response.data;
  },
  
  async getCurrentUser() {
    const response = await api.get('/auth/me');
    return response.data;
  },
  
  async validateToken() {
    try {
      const response = await api.post('/auth/validate');
      return response.data.isValid;
    } catch {
      return false;
    }
  },
  
  logout() {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.location.href = '/login';
  },
  
  hasPermission(permission) {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    return user.permissions?.includes(permission) || false;
  },
  
  hasAnyPermission(permissions) {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    return permissions.some(p => user.permissions?.includes(p));
  },
  
  hasAllPermissions(permissions) {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    return permissions.every(p => user.permissions?.includes(p));
  }
};
```

### Vue Router Guard

```javascript
// src/router/index.js
import { createRouter, createWebHistory } from 'vue-router';
import { authService } from '@/services/authService';

const routes = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/Login.vue'),
    meta: { public: true }
  },
  {
    path: '/users',
    name: 'Users',
    component: () => import('@/views/Users.vue'),
    meta: {
      requiresAuth: true,
      requiredPermissions: ['core.users.view']
    }
  },
  {
    path: '/users/create',
    name: 'CreateUser',
    component: () => import('@/views/CreateUser.vue'),
    meta: {
      requiresAuth: true,
      requiredPermissions: ['core.users.edit']
    }
  }
];

const router = createRouter({
  history: createWebHistory(),
  routes
});

router.beforeEach(async (to, from, next) => {
  const isPublic = to.meta.public;
  const requiresAuth = to.meta.requiresAuth;
  const token = localStorage.getItem('token');
  
  // Public route - allow access
  if (isPublic) {
    return next();
  }
  
  // Protected route - check authentication
  if (requiresAuth && !token) {
    return next('/login');
  }
  
  // Validate token
  if (token) {
    const isValid = await authService.validateToken();
    if (!isValid) {
      authService.logout();
      return next('/login');
    }
  }
  
  // Check permissions
  const requiredPermissions = to.meta.requiredPermissions;
  if (requiredPermissions && requiredPermissions.length > 0) {
    const hasPermission = authService.hasAllPermissions(requiredPermissions);
    if (!hasPermission) {
      return next('/forbidden');
    }
  }
  
  next();
});

export default router;
```

---

## 🧪 Testing Endpoints

### Using cURL

```bash
# Login
curl -X POST http://210.17.1.54:5053/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}'

# Get current user (replace TOKEN with actual token)
curl -X GET http://210.17.1.54:5053/api/auth/me \
  -H "Authorization: Bearer TOKEN"

# Get all permissions
curl -X GET http://210.17.1.54:5053/api/permissions \
  -H "Authorization: Bearer TOKEN"
```

### Using PowerShell

```powershell
# Login
$body = @{
    username = "admin"
    password = "Admin123!"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/auth/login" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"

$token = $response.token

# Get current user
$headers = @{
    Authorization = "Bearer $token"
}

$user = Invoke-RestMethod -Uri "http://210.17.1.54:5053/api/auth/me" `
    -Method Get `
    -Headers $headers

Write-Host "User: $($user.userName)"
Write-Host "Roles: $($user.roles -join ', ')"
```

---

## 📞 Support

For questions or issues with the API, contact the backend team or refer to:
- **Architecture Documentation:** `ARCHITECTURE.md`
- **Module Creation Guide:** `MODULE_EXAMPLE.md`
- **Testing Guide:** `TESTING.md`
- **Frontend Integration:** `FRONTEND_INTEGRATION.md`

---

**Last Updated:** 2024-11-09  
**API Version:** 1.0  
**Maintained By:** SCNexus Core Team
