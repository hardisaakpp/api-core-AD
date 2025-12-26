# Script para compilar localmente y desplegar solo binarios
# Ventajas: 
# - ❌ NO sube código fuente (.cs) al servidor
# - ✅ Solo sube binarios compilados (.dll)
# - 🔒 Mayor seguridad
# - ⚡ Más rápido (compila en tu PC)

param(
    [string]$ServerIP = "192.168.2.153",
    [string]$ServerUser = "root",
    [switch]$SkipTests = $false
)

Write-Host ""
Write-Host "╔════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   SCNexus Core - Build & Deploy (Binarios)    ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Verificar que estamos en el directorio correcto
if (-not (Test-Path "SCNexus.Core.API")) {
    Write-Host "❌ Error: Ejecuta este script desde la raíz del proyecto" -ForegroundColor Red
    exit 1
}

Write-Host "🔨 Compilando proyecto..." -ForegroundColor Cyan
Write-Host "   Configuración: Release" -ForegroundColor Gray
Write-Host "   Target: net8.0" -ForegroundColor Gray
Write-Host ""

# Limpiar compilaciones anteriores
if (Test-Path "publish") {
    Remove-Item -Recurse -Force publish
}

# Compilar en modo Release
cd SCNexus.Core.API
dotnet publish -c Release -o ../publish --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Error al compilar el proyecto" -ForegroundColor Red
    cd ..
    exit 1
}

cd ..

