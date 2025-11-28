#!/bin/bash

# Script de auto-despliegue con Git
# Se ejecuta automáticamente cuando detecta cambios en el repositorio

REPO_DIR="/home/usuario/scnexus-core"
LOG_FILE="/var/log/scnexus-autodeploy.log"

echo "[$(date)] Verificando cambios..." >> $LOG_FILE

cd $REPO_DIR

# Guardar hash actual
BEFORE=$(git rev-parse HEAD)

# Hacer pull
git fetch origin main
AFTER=$(git rev-parse origin/main)

# Si hay cambios
if [ "$BEFORE" != "$AFTER" ]; then
    echo "[$(date)] Cambios detectados. Desplegando..." >> $LOG_FILE
    
    git pull origin main
    
    # Redesplegar
    docker-compose -f docker-compose.prod.yml down
    docker-compose -f docker-compose.prod.yml build --no-cache
    docker-compose -f docker-compose.prod.yml up -d
    
    echo "[$(date)] ✅ Despliegue completado" >> $LOG_FILE
else
    echo "[$(date)] Sin cambios" >> $LOG_FILE
fi
