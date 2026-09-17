# Informe de Verificacion -- INC-49: Vinculacion de Cuentas entre Proveedores

> Cambio: change-49-vinculacion-cuentas . Fase: sdd-verify . Fecha: 2026-09-18
> Rama verificada: inc/vinculacion-cuentas-07-reemplazo-correo (commit a4479d7) -- punta de la cadena de 7 PRs, worktree limpio.
> Veredicto: PASS CON ADVERTENCIAS (pass_with_warnings) -- 0 hallazgos CRITICAL, 1 WARNING, 2 SUGGESTION.

---

## 1. Resultado literal del runner contractual

dotnet test Ludeka.sln
Correctas! - Con error: 0, Superado: 1417, Omitido: 0, Total: 1417, Duracion: 17 s

Coincide exactamente con el total esperado (1417). Linea base previa a INC-49: 1345 pruebas verdes (INC-46 archivado). Confirmado que tests/Ludeka.UnitTests/Application/ExternalLoginServiceTests.cs -- las 6 pruebas de regresion obligatorias de INC-46 -- no tiene ninguna linea modificada en toda la cadena:

git diff 01b4b73..HEAD --name-only -- tests/Ludeka.UnitTests/Application/ExternalLoginServiceTests.cs
(salida vacia)

No hubo bloqueo por MSB3027: se comprobo que no habia ningun proceso Ludeka.Web/dotnet en ejecucion antes de compilar.

---

## 2. Recuento de escenarios -- 35/35 conformes

Ninguno no conforme. Ninguno con salvedad que afecte a la conducta observable. Nota de metodo aplicable a los escenarios puramente visuales de Blazor (1, 2, 16, 17, 18, 19): el repositorio no tiene bUnit ni WebApplicationFactory (verificado -- cero resultados en toda la carpeta tests/), asi que toda prueba de UI de este incremento, igual que las de INC-46, es "contrato de fuente" (lectura del .razor como texto) en vez de renderizado o integracion HTTP real. Es la convencion ya establecida del repositorio, no una carencia introducida por este cambio; la via que si ejercita el conjunto de extremo a extremo es el smoke test manual (tarea 4.5, pendiente y correctamente declarado como tal).

### account-provider-connections (19/19 conformes)

1. Anonimo en SSR no accede al contenido -- Codigo: AccountConnections.razor:2 (@attribute [Authorize]), pipeline Program.cs (orden UseAuthentication->UseAuthorization) -- Prueba: AccountConnectionsPageContractTests.cs:17-28, AuthorizationPipelineContractTests.cs:30-43 (generico)
2. Anonimo interactivo redirigido -- Codigo: Routes.razor + RedirectToLogin.razor (mecanismo INC-46, sin cambios) -- Prueba: AuthorizationPipelineContractTests.cs:101-112
3. Autenticado sin permiso de moderacion accede -- Codigo: AccountConnections.razor:2 sin Policy= -- Prueba: AccountConnectionsPageContractTests.cs:17-28
4. Listado de proveedores y estado de vinculacion -- Codigo: AccountConnectionsService.BuildViewAsync, AccountConnectionsService.cs:52-78 -- Prueba: AccountConnectionsServiceTests.cs:87-108
5. Vincular proveedor no vinculado -- Codigo: ExternalLoginService.LinkAsync, ExternalLoginService.cs:109-147 -- Prueba: ExternalLoginLinkingTests.cs:60-75
6. Identidad vinculada es la de la sesion, no la del cliente -- Codigo: Program.cs:511 (lee de httpContext.User, nunca de form), ExternalLoginEvents.cs:65-77 (reconfirmacion) -- Prueba: ExternalLoginIntentTests.cs:15-75, ExternalLoginEventsLinkBranchTests.cs:56-83
7. Rechazo cuando el proveedor ya pertenece a otra cuenta -- Codigo: ExternalLoginService.cs:128-134 -- Prueba: ExternalLoginLinkingTests.cs:93-108
8. El rechazo no promete resolucion inexistente -- Codigo: AccountConnectionMessages.cs:14-25 (sin "fusion"/"transferir"/"soporte") -- Prueba: AccountConnectionMessagesTests.cs:18-31
9. La fila en conflicto nunca cambia de propietario -- Codigo: ExternalLoginService.cs:136-147 (catch DuplicateExternalLoginException) -- Prueba: ExternalLoginLinkingTests.cs:111-132
10. Desvincular el unico proveedor se deniega -- Codigo: ExternalLoginService.cs:246-250 -- Prueba: ExternalLoginLinkingTests.cs:219-234
11. Peticion que evita la interfaz tambien se deniega -- misma ubicacion; la prueba llama UnlinkAsync directamente, sin UI -- Prueba: ExternalLoginLinkingTests.cs:219-234
12. Desvincular procede con 2+ metodos -- Codigo: ExternalLoginService.cs:252 -- Prueba: ExternalLoginLinkingTests.cs:203-217
13. Vincular registra auditoria -- Codigo: ExternalLoginService.cs:154-163 -- Prueba: ExternalLoginLinkingTests.cs:276-290
14. Desvincular registra auditoria -- Codigo: ExternalLoginService.cs:254-258 -- Prueba: ExternalLoginLinkingTests.cs:292-309
15. Intento denegado/rechazado no audita exito -- RecordAuditAsync solo se alcanza tras exito (ExternalLoginService.cs:283-314) -- Prueba: ExternalLoginLinkingTests.cs:311-343
16. Aviso visible en conexiones -- Codigo: AccountConnections.razor:36-42 -- Prueba: AccountConnectionsServiceTests.cs:131-144 (nivel servicio)
17. Aviso visible y descartable en cabecera -- Codigo: AccountEmailNotice.razor completo; MainLayout.razor:230 -- Prueba: AuthorizationPipelineContractTests.cs:131-139
18. Aviso ausente con correo verificado -- Codigo: AccountConnectionsService.cs:76 (HasVerifiedProviderEmail = links.Any(...)) -- Prueba: AccountConnectionsServiceTests.cs:131-144
19. Aviso nunca en perfil publico -- Codigo: PublicProfile.razor (grep de AccountEmailNotice/IAccountConnectionsService/HasVerifiedProviderEmailAsync sin resultados) -- Prueba: AuthorizationPipelineContractTests.cs:141-151

