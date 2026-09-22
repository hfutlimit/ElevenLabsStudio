(function () {
    "use strict";

    const client = window.ElevenLabsClient;
    let conversation = null;
    let volumeTimer = null;

    function post(message) {
        if (window.chrome?.webview) {
            window.chrome.webview.postMessage(JSON.stringify(message));
        }
    }

    function stopVolumePolling() {
        if (volumeTimer !== null) {
            window.clearInterval(volumeTimer);
            volumeTimer = null;
        }
    }

    function startVolumePolling() {
        stopVolumePolling();
        volumeTimer = window.setInterval(async () => {
            if (!conversation) return;
            try {
                post({
                    type: "volumeChanged",
                    inputVolume: conversation.getInputVolume(),
                    outputVolume: conversation.getOutputVolume(),
                });
            } catch (_) {
                // Volume visualization is optional and must not end a session.
            }
        }, 180);
    }

    function normalizeSpeaker(message) {
        if (message?.role === "user" || message?.source === "user") return "user";
        return "agent";
    }

    async function startSession(payload) {
        if (conversation) return;
        if (!client?.Conversation) {
            post({ type: "error", error: "ElevenLabs client bundle is unavailable." });
            return;
        }

        try {
            post({ type: "connecting" });
            const permissionStream = await navigator.mediaDevices.getUserMedia({ audio: true });
            try {
                conversation = await client.Conversation.startSession({
                    signedUrl: payload.signedUrl,
                    dynamicVariables: payload.dynamicVariables || {},
                    onConnect: ({ conversationId }) => {
                        post({ type: "connected", conversationId: conversationId || "" });
                        startVolumePolling();
                    },
                    onDisconnect: () => {
                        stopVolumePolling();
                        post({ type: "disconnected" });
                        conversation = null;
                    },
                    onMessage: (message) => {
                        post({
                            type: "message",
                            speaker: normalizeSpeaker(message),
                            text: String(message?.message || ""),
                            at: new Date().toISOString(),
                        });
                    },
                    onModeChange: ({ mode }) => post({ type: "modeChanged", mode: mode || "" }),
                    onError: (error) => post({ type: "error", error: String(error || "Realtime client error") }),
                });
            } finally {
                permissionStream.getTracks().forEach((track) => track.stop());
            }
        } catch (error) {
            stopVolumePolling();
            conversation = null;
            post({ type: "failed", error: error instanceof Error ? error.message : String(error) });
        }
    }

    async function stopSession() {
        if (!conversation) return;
        try {
            await conversation.endSession();
        } catch (error) {
            post({ type: "error", error: error instanceof Error ? error.message : String(error) });
        }
    }

    async function handleCommand(raw) {
        const message = typeof raw === "string" ? JSON.parse(raw) : raw;
        switch (message?.type) {
            case "start":
                await startSession(message);
                break;
            case "stop":
                await stopSession();
                break;
            case "mute":
                if (conversation) conversation.setMicMuted(Boolean(message.muted));
                break;
            case "sendText":
                if (conversation && String(message.text || "").trim()) {
                    conversation.sendUserMessage(String(message.text).trim());
                }
                break;
        }
    }

    window.chrome?.webview?.addEventListener("message", (event) => {
        handleCommand(event.data).catch((error) => {
            post({ type: "error", error: error instanceof Error ? error.message : String(error) });
        });
    });

    post({ type: "ready" });
})();
