# Checklist de Tareas: INC-75 — Baja de Usuarios, Anonimización y Borrado Lógico (RGPD / Derecho al Olvido)

## Fase 1: Dominio y Entidad `AppUser`
- [x] 1.1 Añadir el valor `Deleted` a `src/Ludeka.Core/Enums/UserStatus.cs`.
- [x] 1.2 Implementar el método de dominio `AnonymizeAndClose(string? reason = null)` en `src/Ludeka.Core/Entities/AppUser.cs` con validación de blindaje para `FoundingTeam`.
- [x] 1.3 Crear pruebas unitarias de dominio en `tests/Ludeka.UnitTests/Domain/AppUserTests.cs` verificando la anonimización de PII, generación de correo sintético único y protección de la Mesa Fundadora.

## Fase 2: Infraestructura y Repositorios
- [x] 2.1 Extender `IExternalLoginRepository` con `DeleteByUserIdAsync(string userId, CancellationToken cancellationToken = default)`.
- [x] 2.2 Implementar `DeleteByUserIdAsync` en `src/Ludeka.Infrastructure/Data/ExternalLoginRepository.cs`.
- [x] 2.3 Crear o ampliar pruebas unitarias de repositorios en `tests/Ludeka.UnitTests/Infrastructure/ExternalLoginRepositoryTests.cs`.

## Fase 3: Casos de Uso y Servicios de Aplicación
- [x] 3.1 Extender `IUserManagementService` con `AnonymizeUserAsync(string userId, string reason, CancellationToken ct = default)` e implementarlo en `UserManagementService.cs` con purga de logins, registro de auditoría (`AuditAction.Deleted`, `AuditEntityType.User`) e invalidación de sesión.
- [x] 3.2 Crear contrato `IUserAccountService` e implementarlo en `src/Ludeka.Application/Features/Account/UserAccountService.cs` para el autoservicio de baja voluntaria del usuario autenticado en sesión.
- [x] 3.3 Registrar `IUserAccountService` en el contenedor de dependencias (`Program.cs` / DI extensions).
- [x] 3.4 Crear pruebas unitarias de aplicación para `UserManagementService.AnonymizeUserAsync` y `UserAccountService.CloseOwnAccountAsync`.

## Fase 4: Endpoints HTTP y Capa Web
- [x] 4.1 Añadir endpoint POST `/cuenta/baja` con antiforgery, llamada a `CloseOwnAccountAsync`, cierre de cookie (`SignOutAsync`) y redirección a `/?aviso=cuenta-eliminada`.
- [x] 4.2 Añadir pruebas de integración/rutas web para el endpoint de baja.

## Fase 5: Interfaz de Usuario Blazor
- [ ] 5.1 Incorporar la sección «Zona de peligro / Dar de baja mi cuenta» en `AccountPrivacy.razor` con diálogo modal accesible que requiera confirmación explícita mediante la frase `"DAR DE BAJA"`.
- [ ] 5.2 Actualizar `UserManagement.razor` para mostrar la insignia de estado *«Eliminado»*, inhabilitar acciones sobre usuarios ya eliminados y proporcionar el botón y modal de baja administrativa para moderadores con `CanManageUsers`.
- [ ] 5.3 Añadir pruebas de componentes Razor para `AccountPrivacy.razor` y `UserManagement.razor`.

## Fase 6: Verificación Integral y Suite de Pruebas
- [ ] 6.1 Ejecutar `dotnet test` y constatar el 100% de la suite de pruebas en verde sin regresiones.
