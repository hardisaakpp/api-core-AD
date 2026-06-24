# Despliegue Automático (100% Gratuito)

⚠️ **Nota:** GitHub Actions cobra en repos privados. Esta guía usa una solución 100% gratuita con Git + Cron.

## 🚀 Despliegue Automático con Git Polling

El servidor verifica cada 5 minutos si hay cambios en el repositorio y se actualiza automáticamente.

**Flujo:**
1. Haces `git push` desde tu PC
2. Cada 5 min, el servidor verifica si hay cambios
3. Si hay cambios, hace `git pull` y redespliega automáticamente

## 📋 Configuración Inicial (Una sola vez)

### 1. En el servidor Ubuntu, clonar el repositorio

```bash
ssh usuario@172.30.159.208

# Instalar Git si no está instalado
sudo apt update
sudo apt install -y git

# Clonar repo (usar token para repo privado)
cd /home/usuario
git clone https://github.com/alextz111/scnexus-core.git
cd scnexus-core
```

### 2. Configurar credenciales de Git (para repo privado)

**Opción A: Personal Access Token (Recomendado)**

```bash
# En GitHub: Settings → Developer settings → Personal access tokens → Tokens (classic)
# Crear token con permisos: repo (todos)

# Configurar en el servidor
git config --global credential.helper store
git pull  # Te pedirá usuario y token (NO password)
# Username: alextz111
# Password: ghp_xxxxxxxxxxxxx (tu token)
```

**Opción B: Deploy Key (Solo lectura)**

```bash
# En el servidor
ssh-keygen -t rsa -b 4096 -f ~/.ssh/scnexus_deploy -N ""
cat ~/.ssh/scnexus_deploy.pub

# Copiar la clave pública
# En GitHub: Repo Settings → Deploy keys → Add deploy key
# Pegar la clave, marcar "Allow write access" si necesitas push

# Configurar Git para usar esta clave
echo "Host github.com
  IdentityFile ~/.ssh/scnexus_deploy
  IdentitiesOnly yes" >> ~/.ssh/config
```

### 3. Copiar scripts de despliegue

```bash
cd /home/usuario/scnexus-core
chmod +x auto-deploy.sh deploy.sh
```

### 4. Configurar archivo .env.production

```bash
nano .env.production
```

Contenido:
```env
ASPNETCORE_ENVIRONMENT=Production
DB_SERVER=210.17.1.54
DB_NAME=SCNexusDB
DB_USER=desadmin
DB_PASSWORD=***REMOVED***
JWT_KEY=clave_super_secreta_para_jwt_12345
JWT_ISSUER=SCNexus.Core
JWT_AUDIENCE=SCNexus.Users
```

### 5. Configurar cron para verificar cambios cada 5 minutos

```bash
# Editar crontab
crontab -e

# Añadir esta línea al final:
*/5 * * * * /home/usuario/scnexus-core/auto-deploy.sh

# Guardar y salir (Ctrl+O, Enter, Ctrl+X)
```

### 6. Crear archivo de log

```bash
sudo touch /var/log/scnexus-autodeploy.log
sudo chown usuario:usuario /var/log/scnexus-autodeploy.log
```

### 7. Despliegue inicial

```bash
cd /home/usuario/scnexus-core
./deploy.sh
```

## ✅ Uso Diario

Ahora cada vez que hagas cambios:

```bash
# En tu PC Windows
git add .
git commit -m "Actualización X"
git push origin main

# ⏳ Espera máximo 5 minutos
# ✅ El servidor detectará los cambios y desplegará automáticamente
```

## 📊 Ver Estado del Despliegue

```bash
# Ver logs en tiempo real
ssh usuario@172.30.159.208
tail -f /var/log/scnexus-autodeploy.log

# Ver logs del contenedor
docker logs -f scnexus-core-api
```

## 🔧 Despliegue Manual (si no quieres esperar 5 min)

```bash
ssh usuario@172.30.159.208
cd /home/usuario/scnexus-core
./auto-deploy.sh  # Fuerza la verificación inmediata
```

## ⚡ Despliegue Instantáneo (Opcional)

Para desplegar inmediatamente después de `git push`, sin esperar 5 minutos:

```powershell
# En tu PC, crear un alias en PowerShell
# Añadir a tu PowerShell profile (notepad $PROFILE):
function Deploy-SCNexus {
    git push origin main
    if ($LASTEXITCODE -eq 0) {
        ssh usuario@172.30.159.208 "/home/usuario/scnexus-core/auto-deploy.sh"
    }
}
Set-Alias deploy Deploy-SCNexus

# Uso:
deploy  # Push + despliegue inmediato
```

## 🔍 Troubleshooting

### El cron no se ejecuta
```bash
# Verificar que cron está activo
sudo systemctl status cron

# Ver logs de cron
grep CRON /var/log/syslog

# Verificar crontab
crontab -l
```

### Git pull pide contraseña
```bash
# Volver a configurar credenciales
git config --global credential.helper store
git pull  # Ingresar token
```

### Ver si hay cambios pendientes
```bash
cd /home/usuario/scnexus-core
git fetch origin main
git log HEAD..origin/main --oneline
```

## 💡 Cambiar frecuencia de verificación

```bash
crontab -e

# Cada 2 minutos:
*/2 * * * * /home/usuario/scnexus-core/auto-deploy.sh

# Cada 10 minutos:
*/10 * * * * /home/usuario/scnexus-core/auto-deploy.sh

# Cada hora:
0 * * * * /home/usuario/scnexus-core/auto-deploy.sh
```

## 🎯 Ventajas de esta Solución

✅ **100% Gratuita** - Sin costos adicionales
✅ **Simple** - Solo Git + Cron
✅ **Automática** - Push y olvídate
✅ **Logs completos** - Sabes qué pasó y cuándo
✅ **Sin dependencias** - No requiere servicios externos
✅ **Funciona con repos privados** - Sin limitaciones

## ⚠️ Desventajas vs GitHub Actions

❌ No es instantáneo (espera hasta 5 min)
❌ Compila en el servidor (más lento)
❌ Código fuente queda en el servidor
❌ Sin interfaz gráfica de despliegues
