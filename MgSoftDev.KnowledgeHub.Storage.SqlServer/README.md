# MgSoftDev.KnowledgeHub.Storage.SqlServer

Storage provider **SQL Server** para KnowledgeHub. Las tablas del módulo viven dentro de la
base de datos anfitriona, en un esquema propio (default `kh`) y/o con prefijo de tabla.

```csharp
services.AddKnowledgeHubSqlServerStore(connectionString,
    o => { o.Schema = "kh"; o.TablePrefix = ""; });
```

## Instalación de las tablas

El DDL es un script idempotente embebido (UTF-8 con BOM), parametrizado por schema/prefijo:

```csharp
// Opción A: aplicarlo al arrancar (la BD debe existir)
await KnowledgeHubSqlSchema.EnsureDatabaseObjectsAsync(connectionString, options);

// Opción B: obtener el script y ejecutarlo en tu pipeline de migraciones/DBA
var script = KnowledgeHubSqlSchema.GetCreateScript(options).Value;
```

Las PKs se generan en cliente (`Guid.CreateVersion7()`); el DDL conserva
`DEFAULT NEWSEQUENTIALID()/GETDATE()` solo como red de seguridad para INSERTs manuales.
Soporta edición concurrente multi-equipo (la publicación es transaccional con detección de
conflicto optimista).
