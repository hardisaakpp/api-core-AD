# 🚀 Despliegue de Solo Binarios (Sin Código Fuente)

## ✅ Ventajas de Este Método

- ❌ **NO sube código fuente** (.cs) al servidor
- ✅ **Solo binarios compilados** (.dll, .json)
- ⚡ **Compila en tu PC** (más rápido si tienes buen hardware)
- 🔒 **Mayor seguridad** (servidor nunca ve tu código fuente)
- 💚 **100% Gratuito**
- 🎯 **Un solo comando** para compilar y desplegar

## 📋 Configuración Inicial (Una sola vez)

### 1. Crear archivo `.env.production` en el servidor

```bash
# Conectar al servidor
ssh usuario@172.30.159.208

# Crear archivo de configuración
nano ~/.env.production
```

Contenido:
```env
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Server=210.17.1.54;Database=SCNexusDB;User Id=desadmin;Password=***REMOVED***;TrustServerCertificate=True;
Jwt__Key=clave_super_secreta_para_jwt_12345
Jwt__Issuer=SCNexus.Core
Jwt__Audience=SCNexus.Users
```

Guardar: `Ctrl+O`, `Enter`, `Ctrl+X`

### 2. Instalar Docker en el servidor (si no está instalado)

```bash
sudo apt update
sudo apt install -y docker.io unzip
sudo systemctl start docker
sudo systemctl enable docker
sudo usermod -aG docker $USER
```

**Importante:** Cierra sesión y vuelve a conectar para que el grupo docker tenga efecto.

### 3. Configurar SSH sin contraseña (Opcional pero recomendado)

```powershell
# En tu PC Windows, generar clave SSH si no tienes
ssh-keygen -t rsa -b 4096

# Copiar clave al servidor
type $env:USERPROFILE\.ssh\id_rsa.pub | ssh usuario@172.30.159.208 "cat >> ~/.ssh/authorized_keys"
```

## 🎯 Uso Diario

### Cada vez que hagas cambios:

```powershell
# En tu PC Windows, desde la raíz del proyecto
.\build-and-deploy.ps1
```

**Eso es todo.** El script:
1. ✅ Compila el proyecto en modo Release
2. ✅ Comprime solo los binarios
3. ✅ Sube al servidor (solo ~50MB)
4. ✅ Crea imagen Docker
5. ✅ Reinicia el servicio

### Con parámetros personalizados:

```powershell
# Especificar servidor y usuario diferentes
.\build-and-deploy.ps1 -ServerIP "192.168.1.100" -ServerUser "admin"
```

## 📊 Salida del Script

```
╔════════════════════════════════════════════════╗
║   SCNexus Core - Build & Deploy (Binarios)    ║
╚════════════════════════════════════════════════╝

🔨 Compilando proyecto...
   Configuración: Release
   Target: net8.0

✅ Compilación exitosa
   Tamaño: 45.3 MB

📦 Comprimiendo binarios...
   Archivo: scnexus-core.zip (18.2 MB)

🚀 Subiendo al servidor 172.30.159.208...
✅ Archivo subido correctamente

🔧 Desplegando en el servidor...

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Desplegando SCNexus Core en el servidor
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🛑 Deteniendo contenedor anterior...
📦 Extrayendo binarios...
🔨 Construyendo imagen Docker...
🔑 Usando variables de entorno de .env.production
▶️  Iniciando contenedor...

✅ Contenedor iniciado correctamente
CONTAINER ID   IMAGE                      STATUS          PORTS
a1b2c3d4e5f6   scnexus-core-api:latest   Up 2 seconds   0.0.0.0:5053->8080/tcp

✅ Despliegue completado exitosamente

╔════════════════════════════════════════════════╗
║          ✅ DESPLIEGUE COMPLETADO ✅           ║
╚════════════════════════════════════════════════╝

🌐 Servicio disponible en:
   http://172.30.159.208:5053
   http://172.30.159.208:5053/swagger

📋 Comandos útiles:
   Ver logs:      ssh usuario@172.30.159.208 'docker logs -f scnexus-core-api'
   Reiniciar:     ssh usuario@172.30.159.208 'docker restart scnexus-core-api'
   Detener:       ssh usuario@172.30.159.208 'docker stop scnexus-core-api'

🧹 Limpiando archivos temporales...
✅ Limpieza completada
```

## 🔍 Ver Logs del Servidor

```powershell
# Ver logs en tiempo real
ssh usuario@172.30.159.208 "docker logs -f scnexus-core-api"

# Ver últimas 100 líneas
ssh usuario@172.30.159.208 "docker logs --tail=100 scnexus-core-api"

# Buscar errores
ssh usuario@172.30.159.208 "docker logs scnexus-core-api | grep -i error"
```

## 🔄 Rollback (Volver a Versión Anterior)

No hay rollback automático, pero puedes:

```powershell
# Opción 1: Hacer git checkout a un commit anterior y redesplegar
git checkout abc123  # Commit anterior
.\build-and-deploy.ps1
git checkout main    # Volver a la rama principal

# Opción 2: Tener tags en Git
git tag v1.0.0
git push --tags
# Para volver:
git checkout v1.0.0
.\build-and-deploy.ps1
```

## ❓ Troubleshooting

### Error: "No se puede conectar al servidor"

```powershell
# Verificar conexión SSH
ssh usuario@172.30.159.208

# Si falla, verificar:
# - IP correcta
# - Usuario correcto
# - Contraseña/clave SSH configurada
```

### Error: "Docker no encontrado"

```bash
# En el servidor
sudo apt update
sudo apt install -y docker.io
sudo systemctl start docker
sudo usermod -aG docker $USER
# Cerrar sesión y volver a entrar
```

### Error: ".env.production no encontrado"

El script funcionará con valores por defecto, pero es recomendado crear el archivo:

```bash
ssh usuario@172.30.159.208
nano ~/.env.production
# Pegar la configuración y guardar
```

### Verificar qué hay en el contenedor

```bash
# Ver archivos dentro del contenedor
ssh usuario@172.30.159.208 "docker exec scnexus-core-api ls -la /app"

# Buscar código fuente (NO debería encontrar nada)
ssh usuario@172.30.159.208 "docker exec scnexus-core-api find /app -name '*.cs'"
```

## 📈 Comparación de Métodos

| Aspecto | build-and-deploy.ps1 | auto-deploy.sh (Git) |
|---------|---------------------|----------------------|
| **Código fuente en servidor** | ❌ NO | ⚠️ SÍ (en carpeta Git) |
| **Velocidad de despliegue** | ⚡ Rápido (compila en tu PC) | 🐢 Lento (compila en servidor) |
| **Automatización** | Manual (1 comando) | Automática (cada 5 min) |
| **Tamaño de subida** | ~18 MB (binarios) | ~100 MB (código completo) |
| **Requiere Git en servidor** | ❌ NO | ✅ SÍ |
| **Ideal para** | Repos privados, control total | Desarrollo continuo, equipos |

## 💡 Recomendación

**Usa `build-and-deploy.ps1` si:**
- ✅ Tienes un repo privado (sin costos de CI/CD)
- ✅ Quieres máxima seguridad (sin código fuente en servidor)
- ✅ Tienes buen hardware de desarrollo
- ✅ Prefieres control manual de cuándo desplegar

**Usa `auto-deploy.sh` si:**
- ✅ Prefieres automatización total
- ✅ No te importa que el código fuente esté en el servidor
- ✅ Trabajas en equipo y quieres que todos puedan desplegar
