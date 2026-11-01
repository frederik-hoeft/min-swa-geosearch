const pendingRequests = new Map();
const heldLocks = new Map();

export function acquire(name, requestId) {
    if (!navigator.locks) {
        throw new Error("The Web Locks API is required for safe authentication coordination.");
    }

    if (pendingRequests.has(requestId) || heldLocks.has(requestId)) {
        throw new Error("The authentication lock request identifier is already in use.");
    }

    const controller = new AbortController();
    pendingRequests.set(requestId, controller);

    return new Promise((resolve, reject) => {
        let lockRequest;
        try {
            lockRequest = navigator.locks.request(
                name,
                { mode: "exclusive", signal: controller.signal },
                async () => {
                    pendingRequests.delete(requestId);

                    let releaseLock;
                    const released = new Promise(release => {
                        releaseLock = release;
                    });

                    heldLocks.set(requestId, releaseLock);
                    resolve();
                    await released;
                    heldLocks.delete(requestId);
                });
        } catch (error) {
            pendingRequests.delete(requestId);
            reject(error);
            return;
        }

        lockRequest.catch(error => {
            pendingRequests.delete(requestId);
            heldLocks.delete(requestId);
            reject(error);
        });
    });
}

export function release(requestId) {
    const releaseLock = heldLocks.get(requestId);
    if (!releaseLock) {
        return;
    }

    heldLocks.delete(requestId);
    releaseLock();
}

export function cancelAndRelease(requestId) {
    const controller = pendingRequests.get(requestId);
    if (controller) {
        pendingRequests.delete(requestId);
        controller.abort();
    }

    release(requestId);
}
