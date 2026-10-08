# Diseño Arquitectónico: INC-142

## 1. Mapeo y Normalización de Galería Magento en Maldito Games
Maldito Games utiliza el mismo patrón de componentes Magento 2 que Devir para su galería fotográfica:
```html
<script type="text/x-magento-init">
{
    "[data-gallery-role=gallery-placeholder]": {
        "mage/gallery/gallery": {
            "data": [
                {
                    "img": "https://devirinvestments.s3.eu-west-1.amazonaws.com/img/catalog/product/8436578819720-1200-face3d.jpg",
                    "full": "...",
                    "isMain": true
                },
                {
                    "img": "...-frontflat.jpg",
                    "isMain": false
                },
                {
                    "img": "...-backflat.jpg",
                    "isMain": false
                }
            ]
        }
    }
}
</script>
```

### Reglas de Clasificación de Medios:
1. `CoverImageUrl`:
   - URL que contenga `face3d`, `3d` o `caja`.
   - Si no existe ningún marcador 3D explícito, el elemento que tenga `isMain == true`.
2. `TableImageUrl`:
   - URL que contenga `components`, `mesa` o `contenido`.
   - Si no existe y hay `frontflat` disponible, usar `frontflat` como recurso gráfico secundario para no perder la vista plana de caja.
3. `BackCoverImageUrl`:
   - URL que contenga `backflat`, `trasera` o `contra`.
4. `Ean`:
   - Extraído prioritariamente del atributo de formulario `data-product-sku="(\d{13})"` o del nombre de archivo `(\d{13})`.
5. `Pvp`:
   - Extraído de `<meta property="product:price:amount" content="(?<price>[^"]+)">` o del elemento `.price`.

## 2. Inyección de Ofertas en `PurchaseLinks`
Cuando un juego se enriquece desde el catálogo o ficha de una editorial oficial (Devir o Maldito Games):
- Se comprueba si ya existe en `matchedGame.PurchaseLinks` una oferta con el mismo `StoreName`.
- Si no existe, se añade:
  ```csharp
  new GamePurchaseLink(
      storeName: storeName,
      affiliateUrl: item.ProductUrl,
      price: gallery.Pvp.Value,
      currency: "€",
      inStock: true,
      country: "España");
  ```
- Si existe, se actualiza la lista mediante `matchedGame.UpdatePurchaseLinks(...)` asegurando que el precio y la URL apunten a los datos frescos de la editorial.

## 3. Manejo Resiliente de Cloudflare para Ejecución Local
Para garantizar que tanto `devir-images-backfill` como `maldito-images-backfill` funcionen con máxima fiabilidad al ejecutarse en local:
- Se envían cabeceras completas de navegador de escritorio (`sec-ch-ua`, `User-Agent` de Chrome, `Accept`, `Accept-Language`, `Referer`).
- Se introducen pausas de cortesía (250 ms entre fichas de producto, 750 ms entre páginas de catálogo).
- Se implementa reintento defensivo con espera de 1,5 segundos en caso de HTTP 403, 429 o 5xx.
- Si una página puntual de catálogo falla tras los reintentos, el runner registra advertencia y continúa con las siguientes páginas sin abortar el trabajo completo a menos que ocurran fallos consecutivos reiterados.
