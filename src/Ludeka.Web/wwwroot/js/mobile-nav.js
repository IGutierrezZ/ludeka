// Ludeka Mobile Nav (Cierre accesible por clic exterior, Escape y redimensionamiento)
(function () {
    function getMobileNav() {
        return document.querySelector('details[data-mobile-nav]') || document.getElementById('mobile-nav-details');
    }

    function closeNavIfOutside(target) {
        var nav = getMobileNav();
        if (nav && nav.hasAttribute('open')) {
            if (!nav.contains(target)) {
                nav.removeAttribute('open');
            }
        }
    }

    // Escucha en pointerdown para respuesta tactil inmediata sin interferir con la accion del objetivo
    document.addEventListener('pointerdown', function (e) {
        closeNavIfOutside(e.target);
    });

    // Escucha en click para eventos de teclado o disparadores sin puntero directo
    document.addEventListener('click', function (e) {
        closeNavIfOutside(e.target);
    });

    // Tecla Escape para cumplimiento WCAG 2.2 AA (cierre accesible y retorno de foco)
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            var nav = getMobileNav();
            if (nav && nav.hasAttribute('open')) {
                nav.removeAttribute('open');
                var summary = nav.querySelector('summary');
                if (summary) {
                    summary.focus();
                }
            }
        }
    });

    // Cerrar automaticamente al pasar a resolucion de escritorio (lg: 1024px)
    window.addEventListener('resize', function () {
        if (window.innerWidth >= 1024) {
            var nav = getMobileNav();
            if (nav && nav.hasAttribute('open')) {
                nav.removeAttribute('open');
            }
        }
    });

    // Cerrar al navegar en el historial del navegador (popstate)
    window.addEventListener('popstate', function () {
        var nav = getMobileNav();
        if (nav && nav.hasAttribute('open')) {
            nav.removeAttribute('open');
        }
    });
})();
