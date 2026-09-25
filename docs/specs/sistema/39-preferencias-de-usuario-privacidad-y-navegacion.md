# 39. Preferencias de Usuario, Privacidad de Perfil y Navegación de Cuenta

> **Módulo:** 39 — Preferencias de Usuario, Privacidad y Navegación  
> **Estado:** Implementado y Verificado (INC-62)  
> **Fuentes:** [`AccountMenu.razor`](file:///C:/repos/ludeka/src/Ludeka.Web/Components/Shared/AccountMenu.razor), [`AccountSectionNav.razor`](file:///C:/repos/ludeka/src/Ludeka.Web/Components/Shared/AccountSectionNav.razor), [`AccountAppearance.razor`](file:///C:/repos/ludeka/src/Ludeka.Web/Components/Pages/AccountAppearance.razor), [`AccountCountry.razor`](file:///C:/repos/ludeka/src/Ludeka.Web/Components/Pages/AccountCountry.razor), [`AccountPrivacy.razor`](file:///C:/repos/ludeka/src/Ludeka.Web/Components/Pages/AccountPrivacy.razor), [`PublicProfile.razor`](file:///C:/repos/ludeka/src/Ludeka.Web/Components/Pages/PublicProfile.razor), [`MainLayout.razor`](file:///C:/repos/ludeka/src/Ludeka.Web/Components/Layout/MainLayout.razor), [`UserPreference.cs`](file:///C:/repos/ludeka/src/Ludeka.Core/Entities/UserPreference.cs), [`SqliteUserPreferenceService.cs`](file:///C:/repos/ludeka/src/Ludeka.Infrastructure/Services/SqliteUserPreferenceService.cs)  
> **Pruebas:** `AccountPreferencesContractTests.cs`, `AccountMenuContractTests.cs`, `AccountAreaContractTests.cs`, `WebMarkupContractTests.cs` (1.703 pruebas unitarias superadas)

---

## 1. Contexto y Objetivos de Diseño

El incremento INC-62 optimiza y desacopla la experiencia de usuario en torno a su cuenta y preferencias:
1. **Limpieza de la cabecera:** La barra de navegación superior contenía accesos duplicados (`Mi Ludoteca` y el selector modal de país) que saturaban las utilidades de cabecera. Ambos accesos se consolidan en el menú de cuenta desplegable.
2. **Eliminación de redundancias en el menú de cuenta:** El enlace intermedio «Área de Cuenta» (`/cuenta`) dentro del desplegable obligaba a navegar por una pantalla trampolín que duplicaba las opciones ya disponibles como submenús directos.
3. **Pantallas dedicadas de preferencias:** Separación de las ventanas de Apariencia y País en rutas independientes (`/cuenta/apariencia` y `/cuenta/pais`), junto con la ludoteca (`/cuenta/ludoteca`) y conexiones (`/cuenta/conexiones`).
4. **Privacidad y visibilidad del perfil público:** Incorporación de un control explícito para que el usuario pueda ocultar su perfil público y su ludoteca a visitantes u otros miembros de la comunidad (`/cuenta/privacidad`), persistido de forma determinista en `UserPreference.HidePublicProfile`.

---

## 2. Limpieza de Cabecera y Topología de Navegación

### 2.1 Cabecera Global (`MainLayout.razor`)
Se retiran los botones de acceso directo de cabecera a país y ludoteca, dejando las utilidades limpias y ordenadas:
- Menú catálogo desplegable (SSR / móvil).
- Indicador offline.
- Componente `<AccountMenu />`.
- Menú de gestión editorial / mesa fundadora (solo para administradores y moderadores).

### 2.2 Desplegable Canónico de Cuenta (`AccountMenu.razor`)
El menú de cuenta ofrece acceso directo y accesible (WCAG 2.2 AA) a los 7 destinos del usuario autenticado:
1. **Mi Perfil Público** (`/u/{UserId}`): Acceso a la ficha pública comunitaria.
2. **Mi Ludoteca** (`/cuenta/ludoteca`): Acceso a la colección, partidas y préstamos.
3. **Apariencia & Tema** (`/cuenta/apariencia`): Selección del tema visual entre los 5 temas editoriales.
4. **Ubicación & País** (`/cuenta/pais`): Selección territorial para filtrado de tiendas, eventos y sorteos.
5. **Privacidad** (`/cuenta/privacidad`): Control de visibilidad del perfil público.
6. **Conexiones OAuth** (`/cuenta/conexiones`): Vinculación multi-proveedor (Google, Discord).
7. **Cerrar Sesión** (`/logout`): Cierre de sesión seguro con invalidación de cookie.

### 2.3 Barra de Sección Compartida (`AccountSectionNav.razor`)
Todas las páginas hijas del área de cuenta montan la barra de sección compartida con enlaces directos y `aria-current="page"` para la sección activa:
- Retorno a Cuenta (`/cuenta`)
- Ludoteca (`/cuenta/ludoteca`)
- Apariencia (`/cuenta/apariencia`)
- País (`/cuenta/pais`)
- Privacidad (`/cuenta/privacidad`)
- Conexiones (`/cuenta/conexiones`)

---

## 3. Modelo de Dominio y Persistencia de Privacidad

### 3.1 Entidad `UserPreference` (`Ludeka.Core`)
Se añade la propiedad `HidePublicProfile`:
```csharp
public bool HidePublicProfile { get; private set; }

public void SetProfileVisibility(bool hidePublicProfile)
{
    HidePublicProfile = hidePublicProfile;
    UpdatedAt = DateTimeOffset.UtcNow;
}
```

### 3.2 Migración DDL y Reconciliación en SQLite
El esquema se sincroniza mediante `SqliteSchemaMigrator` asegurando compatibilidad hacia adelante y hacia atrás:
```sql
ALTER TABLE "UserPreferences" ADD COLUMN "HidePublicProfile" INTEGER NOT NULL DEFAULT 0;
```

### 3.3 Contrato de Aplicación e Infraestructura
- `IUserPreferenceService.IsPublicProfileHiddenAsync(string userId, CancellationToken ct = default)`: Consulta el estado de visibilidad pública.
- `IUserPreferenceService.SetPublicProfileHiddenAsync(string userId, bool hidePublicProfile, CancellationToken ct = default)`: Persiste el ajuste del usuario.

---

## 4. Guarda de Perfil Privado (`PublicProfile.razor`)

En la pantalla de perfil público comunitaria (`/u/{UserId}`):
- Si el perfil está configurado como privado (`HidePublicProfile == true`):
  - **Visitantes o terceros:** Visualizan una pantalla de aviso sobrio indicando que el usuario ha configurado su perfil como privado, impidiendo la exposición de su colección, estadísticas o préstamos.
  - **Propietario del perfil:** Se le permite visualizar su perfil público pero se le muestra un banner informativo accesible avisando que el perfil se encuentra actualmente oculto para la comunidad, con enlace directo a `/cuenta/privacidad` para modificarlo.

---

## 5. Verificación Automática

- **Pruebas de Contrato de Preferencias:** `tests/Ludeka.UnitTests/Web/AccountPreferencesContractTests.cs` valida las 3 pantallas dedicadas y la guarda de perfil privado.
- **Pruebas de Menú de Cuenta:** `tests/Ludeka.UnitTests/Web/AccountMenuContractTests.cs` certifica la ausencia de la opción redundante `/cuenta` y la presencia de los 7 destinos canónicos.
- **Pruebas de Área de Cuenta:** `tests/Ludeka.UnitTests/Web/AccountAreaContractTests.cs` valida el inventario del hub y el montaje de `AccountSectionNav` en todas las rutas hijas.
- **Pruebas de Iconografía sin Emojis:** `tests/Ludeka.UnitTests/Infrastructure/WebMarkupContractTests.cs` certifica la conformidad de marcado Lucide en `MainLayout` y `AccountMenu`.
- **Métricas:** 1.703 pruebas unitarias ejecutadas al 100% en verde.