### social-login-authentication (13/13 conformes)

20. Columna en BD migrada desde cero -- Codigo: Migracion 20260917112154_AddProviderEmailVerifiedAtToExternalLogins.cs (aditiva, Down con DropColumn); LudekaDbContext.cs:490 -- Prueba: ExternalLoginPersistenceTests.cs:171-188
21. Columna reconciliada en SQLite preexistente -- Codigo: SqliteSchemaMigrator.cs:854 (bloque 22, creacion) + :865-876 (bloque 23, ALTER TABLE) -- Prueba: SqliteSchemaMigratorTests.cs:80-185
22. Verificacion registrada si llega verificado -- Codigo: ExternalLogin.cs:47 (invariante en el constructor) -- Prueba: ExternalLoginTests.cs:109-121
23. Ausencia de verificacion si no llega -- mismo invariante -- Prueba: ExternalLoginTests.cs:123-142
24. Reemplazo exitoso del correo sintetico -- Codigo: ExternalLoginService.cs:171-202 (TryReplacePlaceholderEmailAsync) -- Prueba: ExternalLoginLinkingTests.cs:146-166
25. Reemplazo rechazado por colision -- misma funcion, guarda GetByEmailAsync -- Prueba: ExternalLoginLinkingTests.cs:168-184
26. Cuenta sin correo sintetico no afectada -- guarda IsPlaceholderEmail -- Prueba: ExternalLoginLinkingTests.cs:186-200
27. Par reincidente resuelve a la misma cuenta -- Codigo: ExternalLoginService.cs:52-61 (rama 1) -- Prueba: ExternalLoginCascadeRegressionTests.cs:57-67
28. Correo verificado + cuenta sin identidades (2a) -- Codigo: ExternalLoginService.cs:82-87 -- Prueba: ExternalLoginCascadeRegressionTests.cs:69-88
29. Correo verificado + cuenta con identidades (2b) -- Codigo: ExternalLoginService.cs:72-80 (throw ExternalLoginCollisionException, 0 escrituras) -- Prueba: ExternalLoginCascadeRegressionTests.cs:90-107
30. Alta de usuario comunitario -- Codigo: ExternalLoginService.cs:91-105 (rama 3) -- Prueba: ExternalLoginCascadeRegressionTests.cs:128-140
31. Correo no verificado no fusiona -- misma rama 3 -- Prueba: ExternalLoginCascadeRegressionTests.cs:128-150
32. Vincular proveedor adicional desde sesion -- mismo codigo que el escenario 5 -- Prueba: ExternalLoginLinkingTests.cs:60-75

### user-management-permissions-audit (3/3 conformes)

