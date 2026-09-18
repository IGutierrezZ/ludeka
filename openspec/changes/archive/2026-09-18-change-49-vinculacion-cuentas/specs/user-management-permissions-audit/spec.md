# Delta for user-management-permissions-audit

> Cambio `change-49-vinculacion-cuentas` (INC-49). Añade dos valores de `AuditAction` para registrar la vinculación y desvinculación de identidades externas.

## ADDED Requirements

### Requirement: Acciones de auditoría para vinculación y desvinculación de proveedores externos

El enumerado `AuditAction` DEBE incluir los valores `LinkedProvider` y `UnlinkedProvider`. La bitácora DEBE resolver, para cada uno de estos valores, un nombre visible en español distinto del texto genérico reservado a una acción sin nombre reconocido.

#### Scenario: Nombre visible de LinkedProvider

- GIVEN una entrada de auditoría con `Action=LinkedProvider`
- WHEN se solicita su nombre visible
- THEN se obtiene un texto en español específico para la vinculación, distinto del texto genérico de acción desconocida.

#### Scenario: Nombre visible de UnlinkedProvider

- GIVEN una entrada de auditoría con `Action=UnlinkedProvider`
- WHEN se solicita su nombre visible
- THEN se obtiene un texto en español específico para la desvinculación, distinto del texto genérico de acción desconocida.

#### Scenario: Los valores nuevos no colisionan con los existentes

- GIVEN el enumerado `AuditAction` ampliado con `LinkedProvider` y `UnlinkedProvider`
- WHEN se enumeran todos sus valores
- THEN cada uno de los dos valores nuevos es distinto entre sí y de todos los valores ya definidos previamente.