# Mostrar tamaño de los binarios
$publishSize = (Get-ChildItem publish -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
$publishSizeRounded = [math]::Round($publishSize, 2)
Write-Host ""
Write-Host "Compilacion exitosa" -ForegroundColor Green
Write-Host "   Tamanio: $publishSizeRounded MB" -ForegroundColor Gray
Write-Host ""

Write-Host "Comprimiendo binarios..." -ForegroundColor Cyan

# Crear tar.gz en lugar de zip para mejor compatibilidad con Linux
cd publish
tar -czf ../scnexus-core.tar.gz *
cd ..

$tarSize = (Get-Item scnexus-core.tar.gz).Length / 1MB
$tarSizeRounded = [math]::Round($tarSize, 2)
Write-Host "   Archivo: scnexus-core.tar.gz ($tarSizeRounded MB)" -ForegroundColor Gray
Write-Host ""

Write-Host "Subiendo al servidor ${ServerIP}..." -ForegroundColor Cyan

# Subir al servidor
$homeDir = if ($ServerUser -eq "root") { "/root/" } else { "/home/${ServerUser}/" }
scp scnexus-core.tar.gz "${ServerUser}@${ServerIP}:${homeDir}"

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Error al subir archivo al servidor" -ForegroundColor Red
    Write-Host "   Verifica:" -ForegroundColor Yellow
    Write-Host "   - Conexión SSH funciona: ssh ${ServerUser}@${ServerIP}" -ForegroundColor Yellow
    Write-Host "   - Usuario y contraseña son correctos" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Archivo subido correctamente" -ForegroundColor Green
Write-Host ""
Write-Host "Desplegando en el servidor..." -ForegroundColor Cyan

# Crear script de despliegue temporal (con line endings Unix)
$deployScript = @'
echo ""
echo "=============================================="
echo "  Desplegando SCNexus Core en el servidor"
echo "=============================================="
echo ""

cd __HOME_DIR__

# Detener contenedor anterior
echo "Deteniendo contenedor anterior..."
docker stop scnexus-core-api 2>/dev/null || true
docker rm scnexus-core-api 2>/dev/null || true

# Limpiar despliegue anterior
echo "Limpiando archivos anteriores..."
rm -rf scnexus-core-deploy

# Extraer binarios
echo "Extrayendo binarios..."
mkdir -p scnexus-core-deploy
tar -xzf scnexus-core.tar.gz -C scnexus-core-deploy

# Crear Dockerfile simple (solo para binarios)
cat > scnexus-core-deploy/Dockerfile << 'EOFDF'
FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update && apt-get install -y libldap-2.5-0 libsasl2-2 ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY . .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SCNexus.Core.API.dll"]
EOFDF

# Construir imagen Docker
echo "Construyendo imagen Docker..."
cd scnexus-core-deploy
if ! docker build -t scnexus-core-api:latest . ; then
    echo "Error: Fallo al construir la imagen Docker"
    exit 1
fi

# Preparar variables de entorno
echo "Preparando variables de entorno..."
DOCKER_CMD="docker run -d --name scnexus-core-api --restart unless-stopped -p 5053:8080"

if [ -f __HOME_DIR__/.env.production ]; then
    echo "Usando variables de entorno de .env.production"
    # Usar comillas simples para evitar que el shell expanda variables ($) en contraseñas
    # También eliminamos \r por si el archivo fue creado en Windows
    while IFS= read -r line || [ -n "$line" ]; do
        line="${line%$'\r'}"
        if [ -n "$line" ] && [ "${line:0:1}" != "#" ]; then
            DOCKER_CMD="$DOCKER_CMD -e '$line'"
        fi
    done < __HOME_DIR__/.env.production
else
    echo "Advertencia: .env.production no encontrado, usando valores por defecto"
    DOCKER_CMD="$DOCKER_CMD -e ASPNETCORE_ENVIRONMENT=Production -e Jwt__Key=clave_super_secreta_para_jwt_12345 -e Jwt__Issuer=SCNexus.Core -e Jwt__Audience=SCNexus.Users"
fi

DOCKER_CMD="$DOCKER_CMD scnexus-core-api:latest"

# Ejecutar contenedor
echo "Iniciando contenedor..."
eval $DOCKER_CMD

# Verificar estado
echo ""
if docker ps | grep -q scnexus-core-api; then
    echo "Contenedor iniciado correctamente"
    docker ps | grep scnexus-core-api
else
    echo "Error: El contenedor no se inicio correctamente"
    echo "Logs del contenedor:"
    docker logs scnexus-core-api
    exit 1
fi
    
    # Limpiar archivo zip
'@

# Reemplazar placeholder de home dir y normalizar saltos de línea
$deployScript = $deployScript -replace '__HOME_DIR__', $homeDir
$deployScript = $deployScript -replace "`r`n", "`n"

# Convertir a Unix line endings (LF solo)
$deployScript = $deployScript -replace "`r`n", "`n"

# Guardar script temporal
$tempScript = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($tempScript, $deployScript, [System.Text.UTF8Encoding]::new($false))

# Subir y ejecutar script
scp $tempScript "${ServerUser}@${ServerIP}:/tmp/deploy.sh"
ssh "${ServerUser}@${ServerIP}" "bash /tmp/deploy.sh && rm /tmp/deploy.sh"

# Limpiar archivo temporal local
Remove-Item $tempScript -Force

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Error durante el despliegue en el servidor" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "╔════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║          ✅ DESPLIEGUE COMPLETADO ✅           ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "🌐 Servicio disponible en:" -ForegroundColor Cyan
Write-Host "   http://${ServerIP}:5053" -ForegroundColor White
Write-Host "   http://${ServerIP}:5053/swagger" -ForegroundColor White
Write-Host ""
Write-Host "📋 Comandos útiles:" -ForegroundColor Cyan
Write-Host "   Ver logs:      ssh ${ServerUser}@${ServerIP} 'docker logs -f scnexus-core-api'" -ForegroundColor Gray
Write-Host "   Reiniciar:     ssh ${ServerUser}@${ServerIP} 'docker restart scnexus-core-api'" -ForegroundColor Gray
Write-Host "   Detener:       ssh ${ServerUser}@${ServerIP} 'docker stop scnexus-core-api'" -ForegroundColor Gray
Write-Host ""

# Limpiar archivos temporales
Write-Host "🧹 Limpiando archivos temporales..." -ForegroundColor Cyan
Remove-Item scnexus-core.tar.gz -Force -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force publish -ErrorAction SilentlyContinue

Write-Host "✅ Limpieza completada" -ForegroundColor Green
Write-Host ""