33. Nombre visible LinkedProvider -- Codigo: AuditService.cs:123 ("Vinculacion de Proveedor") -- Prueba: UserManagementAndAuditServiceTests.cs:336-342
34. Nombre visible UnlinkedProvider -- Codigo: AuditService.cs:124 -- Prueba: UserManagementAndAuditServiceTests.cs:344-352
35. Valores nuevos no colisionan -- Codigo: AuditAction.cs:51,56 (ordinales 8 y 9) -- Prueba: UserManagementDomainTests.cs:184-192

No se acepto la matriz de trazabilidad de tasks.md como prueba: cada fila de esta tabla se comprobo leyendo el codigo de produccion y el cuerpo real de la prueba citada, no la afirmacion del documento de tareas.

---

## 3. Criterios de aceptacion de la propuesta (parrafo 11)

Nota factual: proposal.md, seccion 11, tiene 11 casillas, no 10. Las 10 primeras son automatizables; la 11a es el smoke test de navegador. Se verifican las 11.

1. /cuenta/conexiones accesible solo con sesion, anonimo redirigido -- Cumplido -- AccountConnections.razor:2, pipeline INC-46
2. Vincular asocia al UserId de sesion, nunca crea cuenta nueva -- Cumplido -- ExternalLoginService.LinkAsync nunca llama _users.AddAsync
3. Imposible desvincular el ultimo metodo, comprobado en servidor -- Cumplido -- ExternalLoginService.cs:247-250, prueba con llamada directa
4. Colision en login no fusiona en silencio -- Cumplido -- rama 2b + catch en ExternalLoginEvents.cs:112-121
5. Correo verificado + cuenta sin proveedores sigue vinculando (rama 2 intacta) -- Cumplido -- rama 2a preservada, regresion verificada prueba a prueba
6. Aviso en conexiones y cabecera, nunca en perfil publico -- Cumplido -- ver escenarios 16-19
7. ProviderEmailVerifiedAt via migracion aditiva y reconciliador SQLite -- Cumplido -- ver escenarios 20-21
8. Reemplazo del correo sintetico con guarda de indice unico -- Cumplido -- ver escenarios 24-26
9. Vincular/desvincular auditados con nombre visible -- Cumplido -- ver escenarios 13-15, 33-34
10. dotnet test Ludeka.sln en verde sin regresion sobre las 1345 -- Cumplido -- 1417/1417, ExternalLoginServiceTests.cs sin diff
11. Smoke test de navegador real (vincular->desvincular->denegacion) -- Pendiente, correctamente declarado -- tasks.md:156 sin marcar; no automatizable sin credenciales OAuth reales en el entorno; pasos exactos documentados en apply-progress.md (PR #4)

---

## 4. Las seis conductas criticas

1. Imposible desvincular el ultimo metodo, en servidor. ExternalLoginService.UnlinkAsync (ExternalLoginService.cs:229-259) evalua links.Count <= 1 y lanza LastAccessMethodException antes de llamar a RemoveAsync. La prueba UnlinkAsync_WhenAccountHasOnlyOneLink_ShouldDenyEvenWhenCalledDirectlyBypassingTheInterface (ExternalLoginLinkingTests.cs:219-234) invoca el metodo directamente, sin pasar por ningun boton. Confirmado en servidor, no solo en la interfaz.

2. Vincular desde sesion nunca crea cuenta nueva. LinkAsync (ExternalLoginService.cs:109-164) solo llama a RequireActiveUserAsync (que exige una cuenta ya existente y activa) y a _externalLogins.AddAsync; en ningun punto de la funcion se invoca _users.AddAsync. Confirmado por lectura completa del metodo.

3. El userId de la vinculacion se lee exclusivamente de los claims de sesion. Endpoint (Program.cs:511): httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier); el unico dato leido del formulario es form["provider"] (Program.cs:517-520), nunca un userId. En el retorno OAuth, ExternalLoginEvents.cs:65,70 reconfirma contra context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier). Busqueda dirigida sin hallazgos de lectura alternativa.

4. La rama 2b de ResolveAsync no escribe nada. ExternalLoginService.cs:72-80: si matchLinks.Count > 0, se lanza ExternalLoginCollisionException inmediatamente, sin ningun AddAsync previo. Prueba VerifiedEmailMatchingAccountWithExistingLinks_ShouldThrowCollisionWithoutCreatingRows (ExternalLoginCascadeRegressionTests.cs:90-107) afirma recuento invariante de AppUsers y ExternalLogins. Confirmado: cero escrituras.

