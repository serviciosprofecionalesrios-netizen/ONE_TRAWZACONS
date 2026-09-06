# Publicación en Render

La aplicación está preparada para ejecutarse en Render como un servicio Docker,
con PostgreSQL administrado y un disco persistente para adjuntos, evidencias,
archivos operativos y claves de protección de datos.

## Recursos creados por el Blueprint

- Servicio web `trawzacons-service-desk` en la región de Virginia.
- Base PostgreSQL `trawzacons-db` en la misma región y red privada.
- Disco persistente de 1 GB montado en `/app/storage`.
- Migración automática del esquema al arrancar.
- Comprobación de salud en `/health`.

Estos recursos son de pago porque Render no permite discos persistentes en el
plan gratuito. Revisa los importes que muestra Render antes de confirmar.

## Primera publicación

1. Sube la rama `render-deploy` al repositorio remoto.
2. En Render, elige **New > Blueprint** y conecta el repositorio.
3. Selecciona `render.yaml` cuando Render lo detecte.
4. Define `BootstrapAdmin__Password` con una contraseña única de al menos 12
   caracteres. No reutilices la contraseña local anterior.
5. Revisa región, planes y precios, y confirma la creación.
6. Espera a que `/health` responda correctamente y entra con
   `admin@trawzacons.com` y la contraseña indicada.

## Variables opcionales

Las notificaciones se despliegan deshabilitadas. Para activarlas, configura en
Render sus secretos y cambia el indicador correspondiente:

- `EmailNotifications__Enabled=true`
- `EmailNotifications__SmtpHost`
- `EmailNotifications__SmtpPort`
- `EmailNotifications__UseSsl`
- `EmailNotifications__UserName`
- `EmailNotifications__Password`
- `EmailNotifications__FromEmail`
- `WhatsAppNotifications__Enabled=true`
- `WhatsAppNotifications__TwilioAccountSid`
- `WhatsAppNotifications__TwilioAuthToken`
- `WhatsAppNotifications__FromNumber`

Las listas de configuración ASP.NET Core usan índices, por ejemplo:
`WhatsAppNotifications__ToNumbers__0`.

## Datos existentes

El primer despliegue crea una base PostgreSQL vacía. No borres ni desconectes la
base SQL Server local hasta validar la migración de datos. La transferencia debe
hacerse después de crear PostgreSQL, usando la URL externa temporal o una lista
de IP autorizadas, y verificando conteos por tabla antes del cambio definitivo.

Los archivos versionados de `Data/Operaciones` se copian al disco sólo durante
la primera inicialización. Los adjuntos nuevos se guardan en el mismo disco y
permanecen después de reinicios y despliegues.

## Desarrollo local

Sin variables nuevas, la aplicación continúa utilizando `SqlServer` y la cadena
de `appsettings.json`. Para probar PostgreSQL localmente:

```powershell
$env:Database__Provider = 'Postgres'
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Database=trawzacons;Username=postgres;Password=...'
$env:BootstrapAdmin__Password = 'una-clave-segura'
dotnet run
```
