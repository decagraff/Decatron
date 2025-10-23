# Decatron

Decatron es un bot modular para Twitch dise�ado para gesti�n de canales: comandos personalizados, temporizadores, moderaci�n, gesti�n de categor�as, estad�sticas y m�s.

## Caracter�sticas

- Comandos personalizados y micro-comandos
- Cambio/registro de categor�a (game/title)
- Integraci�n con Twitch (EventSub, API y chat)
- Protecci�n anti-spam y herramientas de moderaci�n
- Arquitectura modular con servicios y EF Core

## Requisitos

- .NET 8 SDK
- MySQL (o compatible) disponible y accesible desde la aplicaci�n
- Node/npm (solo si quieres compilar assets front-end manualmente)

## Configuraci�n r�pida

1. Clona el repositorio y abre el proyecto:

   ```bash
   git clone https://github.com/decagraff/Decatron.git
   cd Decatron
   ```

2. Configura las credenciales y secretos:

   - Revisa `appsettings.json`, `appsettings.Development.json` y `appsettings.Secrets.json`.
   - En desarrollo puedes usar User Secrets o `appsettings.Secrets.json` para valores sensibles.

   Secci�n importante: `TwitchSettings` (ClientId, ClientSecret, BotUsername, ChannelId, RedirectUri, Scopes, WebhookSecret).

3. Base de datos y migraciones (Entity Framework Core)

   - Recomendado: crea la base de datos manualmente en MySQL si tu usuario no tiene permisos para crearla autom�ticamente:

     ```sql
     CREATE DATABASE decatron CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
     ```

   - Generar la migraci�n inicial (si a�n no existe):

     ```bash
     dotnet ef migrations add InitialCreate
     ```

   - Aplicar migraciones a la base de datos:

     ```bash
     dotnet ef database update
     ```

   Nota: dependiendo del proveedor MySQL (Pomelo.EntityFrameworkCore.MySql u otro) y de los privilegios de tu usuario, `dotnet ef database update` puede crear el esquema pero no la base de datos. Si la creaci�n falla, crea la base de datos manualmente como se muestra arriba.

4. Insertar tokens del bot (ejemplo)

   La tabla relevante es `bot_tokens` con campos: BotUsername, AccessToken, ChatToken, CreatedAt, UpdatedAt, IsActive.

   - AccessToken: token usado para llamadas a la API de Twitch (app token / OAuth token para la cuenta que controla el canal).
   - ChatToken: token utilizado por el bot para conectarse al chat (IRC). En la base de datos puede almacenarse con o sin el prefijo `oauth:`. El servicio intentar� a�adir el prefijo si falta.

   Ejemplo de inserci�n (usa tus credenciales reales):

   ```sql
   INSERT INTO bot_tokens (BotUsername, AccessToken, ChatToken, CreatedAt, UpdatedAt, IsActive)
   VALUES ('decatrontest', 'APP_ACCESS_TOKEN_AQUI', 'CHAT_OAUTH_TOKEN_AQUI', NOW(), NOW(), 1);
   ```

5. Ejecutar la aplicaci�n

   ```bash
   dotnet run
   ```

   - La aplicaci�n arrancar� y registrar� el TwitchBotService. Verifica los logs para confirmar que el bot obtuvo el token y pudo conectarse a los canales habilitados.

## Notas operativas

- Para probar autenticaci�n OAuth (login de usuarios) utiliza el flujo configurado en `AuthService` y `TwitchSettings.RedirectUri`.
- Si tienes dudas sobre qu� token usar:
  - App token / AccessToken: se usa en llamadas HTTP a la API de Twitch (helix).
  - Chat token / ChatToken: token IRC del bot (a menudo comienza por `oauth:`). Guarda ambos correctamente.

## Migraciones y base de datos � resumen

1. Crear base de datos si es necesario.
2. dotnet ef migrations add InitialCreate
3. dotnet ef database update
4. Insertar registros necesarios (ej. bot_tokens) manualmente.

## Contacto y contribuci�n

Pull requests y issues son bienvenidos. Si aportas c�digo, aseg�rate de seguir las convenciones del proyecto y ejecutar las migraciones y pruebas locales.

---

README generado para facilitar el arranque del proyecto Decatron. Ajusta las instrucciones seg�n tu entorno y credenciales.