5. La fila ExternalLogin jamas cambia de UserId: verificado en IExternalLoginRepository. El contrato (IExternalLoginRepository.cs) expone exactamente GetByProviderKeyAsync, AddAsync, ListByUserIdAsync, RemoveAsync. No existe ningun metodo de actualizacion. Sigue siendo cierto tras las 7 rondas de PRs.

6. El reemplazo del correo sintetico es de mejor esfuerzo. TryReplacePlaceholderEmailAsync (ExternalLoginService.cs:171-202) se invoca despues de que AddAsync de ExternalLogin ya tuvo exito (linea 152, tras el try/catch de las lineas 136-147); si falla por colision (owner is not null) o por carrera (catch DuplicateUserEmailException), devuelve false sin deshacer la vinculacion. LinkAsync sigue devolviendo ExternalLoginLinkOutcome.Linked. Prueba LinkAsync_WhenVerifiedEmailAlreadyOwnedByAnotherAccount_ShouldKeepPlaceholderEmailButStillLink (ExternalLoginLinkingTests.cs:168-184) lo confirma expresamente.

7. Las 6 pruebas de ExternalLoginServiceTests.cs no se modificaron en toda la cadena. git diff 01b4b73..HEAD --name-only -- tests/Ludeka.UnitTests/Application/ExternalLoginServiceTests.cs devuelve salida vacia. Confirmado.

---

## 5. Las cuatro correcciones fuera del plan

1. src/Ludeka.Application no referencia EF Core. grep -rn "EntityFrameworkCore" src/Ludeka.Application --include=*.cs --include=*.csproj devuelve salida vacia (verificado literalmente en esta fase). El patron correcto esta en su lugar: ExternalLoginRepository.AddAsync (Infrastructure, ExternalLoginRepository.cs:31-48) traduce DbUpdateException a DuplicateExternalLoginException; SqliteUserRepository.UpdateAsync (Infrastructure, SqliteUserRepository.cs:76-109) traduce DbUpdateException a DuplicateUserEmailException. ExternalLoginService (Application) solo conoce las excepciones de dominio.

2. La cache de AccountConnectionsService ya no sirve datos obsoletos. IAccountConnectionsService.InvalidateCache()/Invalidated (IAccountConnectionsService.cs:33,38), implementados en AccountConnectionsService.cs:46-50. AccountConnections.razor.UnlinkAsync invoca Connections.InvalidateCache() tras un UnlinkAsync exitoso (AccountConnections.razor:168) y AccountEmailNotice.razor se suscribe en OnAfterRender bajo RendererInfo.IsInteractive y se da de baja en Dispose() (AccountEmailNotice.razor:48-59,79-85). Confirmado con prueba de comportamiento real: GetConnectionsAsync_AfterInvalidateCache_ShouldReturnFreshDataFromTheRepository (AccountConnectionsServiceTests.cs:147-171).

