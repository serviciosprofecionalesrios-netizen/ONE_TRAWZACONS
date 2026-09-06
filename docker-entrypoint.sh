#!/bin/sh
set -eu

storage_root="${PERSISTENT_STORAGE_PATH:-/app/storage}"
operations_storage="$storage_root/operaciones"
uploads_storage="$storage_root/uploads"

mkdir -p "$operations_storage" "$uploads_storage" "$storage_root/keys"

if [ ! -f "$operations_storage/.initialized" ]; then
    if [ -d /app/Data/Operaciones ]; then
        cp -a /app/Data/Operaciones/. "$operations_storage/"
    fi
    touch "$operations_storage/.initialized"
fi

if [ ! -f "$uploads_storage/.initialized" ]; then
    if [ -d /app/wwwroot/uploads ]; then
        cp -a /app/wwwroot/uploads/. "$uploads_storage/"
    fi
    touch "$uploads_storage/.initialized"
fi

rm -rf /app/Data/Operaciones /app/wwwroot/uploads
mkdir -p /app/Data /app/wwwroot
ln -s "$operations_storage" /app/Data/Operaciones
ln -s "$uploads_storage" /app/wwwroot/uploads

exec dotnet ITServiceDeskApp.dll
