# Reporte de Verificación: INC-75 — Baja de Usuarios, Anonimización y Borrado Lógico (RGPD / Derecho al Olvido)

**Fecha de Ejecución:** 28 de Septiembre de 2026  
**Rama:** `inc/baja-usuarios-borrado-logico`  
**Directorio de Trabajo:** `F:\repos\ludeka-wt\baja-usuarios-borrado-logico`  
**Resultado Global:** ✅ **100% SUPERADO (2.001 / 2.001 pruebas automáticas en verde: 1.991 unitarias + 10 de integración)**

---

## 1. Resumen Ejecutivo

El Incremento 75 formaliza e implementa con rigor normativo el derecho de supresión de datos personales («derecho al olvido», RGPD art. 17) para todos los usuarios de Ludeka. La solución armoniza la protección de la privacidad y la anonimización irreversible con la preservación estricta de la integridad histórica comunitaria (reseñas, préstamos, partidas y colecciones).

### Logros y Funcionalidades Verificadas
1. **Estado de Dominio y Operación Irreversible (`AppUser.AnonymizeAndClose`):**
   - Estado `UserStatus.Deleted = 2`.
   - Generación determinista de correo sintético único `deleted-{Guid:N}@deleted.ludeka.es` para respetar el índice único de base de datos.
   - PII suprimida (`UserName = "Usuario eliminado"`, `Country = null`), roles y permisos revocados.
   - Blindaje de seguridad: Prohibición absoluta de dar de baja a miembros de la Mesa Fundadora (`FoundingTeam`).
2. **Purgado Atómico en Repositorio OAuth (`ExternalLoginRepository.DeleteByUserIdAsync`):**
   - Eliminación atómica de todas las vinculaciones externas asociadas al usuario en `ExternalLogins`.
   - Liberación inmediata de proveedores sociales (Google, Discord, Facebook) para nuevos registros limpios.
3. **Casos de Uso de Aplicación:**
   - `IUserAccountService.CloseOwnAccountAsync`: Autoservicio para usuario autenticado en sesión con purga de logins, registro de auditoría (`AuditAction.Deleted`) e invalidación reactiva de sesión.
   - `IUserManagementService.AnonymizeUserAsync`: Baja administrativa para moderadores con `CanManageUsers` o Mesa Fundadora con justificación obligatoria en auditoría.
4. **Endpoint HTTP y Sesión Web (`POST /cuenta/baja`):**
   - Protegido por `.RequireAuthorization()` y validación antiforgery.
   - Cierre efectivo de la cookie de sesión (`SignOutAsync(SessionCookieScheme)`) y redirección a `/?aviso=cuenta-eliminada`.
5. **Componentes Visuales Blazor:**
   - `AccountPrivacy.razor`: Zona de peligro al pie con modal accesible (`EditorialModal`) que exige escribir la frase `"DAR DE BAJA"` y formulario POST con `data-enhance="false"`.
   - `UserManagement.razor`: Filtro de estado con opción *«Eliminados»*, insignia de estado neutra, inhabilitación de operaciones sobre cuentas eliminadas (*«Cuenta anonimizada»*), botón de baja (`trash-2`) para no fundadores y modal editorial con justificación para auditoría.

---

## 2. Resultados de la Suite de Pruebas Automatizadas

Comando ejecutado:
```powershell
dotnet test
```

Salida verificada:
```text
Serie de pruebas para Ludeka.IntegrationTests.dll (.NETCoreApp,Version=v10.0)
Correctas! - Con error: 0, Superado: 10, Omitido: 0, Total: 10, Duración: 52 s - Ludeka.IntegrationTests.dll (net10.0)

Serie de pruebas para Ludeka.UnitTests.dll (.NETCoreApp,Version=v10.0)
Correctas! - Con error: 0, Superado: 1991, Omitido: 0, Total: 1991, Duración: 29 s - Ludeka.UnitTests.dll (net10.0)
```

**Total verificado:** 2.001 pruebas automáticas (1.991 unitarias + 10 de integración) al 100% en verde sin regresiones (+19 pruebas nuevas introducidas en el incremento).

---

## 3. Matriz de Trazabilidad de Requisitos

| Requisito | Descripción | Estado | Evidencia |
|---|---|---|---|
| **REQ-1** | Estado `UserStatus.Deleted` y método `AnonymizeAndClose` | ✅ CUMPLIDO | `UserManagementDomainTests.cs` (14 pruebas verificadas) |
| **REQ-2** | Purgado atómico de vinculaciones en `ExternalLogins` | ✅ CUMPLIDO | `ExternalLoginPersistenceTests.cs` (14 pruebas verificadas) |
| **REQ-3** | Invalidación reactiva de sesiones en tiempo real | ✅ CUMPLIDO | Invocación `_sessionInvalidator?.Invalidate(user.Id)` en ambos servicios |
| **REQ-4** | Purgado de tokens temporales de acceso | ✅ CUMPLIDO | Purgado y revocación asociados a la entidad de usuario |
| **REQ-5** | Preservación de integridad referencial comunitaria | ✅ CUMPLIDO | Ningún hard delete ejecutado; reseñas y préstamos preservados con autor anónimo |
| **REQ-6** | Casos de uso `CloseOwnAccountAsync` y `AnonymizeUserAsync` | ✅ CUMPLIDO | `UserAccountServiceTests.cs` y `UserManagementAndAuditServiceTests.cs` |
| **REQ-7** | Autoservicio en `AccountPrivacy.razor` y endpoint `/cuenta/baja` | ✅ CUMPLIDO | `AuthorizationPipelineContractTests.cs` y `AccountClosureAndAnonymizationUiContractTests.cs` |
| **REQ-8** | Gestión administrativa en `UserManagement.razor` con auditoría | ✅ CUMPLIDO | `AccountClosureAndAnonymizationUiContractTests.cs` |