3. El catch de colision del camino normal de login tiene prueba. tests/Ludeka.UnitTests/Web/ExternalLoginEventsNormalLoginBranchTests.cs existe (130 lineas, 2 pruebas) y ejercita exactamente el catch (ExternalLoginCollisionException) de ExternalLoginEvents.cs:112-121: confirma HandleResponse(), StatusCode 302, Location exacto y ausencia de Set-Cookie. Hallazgo de esta fase: este fichero de prueba (commit 59e5260, posterior al cierre documental de PR #5) no esta mencionado en ninguna seccion de apply-progress.md, que en su seccion de PR #5 declaraba explicitamente lo contrario ("sin prueba dedicada... riesgo residual explicito"). El codigo y la prueba son correctos y pasan; el hallazgo es de trazabilidad documental, no funcional (ver SUGGESTION-1 en la seccion 6).

4. Dos conflictos de ChangeTracker corregidos. ExternalLoginRepository.RemoveAsync (ExternalLoginRepository.cs:60-71): busca una entrada ya rastreada por Id y la elimina en vez de adjuntar una segunda instancia. SqliteUserRepository.UpdateAsync (SqliteUserRepository.cs:86-96): usa CurrentValues.SetValues(user) sobre la entrada rastreada existente. Ambos verificados por lectura directa del codigo.

---

## 6. Hallazgos nuevos de esta fase

### WARNING - Camino de error no cubierto: cuenta suspendida/eliminada a mitad del flujo de vinculacion

ExternalLoginEvents.HandleTicketReceivedAsync (ExternalLoginEvents.cs:37-122) no contiene ningun catch para UnauthorizedAccessException (verificado: grep "catch (UnauthorizedAccessException" ExternalLoginEvents.cs sin resultados). Si entre el instante en que se emite el desafio de vinculacion (POST /cuenta/conexiones/vincular) y el retorno del proveedor OAuth la cuenta de la sesion se suspende o se elimina, ExternalLoginService.LinkAsync -> RequireActiveUserAsync (ExternalLoginService.cs:267-277) lanza UnauthorizedAccessException, que se propaga sin capturar desde la rama de vinculacion de HandleTicketReceivedAsync (lineas 79-94). El resultado observable depende del entorno: en produccion, Program.cs:394 tiene app.UseExceptionHandler("/Error", ...), asi que degrada a la pagina de error generica en vez de a un mensaje accionable; en desarrollo, sin ese middleware activo, mostraria la pagina de excepcion de desarrollo. No es una brecha de seguridad ni de integridad de datos (la cookie de sesion de quien vuelve del proveedor no se sustituye, no se crea ninguna fila), pero rompe el patron de degradacion controlada (redireccion con mensaje fijo) que el resto de este incremento si sigue para cada rama de fallo (sesion ausente, sesion distinta, resultado no vinculado). Ninguna de las pruebas de ExternalLoginEventsLinkBranchTests.cs ejercita este caso. Ningun artefacto de diseno, tareas o apply-progress.md lo menciona.

Recomendacion: capturar UnauthorizedAccessException en la rama de vinculacion y redirigir con un codigo cerrado nuevo (mismo idioma que las otras dos ramas de fallo de D1), en un incremento de mantenimiento posterior. No bloquea el archivado: es un borde de baja probabilidad (exige una accion administrativa exactamente durante la ventana del consentimiento OAuth) con degradacion no catastrofica.

### SUGGESTION-1 - ExternalLoginEventsNormalLoginBranchTests.cs no documentado en apply-progress.md

Ver punto 3 de la seccion 5. El codigo y la prueba son correctos; falta unicamente la entrada narrativa en apply-progress.md que reconcilie la afirmacion previa ("riesgo residual, ningun test lo detectaria") con el estado real actual. Recomendacion: anadir una linea en la seccion de PR #5 (o una nota de cierre) que referencie el commit 59e5260.

### SUGGESTION-2 - Discrepancia de recuento en la instruccion de verificacion

El encargo de esta fase asumia 10 criterios de aceptacion en proposal.md, seccion 11 ("Los diez"). El documento real tiene 11 casillas (la 11a es el smoke test de navegador). Se verificaron las 11 en la seccion 3; no afecta al veredicto, se deja anotado para que el proximo encargo cuente correctamente.

---

## 7. Recomendacion final

El incremento esta funcionalmente completo y verificado: 1417/1417 pruebas en verde, 35/35 escenarios de especificacion conformes con evidencia directa de codigo y prueba, 10/11 criterios de aceptacion automatizables cumplidos (el 11o, manual, correctamente pendiente), las seis conductas de seguridad que son la razon de ser del incremento confirmadas por lectura de codigo y prueba dirigida, y las cuatro correcciones introducidas fuera del plan verificadas como correctas y no regresivas.

El unico WARNING (camino de error sin cobertura ante suspension/eliminacion de cuenta a mitad de vinculacion) es un borde de baja probabilidad con degradacion no catastrofica gracias al manejador de excepciones global ya existente; no compromete ninguna de las garantias de seguridad centrales del incremento (nunca se crea una cuenta indebida, nunca se firma una sesion ajena, nunca se pierde una fila). No constituye, a mi juicio, un motivo para bloquear el merge ni el archivado, pero debe registrarse como deuda tecnica de seguimiento.

El cambio esta listo para mergearse y archivarse.

## Key Learnings

1. La comprobacion previa (GetByProviderKeyAsync) mas el indice unico con traduccion de excepcion en Infrastructure implementan una doble barrera contra condiciones de carrera sin que Application conozca el ORM.
2. Un servicio Scoped con cache de instancia en Blazor Server vive todo el circuito, no una sola peticion; sin invalidacion explicita y un evento de notificacion, los suscriptores del mismo circuito ven estado obsoleto tras una escritura.
3. Ausencia de bUnit/WebApplicationFactory en un repositorio implica que todas las pruebas de paginas Blazor son de contrato de fuente, no de comportamiento renderizado; es una limitacion de cobertura preexistente, no especifica de este incremento.
4. Un catch que traduce excepciones de un caso de uso (ResolveAsync) puede quedar sin equivalente en una rama hermana (LinkAsync dentro del mismo manejador de evento) si no se revisa explicitamente cada rama de fallo nueva contra cada excepcion que el servicio de aplicacion puede lanzar.
