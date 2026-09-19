using System.Collections.Generic;
using Ludeka.Application.Contracts;
using Ludeka.Core.Enums;

namespace Ludeka.Jobs;

/// <summary>
/// Identidad de sistema para <c>Ludeka.Jobs</c> (INC-47, R6). El §4.6 del diseño solo prevé
/// <c>ILogger</c> y <see cref="DenyAllSessionPermissionGuard"/> como registros mínimos de un host
/// sin web; es insuficiente — 11 servicios de <c>Ludeka.Application</c> exigen
/// <see cref="ICurrentUserService"/> como parámetro obligatorio de constructor
/// (<c>AuditService</c>, <c>UserManagementService</c>, <c>BggImportService</c>,
/// <c>BggSearchAssistedService</c>, <c>GameEditorService</c>, <c>FoundingVerdictService</c>,
/// <c>AccountConnectionsService</c>, <c>UserLibraryService</c>, <c>UserLibraryStatsService</c>,
/// <c>GamePlayLogService</c>, <c>SessionPermissionGuard</c>), y ninguna de las cuatro rutas de
/// trabajo los necesita, pero <c>ValidateOnBuild</c> valida el grafo completo al construir el
/// contenedor, no solo lo que cada trabajo usa. Hueco de diseño detectado y resuelto por el
/// orquestador (Engram <c>sdd/change-47-workers-cloud-run/hueco-currentuserservice</c>), no por
/// esta fase.
///
/// Implementa <b>exactamente</b> la semántica "sin sesión" que el propio contrato documenta
/// (<see cref="ICurrentUserService"/>: "Sin sesión autenticada, UserId es vacío, Roles no contiene
/// roles y las comprobaciones devuelven false"): deliberadamente sin privilegios, por la misma
/// coherencia que ya motivó registrar <see cref="DenyAllSessionPermissionGuard"/> en vez de una
/// guarda permisiva — un host sin humano detrás no debe presentar una identidad con permisos.
/// Ninguna de las cuatro rutas de trabajo comprueba permisos hoy (§4.6/spec).
/// </summary>
public sealed class SystemCurrentUserService : ICurrentUserService
{
    public string UserId => string.Empty;
    public string UserName => string.Empty;
    public IReadOnlyList<string> Roles => [];
    public bool IsFoundingTeam => false;
    public bool IsInRole(string role) => false;
    public bool HasPermission(ModeratorPermission permission) => false;
}
