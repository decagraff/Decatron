# Decatron

Decatron es un bot modular para Twitch diseñado para gestión de canales: comandos personalizados, temporizadores, moderación, gestión de categorías, estadísticas y más.

## Características

- Comandos personalizados y micro-comandos
- Cambio/registro de categoría (game/title)
- Integración con Twitch (EventSub, API y chat)
- Protección anti-spam y herramientas de moderación
- Arquitectura modular con servicios y EF Core

## Requisitos

- .NET 8 SDK
- MySQL (o compatible) disponible y accesible desde la aplicación
- Node/npm (solo si quieres compilar assets front-end manualmente)

## Configuración rápida

1. Clona el repositorio y abre el proyecto:

   ```bash
   git clone https://github.com/decagraff/Decatron.git
   cd Decatron
   ```

2. Configura las credenciales y secretos:

   - Revisa `appsettings.json`, `appsettings.Development.json` y `appsettings.Secrets.json`.
   - En desarrollo puedes usar User Secrets o `appsettings.Secrets.json` para valores sensibles.

   Sección importante: `TwitchSettings` (ClientId, ClientSecret, BotUsername, ChannelId, RedirectUri, Scopes, WebhookSecret).

3. Base de datos y migraciones (Entity Framework Core)

   - Recomendado: crea la base de datos manualmente en MySQL si tu usuario no tiene permisos para crearla automáticamente:

     ```sql
     CREATE DATABASE decatron CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
     ```

   - Generar la migración inicial (si aún no existe):

     ```bash
     dotnet ef migrations add InitialCreate
     ```

   - Aplicar migraciones a la base de datos:

     ```bash
     dotnet ef database update
     ```

   Nota: dependiendo del proveedor MySQL (Pomelo.EntityFrameworkCore.MySql u otro) y de los privilegios de tu usuario, `dotnet ef database update` puede crear el esquema pero no la base de datos. Si la creación falla, crea la base de datos manualmente como se muestra arriba.

4. Insertar tokens del bot (ejemplo)

   La tabla relevante es `bot_tokens` con campos: BotUsername, AccessToken, ChatToken, CreatedAt, UpdatedAt, IsActive.

   - AccessToken: token usado para llamadas a la API de Twitch (app token / OAuth token para la cuenta que controla el canal).
   - ChatToken: token utilizado por el bot para conectarse al chat (IRC). En la base de datos puede almacenarse con o sin el prefijo `oauth:`. El servicio intentará añadir el prefijo si falta.

   Ejemplo de inserción (usa tus credenciales reales):

   ```sql
   INSERT INTO bot_tokens (BotUsername, AccessToken, ChatToken, CreatedAt, UpdatedAt, IsActive)
   VALUES ('decatrontest', 'APP_ACCESS_TOKEN_AQUI', 'CHAT_OAUTH_TOKEN_AQUI', NOW(), NOW(), 1);
   ```

5. Ejecutar la aplicación

   ```bash
   dotnet run
   ```

   - La aplicación arrancará y registrará el TwitchBotService. Verifica los logs para confirmar que el bot obtuvo el token y pudo conectarse a los canales habilitados.

## Notas operativas

- Para probar autenticación OAuth (login de usuarios) utiliza el flujo configurado en `AuthService` y `TwitchSettings.RedirectUri`.
- Si tienes dudas sobre qué token usar:
  - App token / AccessToken: se usa en llamadas HTTP a la API de Twitch (helix).
  - Chat token / ChatToken: token IRC del bot (a menudo comienza por `oauth:`). Guarda ambos correctamente.

## Migraciones y base de datos — resumen

1. Crear base de datos si es necesario.
2. dotnet ef migrations add InitialCreate
3. dotnet ef database update
4. Insertar registros necesarios (ej. bot_tokens) manualmente.

## Contacto y contribución

Pull requests y issues son bienvenidos. Si aportas código, asegúrate de seguir las convenciones del proyecto y ejecutar las migraciones y pruebas locales.

---

README generado para facilitar el arranque del proyecto Decatron. Ajusta las instrucciones según tu entorno y credenciales.