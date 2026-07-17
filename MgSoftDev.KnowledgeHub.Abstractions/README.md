# MgSoftDev.KnowledgeHub.Abstractions

Contratos, entidades y DTOs del módulo de documentación colaborativa **KnowledgeHub**.

Referencia este paquete cuando necesites:

- Implementar un **storage provider propio** (`IKnowledgeHubStore`) si los oficiales
  (SqlServer / LiteDb) no aplican a tu proyecto.
- Implementar el **contexto de usuario** del anfitrión (`IKnowledgeHubUserContext`):
  KnowledgeHub no tiene tablas de usuarios; tu app provee `UserName`, la lista de permisos
  efectivos y el catálogo de permisos para el selector de visibilidad.
- Consumir los contratos de servicio (`IKnowledgeHubPageService`, etc.) desde tu código.

## Permisos reservados

Incluye en `IKnowledgeHubUserContext.Permissions` los nombres well-known cuando el usuario
tenga la capacidad:

| Permiso | Capacidad |
|---|---|
| `KnowledgeHub.Admin` | Ve y puede todo |
| `KnowledgeHub.Edit` | Crear/editar/guardar borradores (y publicar/gestionar en modo por defecto) |
| `KnowledgeHub.Publish` | Publicar (solo con `KnowledgeHubOptions.UseFineGrainedPublish`) |
| `KnowledgeHub.ManagePermissions` | Gestionar visibilidad (solo con `UseFineGrainedManagePermissions`) |

La visibilidad por página se asigna con permisos arbitrarios de tu catálogo (strings).
