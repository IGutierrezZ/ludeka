# Diseño Técnico — INC-54: Padrón Exhaustivo y Mecanismo de Carga del Directorio Lúdico Español

## 1. Arquitectura de Datos (`seed-directory.json`)

El archivo se almacena en `src/Ludeka.Infrastructure/Seeding/seed-directory.json` y se declara como `<EmbeddedResource>` en `Ludeka.Infrastructure.csproj`.

Estructura JSON:
```json
{
  "publishers": [
    {
      "name": "Devir Iberia",
      "slug": "devir-iberia",
      "country": "España",
      "city": "Barcelona",
      "description": "Editorial decana del juego de mesa moderno en español...",
      "logoUrl": "/images/publishers/devir.png",
      "websiteUrl": "https://devir.es",
      "socialLinks": [
        { "platform": "Website", "url": "https://devir.es" },
        { "platform": "YouTube", "url": "https://youtube.com/@devirtv", "handle": "@devirtv", "title": "Devir TV" },
        { "platform": "Instagram", "url": "https://instagram.com/deviriberia", "handle": "@deviriberia" }
      ]
    }
  ],
  "stores": [
    {
      "name": "Zacatrus!",
      "slug": "zacatrus",
      "type": "Hybrid",
      "country": "España",
      "city": "Madrid",
      "address": "Calle Fernández de los Ríos 57",
      "description": "Cadena de tiendas físicas y tienda online referente...",
      "logoUrl": "/images/stores/zacatrus.png",
      "websiteUrl": "https://zacatrus.es",
      "affiliateCode": "LDKZAC",
      "hasLoyaltyProgram": true,
      "socialLinks": [ ... ],
      "shippingCountries": ["España", "Internacional"]
    }
  ],
  "creators": [
    {
      "name": "Análisis Parálisis",
      "slug": "analisis-paralisis",
      "nationality": "España",
      "bio": "Referente absoluto en reseñas, partidas y directos lúdicos...",
      "avatarUrl": "/images/creators/analisis-paralisis.png",
      "websiteUrl": "https://analisisparalisis.es",
      "socialLinks": [ ... ]
    }
  ]
}
```

## 2. Reconciliador y Seeder (`DirectorySeeder.cs`)

### 2.1 Lectura de Recurso con Fallback
`DirectorySeeder.ReadSeedJson()` consulta:
1. Ensamblado: `assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("seed-directory.json"))`.
2. Disco (binario de ejecución): `Path.Combine(AppContext.BaseDirectory, "Seeding", "seed-directory.json")`.
3. Disco (código fuente local): `Path.Combine(Directory.GetCurrentDirectory(), "src", "Ludeka.Infrastructure", "Seeding", "seed-directory.json")`.

### 2.2 Reconciliación Idempotente
- `SeedPublishersAsync`: Si la editorial no existe, se añade. Si existe, se enriquecen campos nulos o vacíos (como `LogoUrl`, `WebsiteUrl`, `City`, `Description`) y se agregan los enlaces sociales canónicos que no estuvieran ya presentes.
- `SeedStoresAsync`: Idem para tiendas.
- `SeedCreatorsAsync`:
  1. Purga quirúrgica de la lista cerrada de diseñadores de juegos de mesa retirados (`RetiredSeedCreatorSlugs`).
  2. Re-siembra aditiva del padrón: añade los creadores que no existan por slug y enriquece los existentes si carecen de datos clave.
  3. No toca los creadores que hayan sido añadidos manualmente con slugs distintos.

### 2.3 Objeto de Resultado
`DirectorySeedResult(int SeededPublishers, int SeededStores, int SeededCreators, int TotalActivePublishers, int TotalActiveStores, int TotalActiveCreators)`.

## 3. Mecanismos de Disparo y Carga

### 3.1 CLI Runner (`Ludeka.Jobs`)
- Constante: `JobNames.SeedDirectory = "seed-directory"`.
- Clase: `SeedDirectoryJobRunner : IJobRunner`.
- Resuelve `IDbContextFactory<LudekaDbContext>` para ejecutar en un ámbito aislado y sin colisiones de DbContext.
- Comando:
  ```powershell
  dotnet run --project src/Ludeka.Jobs -- seed-directory
  ```

### 3.2 Botón de Administración Web (`CatalogQueueAdmin.razor`)
- Nueva tarjeta en el panel de administración para "Directorio del Ecosistema Lúdico".
- Botón "Sincronizar Padrón del Directorio".
- Restringido a usuarios con permiso `CanManagePublishers`, `CanManageCreators`, `CanManageStoreLinks` o Miembros Fundadores.
- Estado reactivo `_isSeedingDirectory` con indicador de carga y banner de resultado al terminar.

### 3.3 Arranque en Desarrollo (`Program.cs`)
- Mantiene la llamada automática `await DirectorySeeder.SeedDirectoryAsync(db);` cuando `SeedDemoData` esté activo.

## 4. Integración con Monitorización Multimedia
El servicio `ChannelDirectoryProvider` de INC-19 ya lee dinámicamente de `IPublisherRepository`, `IStoreRepository` e `ICreatorRepository` buscando enlaces de `SocialPlatform.YouTube`. Al incorporar los canales oficiales de las editoriales, tiendas y creadores, `ChannelFocusProvider` los detecta automáticamente otorgándoles las bonificaciones de prioridad editorial correspondientes.
