# Diseño Arquitectónico: INC-73 — Curación y Saneamiento Integral del Directorio Lúdico Español

## 1. Arquitectura de Datos del Padrón Canónico

El archivo canónico reside en:
`src/Ludeka.Infrastructure/Seeding/seed-directory.json`
configurado como `<EmbeddedResource Include="Seeding\seed-directory.json" />` en `Ludeka.Infrastructure.csproj`.

### 1.1 Esquema Normalizado de Entidad
Para evitar la redundancia detectada en la auditoría inicial, la estructura de cada entidad sigue este contrato:
- `websiteUrl`: Dirección web principal oficial (o `null` si la entidad no dispone de dominio propio o este expiró).
- `socialLinks`: Colección de objetos `{ "platform": "...", "url": "...", "handle": "...", "title": "..." }`.
  - Queda prohibida la inclusión de `platform: "Website"` en `socialLinks` cuando la entidad ya especifica `websiteUrl`.
  - Se admiten únicamente plataformas reales: `YouTube`, `Instagram`, `Twitter` (o `X`), `Twitch`, `TikTok`, `Podcast`/`iVoox`, `Facebook`.

### 1.2 Reglas de Saneamiento y Curación

1. **Editoriales:**
   - Normalización de URLs con redirecciones directas o subdominios necesarios (`https://www.masqueoca.com`, `https://es.asmodee.com`).
   - Saneamiento de microeditoriales con dominios caducados (ej. redirigir a sus páginas de Verkami/Kickstarter o redes oficiales si el dominio expiró).
   - Verificación de canales de YouTube para que el buscador multimedia agregue contenido oficial.

2. **Tiendas Especializadas:**
   - Corrección de dominios desalineados (ej. `Nostromo Cómics` -> `https://nostromocomics.com`).
   - Verificación de tiendas con cierre o cambio de web comercial.
   - Preservación de códigos de afiliación e información de fidelización y envíos.

3. **Creadores de Contenido:**
   - Revisión minuciosa de los canales de YouTube (`@handle`), garantizando que correspondan con los canales lúdicos en español reales.
   - Corrección de portales informativos (`doctormeeple.es`, `elclubdante.es`).
   - Retirada de enlaces a cuentas inactivas o suspendidas en Twitter/X.

---

## 2. Compatibilidad e Idempotencia con `DirectorySeeder`

El reconciliador `DirectorySeeder.cs` opera con la siguiente lógica:
```csharp
// Busca por Slug invariante
var existingPublisher = await db.Publishers.FirstOrDefaultAsync(p => p.Slug == item.Slug);
if (existingPublisher == null) {
    db.Publishers.Add(item);
} else {
    // Actualización idempotente sin alterar IDs ni relaciones preexistentes
    existingPublisher.WebsiteUrl = item.WebsiteUrl;
    existingPublisher.Description = item.Description;
    existingPublisher.SocialLinks = item.SocialLinks;
    // ...
}
```
Al mantener los slugs estrictamente intactos:
- En bases de datos existentes, la sincronización enriquece y corrige los enlaces sin generar duplicados.
- En bases de datos nuevas (o entornos de prueba), se siembra el catálogo depurado desde el primer arranque.

---

## 3. Estrategia de Pruebas Unitarias

En `tests/Ludeka.UnitTests/Infrastructure/DirectorySeederTests.cs`:
1. **`DirectorySeeder_CanonicalJson_HasNoDuplicatedWebsitesInSocialLinks`**: Verifica que ninguna editorial, tienda o creador contenga en `socialLinks` una entrada duplicada de `websiteUrl`.
2. **`DirectorySeeder_CanonicalJson_ContainsExactEntityCounts`**: Comprueba que el censo mantenga las 46 editoriales, 37 tiendas y 35 creadores canónicos.
3. **`DirectorySeeder_CanonicalJson_AllSocialLinksHaveValidUrlsAndHttps`**: Comprueba que todos los enlaces sociales tengan formato URI válido y usen el protocolo `https://`.
4. **`DirectorySeeder_KeyEntities_HaveAccurateOfficialUrls`**: Certifica casos específicos de corrección (ej. Doctor Meeple en `.es`, Club Dante en `elclubdante.es`, Nostromo en `nostromocomics.com`, MasQueOca en `www.masqueoca.com`).
