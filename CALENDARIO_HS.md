# Calendario operativo H&S

## Integración y alcance

La ruta existente `/Hs/Calendar` ahora ofrece semana (lunes–domingo, 06:00–18:00 por defecto y opción de 24 horas) y mes con el mismo modelo. El drawer muestra los registros de la fecha, incluidos los vencimientos en su fecha real. Los pendientes vencidos de cualquier período permanecen en un panel superior abierto, en grupos de vencidas/hoy, 1–15 días y 16–30 días. Cada grupo presenta hasta tres registros y permite abrir el listado completo.

Fuente oficial: `OperacionesController.BuildControlDocumentosModelCore(includeInactive: false, calendarOnly: true)` mediante un adaptador interno `[NonAction]`. Conserva el merge de solicitudes, personas importadas y overrides; las identidades se resuelven con los maestros existentes. La opción evita calcular historial, tendencias y equipos para el calendario, sin cambiar el comportamiento predeterminado de Control de Documentos. Sólo se admiten filas con `IsActive == true` y `MasterId` confirmado. No hay migraciones ni catálogos nuevos.

Los vencimientos se leen de `FechaVencimiento` ya calculada por el módulo, sin duplicar sus reglas de validez. Se recalculan los días con fecha civil de `America/Managua`. Las capacitaciones sin fecha no se inventan ni se incluyen en los KPI de vencimiento. Los documentos personales conservan su clasificación y no incrementan los KPI de capacitación. El KPI verde representa >30 días; amarillo/naranja 1–30; rojo <=0. Estas tres categorías son excluyentes.

La agenda también consulta inspecciones/auditorías, realización de capacitaciones, acciones, permisos y revisiones documentales, con rangos UTC correspondientes a Nicaragua y límite exclusivo. La fuente documental se consolida completa para obtener KPI y alertas globales; los eventos de agenda se envían sólo para el rango visible; los KPI tienen una proyección de vencimientos filtrada e independiente del rango para permitir su consulta en el drawer. Las capacitaciones individuales H&S sólo aparecen si el código del maestro (o, cuando no existe código, el nombre) resuelve de manera única a un conductor activo. No se genera un vencimiento paralelo de HsTrainingRecords: Control de Documentos es la autoridad.

La sede se ofrece únicamente para actividades que tienen ese campo; no se atribuye una sede ficticia a documentos de conductores. El filtro de sede sólo restringe las actividades por sede: los vencimientos documentales permanecen visibles aunque no tengan sede. Los KPI y alertas siguen los filtros, pero no se limitan al rango visible. Los enlaces usan los endpoints existentes; la autorización sigue aplicándose en el destino.

## Alertas y hosting

La campana existente admite ahora enlaces H&S, con los mismos roles del controlador. Las notificaciones son proyecciones vivas, igual que las alertas SLA existentes, sin historial de lectura persistente nuevo. Hay una entrada por conductor/capacitación/vencimiento, que refleja el umbral actual (30, 15, 0); no se acumulan notificaciones de umbrales anteriores. No se envían correos ni WhatsApp desde el nuevo proceso.

`HsExpiryMonitor` evalúa al arrancar y cada 15 minutos, en un scope independiente de peticiones. Actualiza `HsAlertInbox` mediante reemplazo atómico; renovar, borrar o desactivar retira el pendiente en la siguiente lectura/evaluación. La campana vuelve a consultar la fuente para reflejar modificaciones sin esperar al proceso. El almacenamiento es en memoria y se reconstruye de la fuente oficial al reiniciar; no se duplican datos personales en tablas nuevas.

La documentación actual de Render describe un servicio gratuito que puede suspenderse. El proceso sólo corre mientras el servicio está encendido. Para evaluación garantizada sin visitas, usar una instancia siempre activa con los mismos datos y proceso. Si se conserva el servicio suspendible, proponer un job externo programado autenticado que ejecute el evaluador con acceso al mismo almacenamiento persistente; no se ha desplegado ni creado un endpoint público para ello. Los archivos operativos requieren almacenamiento persistente según el despliegue actual. Ningún scheduler dentro del proceso puede operar durante la suspensión.

## Validación

`dotnet run --project scripts/HsCalendarChecks -p:NuGetAudit=false` comprueba umbrales, identidad activa, deduplicación, ejemplo 20/11 → 20/12, fecha de Nicaragua, permanencia de vencidas, renovación/eliminación, desactivación y fuente vacía.

`dotnet build --no-restore` compila controladores y Razor. `node --check wwwroot/js/hs-calendar.js` valida sintaxis. Las verificaciones con base real y navegación autenticada se deben ejecutar en el entorno de aceptación; no se modificó una base de producción.

## Rediseño de visibilidad

Los cuatro KPI son interactivos. El mes limita cada día a tres tarjetas y permite ver todos los registros. La semana comparte una columna de horas, separa los vencimientos del día y ofrece acceso a actividades fuera del horario compacto. La campana reutiliza las clases visuales existentes de notificaciones para que las alertas H&S tengan el mismo formato.
