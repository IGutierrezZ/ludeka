/**
 * Ludeka Rail Drag-to-Scroll
 * Permite arrastrar con el ratón en escritorio los carriles horizontales (.overflow-x-auto)
 * y previene la navegación accidental en enlaces al soltar si hubo desplazamiento.
 */
(function () {
    let isDown = false;
    let startX = 0;
    let scrollLeft = 0;
    let activeContainer = null;
    let hasDragged = false;

    document.addEventListener('mousedown', function (e) {
        // Solo responder al botón primario (izquierdo)
        if (e.button !== 0) return;

        const target = e.target;
        if (!target) return;

        const tagName = target.tagName ? target.tagName.toLowerCase() : '';
        if (['input', 'select', 'textarea', 'button'].includes(tagName)) return;

        const container = target.closest('.overflow-x-auto');
        if (!container) return;

        // Comprobar si el contenedor tiene contenido desplazable horizontalmente
        if (container.scrollWidth <= container.clientWidth) return;

        isDown = true;
        hasDragged = false;
        activeContainer = container;
        startX = e.pageX - container.offsetLeft;
        scrollLeft = container.scrollLeft;
    });

    document.addEventListener('mousemove', function (e) {
        if (!isDown || !activeContainer) return;

        const x = e.pageX - activeContainer.offsetLeft;
        const walk = x - startX;

        if (Math.abs(walk) > 6) {
            hasDragged = true;
            activeContainer.classList.add('cursor-grabbing');
            activeContainer.style.userSelect = 'none';
            activeContainer.style.scrollBehavior = 'auto';
            activeContainer.style.scrollSnapType = 'none';
        }

        if (hasDragged) {
            e.preventDefault();
            activeContainer.scrollLeft = scrollLeft - walk;
        }
    });

    function endDrag() {
        if (activeContainer) {
            activeContainer.classList.remove('cursor-grabbing');
            activeContainer.style.userSelect = '';
            activeContainer.style.scrollBehavior = '';
            activeContainer.style.scrollSnapType = '';
        }
        isDown = false;
        // Mantener hasDragged brevemente para interceptar el evento click
        setTimeout(function () {
            activeContainer = null;
            hasDragged = false;
        }, 60);
    }

    document.addEventListener('mouseup', endDrag);

    // Evitar navegación o clics accidentales si el usuario arrastró el carril
    document.addEventListener('click', function (e) {
        if (hasDragged) {
            e.preventDefault();
            e.stopPropagation();
        }
    }, true); // Fase de captura para interceptar antes del listener del elemento <a>
})();
