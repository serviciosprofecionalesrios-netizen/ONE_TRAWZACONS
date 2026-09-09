# Reparaciones en Mantenimiento

Ruta: `/MaintenanceRepairs`. El menú Mantenimiento incluye Reparaciones.

Fuente: https://docs.google.com/spreadsheets/d/e/2PACX-1vQaXxESkG30H_6igTHWwFQSv2h-i8zqbZ6e9X_tiR5ggvrY6incp4R7TF9RXcr1UZYwa54_Z7ba4rgs/pubhtml

Se consulta exclusivamente Reparaciones, gid 1524236300. Se incluyen filas con ID Reparación, N° Gestion o Codigo de Equipo. Corte revisado: 108 reparaciones, 90 Entregado, 10 Terminado y 8 Pendiente. Tres filas vacías se omiten. Los estados son los originales, sin inferirlos por fecha de salida.

La vista muestra folio, equipo, mecánico, fechas, sitio y problema. El detalle incluye diagnóstico, trabajo realizado, elemento, cliente, responsable y kilometraje. No muestra identificadores internos ni rutas de fotos o firmas que no sean URLs accesibles. No combina otras pestañas sin relaciones verificadas.

La lectura usa la caché de cinco minutos y control de errores del servicio existente, con documento independiente. No importa datos a la base ni escribe en la hoja. Conserva los permisos del módulo y no requiere migraciones.

Verificación: `dotnet build` y `dotnet run --project scripts/PublishedInventoryChecks -- RUTA_CSV`. La carpeta opcional contiene las exportaciones de inventario previas y Reparaciones.csv del corte revisado. No se incluyen los CSV en el repositorio.
