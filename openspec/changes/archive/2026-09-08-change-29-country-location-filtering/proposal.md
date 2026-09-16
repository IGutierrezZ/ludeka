# Propuesta de Cambio: change-29-country-location-filtering

## 1. Resumen Ejecutivo
El **Incremento 29** dota a Ludeka de un sistema integral de localización territorial y filtrado geográfico por país para contenidos comerciales y presenciales (tiendas físicas y online con envíos locales, eventos presenciales y sorteos con restricciones territoriales):
1. **Catálogo de Países y Dominio Estandarizado (`CountryCatalog`):**
   - Países hispanohablantes de referencia (España 🇪🇸, México 🇲🇽, Argentina 🇦🇷, Chile 🇨🇱, Colombia 🇨🇴, Perú 🇵🇪, Uruguay 🇺🇾) más la opción especial **Internacional / Global 🌎** (para sorteos mundiales o ferias internacionales como SPIEL Essen).
2. **Entidades con Soporte de País:**
   - **`Publisher`:** Validación y normalización del país existente.
   - **`Store`:** Incorporación de país sede (`Country`) y soporte de países de envío (`ShippingCountries` / envíos multi-país).
   - **`Giveaway`:** Ámbito territorial del sorteo (`Country`), garantizando que los sorteos "Internacional" sean visibles para todos los países.
   - **`BoardGameEvent`:** País de celebración (`Country`) manteniendo `Location` para ciudad/recinto.
   - **`GamePurchaseLink`:** País de la oferta o territorio de cobertura (`Country`), permitiendo asociar cada enlace de compra de juegos o fundas a su territorio.
   - **`AppUser` y `UserPreference`:** País preferido del usuario (`Country`) con persistencia en base de datos.
3. **Preferencia de Usuario y Advertencia Explícita:**
   - En el perfil de usuario se habilita el selector de país ("Todos los países" / Sin especificar, o país concreto).
   - **Aviso destacado obligatorio:** Al interactuar con el selector, se advierte al usuario:
     > *"⚠️ Al seleccionar un país, los sorteos, eventos y tiendas se filtrarán automáticamente para mostrar únicamente los disponibles en tu territorio. Si prefieres explorar toda la información de la comunidad global sin filtrar, déjalo sin seleccionar."*
4. **Filtrado Territorial Estricto y Estados Vacíos en Compras:**
   - En la sección "Dónde Comprar" de juegos y fundas (`StoreOffersCard.razor`): si el usuario tiene país seleccionado, solo se muestran ofertas de tiendas que envíen o pertenezcan a ese país.
   - **Regla estricta:** Si para ese país no existe ninguna tienda vinculada con stock o envíos para ese juego o fundas, no se muestran tiendas de otros países; en su lugar, se presenta un estado vacío informativo (*"Actualmente no hay tiendas colaboradoras con envíos a {País} para este juego o sus fundas"*).
   - Marcado de badge de país en tarjetas de sorteos, tiendas, eventos y ofertas de compra.
5. **Detección de Ubicación y Ordenación Priorizada:**
   - Servicio client-side de geolocalización e inferencia por zona horaria.
   - Si el usuario no tiene filtro estricto pero tiene ubicación detectada o activa *"Priorizar por mi país"*, los sorteos, eventos y tiendas se ordenan situando en primer lugar los de su territorio.

---

## 2. Justificación y Valor
- **Relevancia Comercial y Comunitaria:** Los juegos de mesa implican objetos físicos con costes de aduanas y envío transfronterizos. Mostrar a un usuario mexicano tiendas españolas sin envíos a México genera frustración.
- **Legalidad de Sorteos:** Los sorteos en redes suelen limitarse territorialmente por costes de envío o normativas fiscales.
- **Respeto a la Comunidad Global:** Quien desee ver todo el ecosistema hispanohablante puede mantener su perfil sin país y beneficiarse de la ordenación priorizada sin perder visibilidad global.

---

## 3. Decisiones Arquitectónicas Confirmadas por el Usuario
1. **Sorteos e Información Internacional:** Los elementos con ámbito "Internacional" son siempre visibles en cualquier país.
2. **Tiendas Multi-País:** Si una tienda realiza envíos a múltiples países (o ámbito internacional), es visible en todos los países correspondientes.
3. **Advertencia de Filtrado:** Aviso explícito en el perfil para evitar que el usuario piense que la plataforma carece de contenidos cuando simplemente están filtrados por territorio.
4. **Detección sin Fricción:** Inferencia rápida por zona horaria de navegador con posibilidad de geolocalización precisa opcional.
