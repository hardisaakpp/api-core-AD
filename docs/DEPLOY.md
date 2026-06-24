# Guía de Despliegue en Ubuntu

## 📋 Requisitos Previos

- Ubuntu Server 20.04 o superior
- Acceso SSH al servidor
- Docker y Docker Compose instalados

## 🎯 Resumen de Opciones

| Opción | Cuándo Usar | Cada Actualización | Código Fuente | Guía |
|--------|-------------|-------------------|---------------|------|
| **build-and-deploy.ps1** | ⭐ **Recomendado** para repos privados | `.\build-and-deploy.ps1` (1 comando) | ❌ NO va al servidor | `BINARY-DEPLOY.md` |
| **Auto-Deploy Git** | Automatización sin costos | `git push` (espera 5 min) | ⚠️ SÍ queda en servidor | `AUTO-DEPLOY.md` |
| **Git + deploy.sh** | Control manual total | `git pull && ./deploy.sh` | ⚠️ SÍ queda en el servidor | Ver abajo |
| **GitHub Actions** | Solo repos públicos | `git push` (automático) | ❌ NO va al servidor | Tiene costo en privados |

---

## 🏆 Opción Recomendada: Compilar Localmente

### Compila en tu PC y sube solo binarios (SIN código fuente)

```powershell
# Un solo comando hace todo
.\build-and-deploy.ps1
```

**Ventajas:**
- ❌ **NO sube código fuente** al servidor
- ✅ Solo binarios compilados (~18 MB vs ~100 MB)
- ⚡ Compila en tu PC (más rápido)
- 🔒 Máxima seguridad
- 💚 100% Gratuito

📖 **Ver guía completa:** `BINARY-DEPLOY.md`

## 🚀 Opción 1: Compilar Localmente (MÁS SEGURO - Sin código fuente)

**✅ Recomendado:** Compila en tu máquina y sube solo binarios al servidor (NO sube código fuente .cs)

```powershell
# Desde tu máquina Windows
cd C:\devLX\scnexus-core

# Editar IP y usuario del servidor en build-and-deploy.ps1
.\build-and-deploy.ps1 -ServerIP "172.30.159.208" -ServerUser "usuario"
```

**Ventajas:**
- ❌ NO sube código fuente (.cs) al servidor
- ✅ Solo binarios compilados (.dll)
- 🔒 Mayor seguridad

---

## 🚀 Opción 2: Despliegue con Docker (Compila en el servidor)

**⚠️ Nota:** Esta opción copia el código fuente al servidor para compilar allí.

### 1. Copiar archivos al servidor

```bash
# Desde tu máquina Windows (PowerShell)
scp -r C:\devLX\scnexus-core usuario@172.30.159.208:/home/usuario/

# O usando Git en el servidor
ssh usuario@172.30.159.208
git clone https://github.com/alextz111/scnexus-core.git
cd scnexus-core
```

### 2. Configurar variables de entorno

```bash
# Editar .env.production con las credenciales correctas
nano .env.production
```

### 3. Ejecutar despliegue

```bash
# Dar permisos de ejecución
chmod +x deploy.sh

# Ejecutar script de despliegue
./deploy.sh
```

### 4. Verificar servicio

```bash
# Ver logs en tiempo real
docker-compose -f docker-compose.prod.yml logs -f

# Verificar estado
docker-compose -f docker-compose.prod.yml ps

# Probar endpoint
curl http://210.17.1.54:5053/api/auth/login
```

## 🌐 Acceso desde la Red

El servicio estará disponible en:
- `http://172.30.159.208:5053` (desde tu red)
- `http://210.17.1.54:5053` (desde el servidor)
- Swagger: `http://172.30.159.208:5053/swagger`

## 🔧 Comandos Útiles

```bash
# Ver logs
docker-compose -f docker-compose.prod.yml logs -f

# Reiniciar servicio
docker-compose -f docker-compose.prod.yml restart

# Detener servicio
docker-compose -f docker-compose.prod.yml down

# Actualizar y redesplegar
git pull
./deploy.sh

# Ver estado del contenedor
docker ps
docker stats scnexus-core-api
```

## 🛡️ Configurar como Servicio del Sistema (Opcional)

Para que se inicie automáticamente al arrancar el servidor:

```bash
# Crear servicio systemd
sudo nano /etc/systemd/system/scnexus-core.service
```

Contenido:
```ini
[Unit]
Description=SCNexus Core API
Requires=docker.service
After=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=/home/usuario/scnexus-core
ExecStart=/usr/bin/docker-compose -f docker-compose.prod.yml up -d
ExecStop=/usr/bin/docker-compose -f docker-compose.prod.yml down
User=usuario

[Install]
WantedBy=multi-user.target
```

Activar servicio:
```bash
sudo systemctl daemon-reload
sudo systemctl enable scnexus-core
sudo systemctl start scnexus-core
sudo systemctl status scnexus-core
```

## 🔒 Recomendaciones de Seguridad

1. **Cambiar JWT Secret Key** en producción
2. **Usar HTTPS** con certificado SSL (Nginx reverse proxy)
3. **Firewall:** Permitir solo puerto 5053
   ```bash
   sudo ufw allow 5053/tcp
   sudo ufw enable
   ```
4. **NO subir** `.env.production` a Git
5. Usar variables de entorno del sistema para secretos

## 🔄 Actualización de la Aplicación

```bash
cd /home/usuario/scnexus-core
git pull
./deploy.sh
```

## 📊 Monitoreo

```bash
# Ver uso de recursos
docker stats scnexus-core-api

# Ver logs de errores
docker-compose -f docker-compose.prod.yml logs --tail=100 | grep -i error

# Espacio en disco
df -h
docker system df
```

## ❓ Troubleshooting

### El contenedor no inicia
```bash
docker-compose -f docker-compose.prod.yml logs
```

### Resetear base de datos (⚠️ cuidado en producción)
```bash
# Dentro del contenedor
docker exec -it scnexus-core-api dotnet ef database drop --force
docker exec -it scnexus-core-api dotnet ef database update
```

### Limpiar todo y empezar de nuevo
```bash
docker-compose -f docker-compose.prod.yml down -v
docker system prune -a
./deploy.sh
```
