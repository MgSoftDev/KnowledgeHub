# MgSoftDev.KnowledgeHub.Storage.EntityFramework

Base **EF Core neutral** de los storage providers relacionales de KnowledgeHub. No se usa
directamente: instala el paquete del motor (hoy `MgSoftDev.KnowledgeHub.Storage.SqlServer`;
PostgreSQL en el futuro se apoyará en esta misma base).

Expone `KnowledgeHubDbContext` (modelo Fluent sin defaults de servidor — los valores se
generan en cliente), `KnowledgeHubEfModelOptions` (Schema / TablePrefix) y
`EfKnowledgeHubStore` (contextos cortos vía `IDbContextFactory` + `AsNoTracking`).
