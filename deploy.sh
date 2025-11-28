#!/bin/bash

# Script de despliegue para scnexus-core en Ubuntu
# Uso: ./deploy.sh

set -e  # Detener en caso de error

echo "🚀 Iniciando despliegue de SCNexus Core..."

# Verificar que Docker está instalado
if ! command -v docker &> /dev/null; then
    echo "❌ Docker no está instalado. Instalando..."
    sudo apt update
    sudo apt install -y docker.io docker-compose
    sudo systemctl start docker
    sudo systemctl enable docker
fi

# Verificar que existe .env.production
if [ ! -f ".env.production" ]; then
    echo "❌ Falta archivo .env.production"
    exit 1
fi

# Detener contenedores anteriores
echo "🛑 Deteniendo contenedores anteriores..."
docker-compose -f docker-compose.prod.yml down || true

# Limpiar imágenes antiguas (opcional)
echo "🧹 Limpiando imágenes antiguas..."
docker image prune -f

# Construir nueva imagen
echo "🔨 Construyendo imagen Docker..."
docker-compose -f docker-compose.prod.yml build --no-cache

# Iniciar contenedores
echo "▶️  Iniciando contenedores..."
docker-compose -f docker-compose.prod.yml up -d

# Verificar estado
echo "✅ Verificando estado..."
sleep 5
docker-compose -f docker-compose.prod.yml ps

# Mostrar logs
echo "📋 Últimos logs:"
docker-compose -f docker-compose.prod.yml logs --tail=50

echo ""
echo "✅ Despliegue completado"
echo "🌐 Servicio disponible en: http://$(hostname -I | awk '{print $1}'):5053"
echo "📚 Swagger: http://$(hostname -I | awk '{print $1}'):5053/swagger"
echo ""
echo "📝 Comandos útiles:"
echo "  Ver logs:      docker-compose -f docker-compose.prod.yml logs -f"
echo "  Reiniciar:     docker-compose -f docker-compose.prod.yml restart"
echo "  Detener:       docker-compose -f docker-compose.prod.yml down"
echo "  Estado:        docker-compose -f docker-compose.prod.yml ps"
