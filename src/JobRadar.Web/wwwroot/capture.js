// Capture page: receives the ad text the bookmarklet sends from a Seek ad page the candidate opened.
// Only Seek pages may send it, and the text is shown for checking before anything is saved.
window.jobRadarCapture = {
    listen: dotnet => {
        const fromSeek = origin => /^https:\/\/([a-z0-9-]+\.)*seek\.com\.au$/i.test(origin);
        window.addEventListener("message", e => {
            if (!fromSeek(e.origin) || !e.data || e.data.type !== "jobradar-capture")
                return;
            dotnet.invokeMethodAsync("Receive", String(e.data.source || ""), String(e.data.id || ""), String(e.data.text || ""));
        });
        // The bookmarklet waits for this before sending: nothing goes to a page that is not listening.
        // No data in it, so any target origin will do.
        if (window.opener)
            window.opener.postMessage("jobradar-ready", "*");
    },
};
