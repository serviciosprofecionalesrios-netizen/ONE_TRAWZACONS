# Inventario publicado

El menú Inventario y su panel incluyen Inventario, Entradas, Salidas y Órdenes de compra del documento público compartido. Cada vista permite buscar todas las columnas y navegar en páginas de 50 registros.

La conexión es de lectura: no importa registros a las tablas locales, no crea tickets, no altera existencias ni escribe en Google Sheets. Las ediciones se hacen en la hoja. Cada pestaña se vuelve a consultar al abrirla después de cinco minutos; Google puede demorar en publicar cambios. La caché reside en memoria por instancia y desaparece al reiniciar. Un error muestra una advertencia y conserva la última consulta disponible. Sin caché se muestra «No disponible».

Fuente: https://docs.google.com/spreadsheets/d/e/2PACX-1vT9UL6D90oBYQxXoKBRow9Zhvlmd7D7dL7QYwMgUYMLafBE9eHDkFcQFQfmXcK4v799GiSboWOm3hKC/pubhtml

## Criterio de registros

Se conserva una fila cuando al menos una de estas columnas tiene contenido:

| Pestaña | Columnas | Registros observados el 2026-09-09 |
| --- | --- | ---: |
| Inventario | ID_PRODUCTO, ITEM, DESCRIPCION | 1438 |
| Entradas | IDENTRADA, ID_O_C, ORDEN DE COMPRA, DESCRIPCION | 6 |
| Salidas | ID_DESPACHO, ID_Salida, N° REQUISA, ITEM, DESCRIPCION | 1126 |
| Orden De Compra | ID_OC, ORDEN DE COMPRA, FACTURA # | 1031 |

Las filas de compras sin esos identificadores contienen descripciones repetidas y valores calculados. No se cuentan como órdenes. La interfaz informa cuántas filas se omitieron. Se conservan duplicados, ceros, vacíos, monedas y fechas originales. El conteo representa registros, no necesariamente órdenes únicas. No se suman STOCK, EXISTENCIA o cantidades recibidas ni se vinculan movimientos sin claves suficientes. «Descripcion OC», «Solicitud de OC» y «Copia de Salidas» no se mezclan con las cuatro pestañas para evitar dobles conteos.

## Verificación

`dotnet build --nologo`

`dotnet run --project scripts/PublishedInventoryChecks`

Opcionalmente, pasar una carpeta con las cuatro exportaciones CSV de la fecha anterior para reconciliar ese corte: `dotnet run --project scripts/PublishedInventoryChecks -- RUTA`. Las exportaciones no se incluyen en el repositorio.

El servicio usa el documento fijo y los permisos existentes del controlador de inventario. No requiere credenciales de Google. Se debe desplegar la versión modificada para habilitar la conexión en el sitio. No necesita migración de base de datos.
