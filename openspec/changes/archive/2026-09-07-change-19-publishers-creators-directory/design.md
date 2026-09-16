# Diseño Técnico: change-19-publishers-creators-directory

## 1. Arquitectura de Componentes y Flujo de Datos

```
[ Ludeka.Web ]
   ├── Directoriosectoriales: /editoriales, /creadores, /tiendas
   ├── Fichas de Detalle: /editoriales/{slug}, /creadores/{slug}, /tiendas/{slug}
   ├── Modales de Moderación: PublisherEditModal, CreatorEditModal, StoreEditModal
   └── Enlaces Cruzados: GameDetail.razor, StoreOffersCard.razor
          │
          ▼
[ Ludeka.Application ]
   ├── IPublisherService / PublisherService
   ├── ICreatorService / CreatorService
   ├── IStoreService / StoreService
   ├── IChannelDirectoryProvider / ChannelDirectoryProvider
   └── DTOs & Mapping
          │
          ▼
[ Ludeka.Infrastructure ]
   ├── SqlitePublisherRepository / IPublisherRepository
   ├── SqliteCreatorRepository / ICreatorRepository
   ├── SqliteStoreRepository / IStoreRepository
   ├── ChannelFocusProvider (enriquecido con IChannelDirectoryProvider)
   ├── LudekaDbContext (DbSets & JSON OwnsMany)
   └── DirectorySeeder (sembrado canónico)
          │
          ▼
[ Ludeka.Core ]
   ├── Entidades: Publisher, Creator, Store
   ├── Enums: SocialPlatform, StoreType
   └── Value Object: SocialNetworkLink
```

---

## 2. Definición Detallada de Contratos e Interfaces

### 2.1 Dominio (`Ludeka.Core`)
```csharp
namespace Ludeka.Core.Enums;

public enum SocialPlatform
{
    Website,
    YouTube,
    Instagram,
    Twitter,
    Discord,
    Facebook,
    BoardGameGeek,
    Twitch,
    TikTok,
    Other
}

public enum StoreType
{
    PhysicalOnly,
    OnlineOnly,
    Hybrid
}
```

```csharp
namespace Ludeka.Core.ValueObjects;

public record SocialNetworkLink(
    SocialPlatform Platform,
    string Url,
    string? Handle = null,
    string? Title = null
);
```

```csharp
namespace Ludeka.Core.Entities;

public class Publisher
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string? City { get; private set; }
    public string? Description { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public List<SocialNetworkLink> SocialLinks { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    
    // Métodos de negocio: UpdateDetails, AddOrUpdateSocialLink, ClearSocialLinks
}

public class Creator
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Nationality { get; private set; }
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
    public int? BggPersonId { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public List<SocialNetworkLink> SocialLinks { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
}

public class Store
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public StoreType Type { get; private set; }
    public string? City { get; private set; }
    public string? Address { get; private set; }
    public string? Description { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public string? AffiliateCode { get; private set; }
    public bool HasLoyaltyProgram { get; private set; }
    public List<SocialNetworkLink> SocialLinks { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
}
```

### 2.2 Aplicación (`Ludeka.Application`)
```csharp
namespace Ludeka.Application.Contracts;

public interface IPublisherRepository
{
    Task<IReadOnlyList<Publisher>> GetAllAsync(CancellationToken ct = default);
    Task<Publisher?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Publisher?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Publisher?> GetByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(Publisher publisher, CancellationToken ct = default);
    Task UpdateAsync(Publisher publisher, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICreatorRepository
{
    Task<IReadOnlyList<Creator>> GetAllAsync(CancellationToken ct = default);
    Task<Creator?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Creator?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Creator?> GetByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(Creator creator, CancellationToken ct = default);
    Task UpdateAsync(Creator creator, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IStoreRepository
{
    Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken ct = default);
    Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Store?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Store?> GetByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(Store store, CancellationToken ct = default);
    Task UpdateAsync(Store store, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IChannelDirectoryProvider
{
    Task<IReadOnlyList<ChannelFocusEntry>> GetDynamicReferenceChannelsAsync(CancellationToken ct = default);
}
```

### 2.3 Integración con el Motor de YouTube (INC-14)
`ChannelFocusProvider` inyecta `IServiceScopeFactory` (o `IChannelDirectoryProvider`) de forma desacoplada para evaluar en tiempo real si un canal de YouTube coincide con alguno de los canales registrados en las entidades `Publisher`, `Creator` o `Store`:
- Si coincide con una Editorial: categoría `ChannelCategory.Publisher` con bono algorítmico +50.
- Si coincide con un Creador: categoría `ChannelCategory.Creator` con bono algorítmico +65.
- Si coincide con una Tienda: categoría `ChannelCategory.Store` con bono algorítmico +55.

---

## 3. Persistencia y Mapeo en EF Core 10 (`LudekaDbContext`)
- Tablas SQLite: `Publishers`, `Creators`, `Stores`.
- Índices únicos por `Slug` e índices por `Name`.
- Mapeo de `SocialLinks` con `OwnsMany(p => p.SocialLinks, b => b.ToJson())`.

---

## 4. Estrategia de Sembrado Canónico (`DirectorySeeder`)
Se provisionan entidades iniciales para enriquecer el catálogo inmediatamente:
- **Editoriales:** Devir, Maldito Games, Tranjis Games, Asmodee Ibérica, Stonemaier Games.
- **Creadores:** Elizabeth Hargrave, Klaus Teuber, Uwe Rosenberg, Jacob Fryxelius, Bruno Cathala, Jamey Stegmaier, Sergio (Análisis Parálisis).
- **Tiendas:** Zacatrus!, Cuarto de Juegos, Dungeon Marvels, Jugamos Otra.

---

## 5. UI/UX y Componentes Blazor
- Vistas de directorio accesibles con diseño editorial y selector por filtros.
- Vistas de detalle con microtextos y tarjetas cruzadas con el catálogo de juegos.
- Modales para dar de alta y editar fichas restringidos a `Moderator` y `FoundingTeam`.
