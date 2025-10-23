# Configuración de Twitch Developer Console para Decatron

Esta guía te llevará paso a paso para configurar correctamente una aplicación en Twitch Developer Console, necesaria para que Decatron funcione.

## Requisitos Previos

- Cuenta de Twitch activa
- Verificación de cuenta habilitada (autenticación de dos factores recomendada)

## Recomendación Importante: Cuenta Separada para el Bot

**Altamente recomendado**: Usar una cuenta de Twitch dedicada exclusivamente para el bot, separada de tu cuenta principal de streaming. Esto proporciona:

- **Mayor seguridad**: Los tokens del bot no afectan tu cuenta principal
- **Mejor organización**: Separación clara entre bot y streamer
- **Facilidad de gestión**: Permisos y tokens independientes
- **Reducción de riesgos**: En caso de problemas, tu cuenta principal permanece segura

### Configuración con Cuenta Separada

1. **Crea** una nueva cuenta de Twitch para el bot (ej: `tucanal_bot`)
2. **Configura** la aplicación en Twitch Developer Console con tu cuenta principal
3. **Genera tokens** usando la cuenta del bot
4. **Otorga permisos** necesarios desde tu cuenta principal al bot

## Paso 1: Acceder a Twitch Developer Console

1. Ve a [Twitch Developer Console](https://dev.twitch.tv/console)
2. Inicia sesión con tu cuenta de Twitch
3. Si es tu primera vez, acepta los términos de servicio de desarrollador

## Paso 2: Crear Nueva Aplicación

1. Haz clic en **"Applications"** en el menú lateral
2. Selecciona **"Register Your Application"**
3. Completa el formulario:

### Información Básica
- **Name**: `Decatron-TuNombreDeCanal` (ej: `Decatron-MiStream`)
- **OAuth Redirect URLs**: Ver sección siguiente
- **Category**: `Broadcasting Suite`
- **Client Type**: `Confidential`

### OAuth Redirect URLs

**IMPORTANTE**: Debes configurar las URLs según tu entorno:

#### Para Desarrollo Local:
```
https://localhost:7282/api/auth/callback
```

#### Para Producción:
```
https://tudominio.com/api/auth/callback
```

**Nota**: Puedes agregar ambas URLs si planeas usar tanto desarrollo como producción.

## Paso 3: Obtener Credenciales

Una vez creada la aplicación:

1. Haz clic en **"Manage"** en tu aplicación
2. Anota los siguientes valores:

### Client ID
```
Ejemplo: td8vr15lzmvbei4qj4bbd3u4m5jovu
```

### Client Secret
1. Haz clic en **"New Secret"**
2. Copia el secret inmediatamente (solo se muestra una vez)
```
Ejemplo: b8c6mnoqg1xlsbw41foih2h51pqkhd
```

**CRÍTICO**: Guarda el Client Secret inmediatamente. No se puede recuperar después.

## Paso 4: Configurar TwitchTokenManager

Con las credenciales obtenidas, configura el `TwitchTokenManager.exe`:

### Configuración Inicial
1. Ejecuta `TwitchTokenManager.exe`
2. Ingresa los datos de tu aplicación:
   - **Client ID**: El obtenido en el paso anterior
   - **Client Secret**: El secret generado
   - **Redirect URI**: Debe coincidir exactamente con lo configurado en Twitch

### Ejemplo de Configuración
```
Client ID: td8vr15lzmvbei4qj4bbd3u4m5jovu
Client Secret: b8c6mnoqg1xlsbw41foih2h51pqkhd
Redirect URI: https://localhost:7282/api/auth/callback
```

## Paso 5: Tipos de Tokens Necesarios

Decatron requiere dos tipos de tokens:

### App Access Token
- **Uso**: Llamadas generales a la API de Twitch
- **Duración**: ~60 días
- **Renovación**: Automática por el sistema

### User Access Token  
- **Uso**: Acciones específicas del usuario (cambiar título, categoría)
- **Duración**: Variable (configurado en OAuth)
- **Renovación**: Mediante refresh token

### Recomendación
Usa la opción **"Generar Ambos Tokens"** en TwitchTokenManager para obtener todo lo necesario de una vez.

## Paso 6: Webhooks (Opcional pero Recomendado)

Para funcionalidades avanzadas, configura webhooks:

### EventSub Webhook URL
```
Desarrollo: https://localhost:7282/api/eventsub/webhook
Producción: https://tudominio.com/api/eventsub/webhook
```

**Nota**: Los webhooks requieren HTTPS en producción.

## Configuración para Diferentes Entornos

### Desarrollo Local
- **Redirect URI**: `https://localhost:7282/api/auth/callback`
- **EventSub**: `https://localhost:7282/api/eventsub/webhook`
- **Puerto**: 7282 (configurable en `appsettings.json`)

### Producción
- **Redirect URI**: `https://tudominio.com/api/auth/callback`
- **EventSub**: `https://tudominio.com/api/eventsub/webhook`
- **Puerto**: 443 (HTTPS estándar) o el configurado en tu servidor

### Configuración Híbrida
Puedes tener ambos entornos configurados agregando múltiples Redirect URIs:
```
https://localhost:7282/api/auth/callback
https://tudominio.com/api/auth/callback
```

## Configuración de Scopes en TwitchTokenManager

### Selección de Scopes Recomendada

El TwitchTokenManager incluye una interfaz completa para seleccionar scopes:

1. **Interfaz de Scopes**: Lista completa de todos los scopes disponibles organizados en una grilla
2. **Botones de Selección**:
   - **"Seleccionar Todos"**: Marca todos los scopes disponibles
   - **"Deseleccionar Todos"**: Desmarca todos los scopes

### Recomendación: Seleccionar Todos los Scopes

Para máxima funcionalidad del bot, se recomienda usar **"Seleccionar Todos"** porque:

- **Futuras funcionalidades**: El bot está en desarrollo constante
- **Evita problemas**: No tendrás que regenerar tokens por falta de permisos
- **Máxima compatibilidad**: Funciona con todas las características actuales y futuras
- **Simplicidad**: Una sola configuración para todo

### Scopes Críticos para Decatron

Los siguientes scopes son especialmente importantes para las funcionalidades actuales:

```
chat:read               # Leer mensajes del chat
chat:edit               # Enviar mensajes al chat
channel:manage:broadcast # Cambiar título y categoría
user:edit:broadcast     # Permisos de broadcast
channel:bot            # Identificarse como bot
moderation:read        # Leer información de moderación
moderator:read:followers # Leer seguidores
```

### Uso de Scopes por Funcionalidad

- **Comandos `!game` y `!title`**: Requieren `channel:manage:broadcast`
- **Chat del bot**: Requieren `chat:read` y `chat:edit`
- **Sistema de permisos**: Requiere `moderation:read`
- **Funcionalidades futuras**: Pueden requerir scopes adicionales

**Nota**: Los scopes solo se aplican al User Access Token. El App Access Token no requiere scopes específicos.

## Solución de Problemas Comunes

### Error: "Invalid Redirect URI"
- Verifica que la URI en TwitchTokenManager coincida exactamente con la configurada en Twitch
- Asegúrate de usar HTTPS (no HTTP)
- Revisa que no haya espacios o caracteres extra

### Error: "Invalid Client"
- Confirma que el Client ID sea correcto
- Verifica que el Client Secret esté bien copiado
- Asegúrate de que la aplicación esté activa en Twitch Console

### Error: "Unauthorized"
- Revisa que todos los scopes necesarios estén incluidos
- Confirma que la aplicación tenga permisos suficientes
- Verifica que el token no haya expirado

### Tokens no funcionan
- Regenera los tokens usando TwitchTokenManager
- Verifica que el Bot Username corresponda al usuario que autorizó los tokens
- Confirma que la aplicación esté configurada como "Confidential"

## Migración de Desarrollo a Producción

Cuando muevas de desarrollo a producción:

1. **Actualiza Redirect URIs** en Twitch Console
2. **Regenera tokens** con TwitchTokenManager usando las nuevas URLs
3. **Actualiza configuración** en `appsettings.json` y `appsettings.Secrets.json`
4. **Redeploya** la aplicación con la nueva configuración

## Seguridad

### Mejores Prácticas
- **Nunca** compartas tu Client Secret públicamente
- **No** incluyas credenciales en repositorios Git
- **Usa** `appsettings.Secrets.json` para datos sensibles
- **Regenera** tokens si sospechas compromiso
- **Limita** el acceso a la aplicación de Twitch

### Gestión de Secrets
```bash
# Para desarrollo, puedes usar User Secrets
dotnet user-secrets set "TwitchSettings:ClientSecret" "tu_client_secret"
dotnet user-secrets set "TwitchSettings:ClientId" "tu_client_id"
```

## Siguientes Pasos

Una vez completada esta configuración:

1. Regresa al [README principal](./README.md) 
2. Continúa con la configuración de archivos
3. Configura la base de datos
4. Ejecuta Decatron

---

**Documentación específica para configuración de Twitch Developer Console. Parte del proyecto Decatron.**