# MgSoftDev.KnowledgeHub.Storage.LiteDb

Storage provider **LiteDB** para KnowledgeHub: persistencia en archivo, sin servidor de base
de datos. Ideal cuando la app anfitriona no tiene BD propia.

```csharp
services.AddKnowledgeHubLiteDbStore(@"C:\datos\knowledgehub.db",
    o => o.CollectionPrefix = "kh_");   // prefijo si compartes archivo con otras colecciones
```

Las colecciones se crean solas al primer uso (`kh_DocPages`, `kh_DocPageVersions`,
`kh_DocImages`, `kh_DocImageContents`, `kh_DocPages_DocImages`, `kh_DocPages_Permissions`)
con sus índices (slug único, hash único, versión única por página).

> ⚠️ **Single-process por diseño**: la instancia `LiteDatabase` es exclusiva (Direct mode).
> Para edición concurrente desde varios equipos usa el provider **SqlServer**.
> No compartas el mismo archivo .db con otro `LiteDatabase` del anfitrión — usa un archivo
> propio para el módulo (o el mismo archivo solo a través de este provider con prefijo).
