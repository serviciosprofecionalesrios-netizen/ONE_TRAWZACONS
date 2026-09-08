# Publicación en Render

La aplicación está preparada para una demostración temporal en Render como un
servicio Docker gratuito, con PostgreSQL administrado gratuito y almacenamiento
efímero para adjuntos, evidencias, archivos operativos y claves de protección.

## Recursos creados por el Blueprint

- Servicio web `trawzacons-service-desk` en la región de Virginia.
- Base PostgreSQL `trawzacons-db` en la misma región y red privada.
- Directorio efímero `/app/storage` para los archivos de la demostración.
- Migración automática del esquema al arrancar.
- Comprobación de salud en `/health`.

Esta configuración no requiere un recurso de pago. El servicio web gratuito
puede suspenderse después de un periodo sin tráfico, y la primera petición tras
la suspensión tardará más en responder.

La base PostgreSQL gratuita caduca después de 30 días. Los archivos escritos en
`/app/storage` se pierden cuando el servicio se reinicia o se vuelve a desplegar.
Esta variante debe utilizarse únicamente para mostrar el proyecto al cliente.

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

Los adjuntos y archivos operativos creados durante la demostración son
temporales y no permanecen después de reinicios o nuevos despliegues.

## Desarrollo local

Sin variables nuevas, la aplicación continúa utilizando `SqlServer` y la cadena
de `appsettings.json`. Para probar PostgreSQL localmente:

```powershell
$env:Database__Provider = 'Postgres'
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Database=trawzacons;Username=postgres;Password=...'
$env:BootstrapAdmin__Password = 'una-clave-segura'
dotnet run
```
