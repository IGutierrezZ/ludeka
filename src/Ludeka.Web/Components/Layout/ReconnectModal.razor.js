// Set up event handlers
const reconnectModal = document.getElementById("components-reconnect-modal");
reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);

const retryButton = document.getElementById("components-reconnect-button");
retryButton.addEventListener("click", retry);

const resumeButton = document.getElementById("components-resume-button");
resumeButton.addEventListener("click", resume);

let showModalTimer = null;
const RECONNECT_GRACE_PERIOD_MS = 1500;

function clearShowTimer() {
    if (showModalTimer !== null) {
        clearTimeout(showModalTimer);
        showModalTimer = null;
    }
}

function handleReconnectStateChanged(event) {
    if (event.detail.state === "show") {
        // Retardo de cortesía: si la reconexión se resuelve rápidamente (p. ej. al volver de otra pestaña),
        // no mostramos el diálogo evitando parpadeos molestos en la interfaz.
        if (showModalTimer === null && !reconnectModal.open) {
            showModalTimer = setTimeout(() => {
                showModalTimer = null;
                if (!reconnectModal.open) {
                    reconnectModal.showModal();
                }
            }, RECONNECT_GRACE_PERIOD_MS);
        }
    } else if (event.detail.state === "hide") {
        clearShowTimer();
        if (reconnectModal.open) {
            reconnectModal.close();
        }
    } else if (event.detail.state === "failed") {
        clearShowTimer();
        if (!reconnectModal.open) {
            reconnectModal.showModal();
        }
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    } else if (event.detail.state === "rejected") {
        clearShowTimer();
        location.reload();
    }
}

async function retry() {
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);

    try {
        // Reconnect will asynchronously return:
        // - true to mean success
        // - false to mean we reached the server, but it rejected the connection (e.g., unknown circuit ID)
        // - exception to mean we didn't reach the server (this can be sync or async)
        const successful = await Blazor.reconnect();
        if (!successful) {
            // We have been able to reach the server, but the circuit is no longer available.
            // We'll reload the page so the user can continue using the app as quickly as possible.
            const resumeSuccessful = await Blazor.resumeCircuit();
            if (!resumeSuccessful) {
                location.reload();
            } else {
                clearShowTimer();
                if (reconnectModal.open) {
                    reconnectModal.close();
                }
            }
        }
    } catch (err) {
        // We got an exception, server is currently unavailable
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    }
}

async function resume() {
    try {
        const successful = await Blazor.resumeCircuit();
        if (!successful) {
            location.reload();
        } else {
            clearShowTimer();
            if (reconnectModal.open) {
                reconnectModal.close();
            }
        }
    } catch {
        reconnectModal.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
    }
}

async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") {
        await retry();
    }
}
