# Delta for user-library-view

## MODIFIED Requirements

### Requirement: Navegación por Pestañas de Estado

La página DEBE organizar los juegos del usuario en pestañas reactivas con ruta canónica `/cuenta/ludoteca` (sección del hub) y alias permanente `/mi-ludoteca` de idéntico comportamiento:
1. *En mi ludoteca (N)*
2. *Jugados (N)*
3. *Deseados (N)*
4. *Quiero comprar (N)*
5. *Préstamos activos (N)*
6. *Cola comunitaria (N)* (visible para usuarios con rol de moderador o equipo fundador).

(Previously: la ruta única era `/mi-ludoteca`; ahora la canónica es `/cuenta/ludoteca` y `/mi-ludoteca` queda como alias.)

#### Escenario: Renderizado de contadores por pestaña

- DADO un usuario con 3 juegos en `InCollection`, 5 en `Played`, 2 en `Wishlist`, 1 en `WantToBuy` y 1 préstamo activo
- CUANDO el usuario navega a `/cuenta/ludoteca`
- ENTONCES las pestañas DEBEN mostrar los conteos numéricos exactos `(3)`, `(5)`, `(2)`, `(1)` y `(1)`.

#### Escenario: Cambio de pestaña reactivo

- DADO el usuario en `/cuenta/ludoteca`
- CUANDO pulsa la pestaña *Deseados*
- ENTONCES la vista DEBE mostrar exclusivamente los juegos clasificados como `Wishlist`.

#### Escenario: Alias `/mi-ludoteca` con idéntico comportamiento

- DADO el alias `/mi-ludoteca` vivo
- CUANDO el usuario navega a `/mi-ludoteca`
- ENTONCES recibe exactamente la misma vista, pestañas y contadores que en `/cuenta/ludoteca`.

---

El resto de requerimientos de la capacidad (préstamos, importación BGG, badge de cola y panel de moderación) permanecen INTACTOS y no se reproducen aquí.
