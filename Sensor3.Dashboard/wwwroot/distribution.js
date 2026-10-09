// Detection is a convenience only. Every device can select either platform manually.
function detectDownloadPlatform() {
    if (location.pathname !== '/download') return;
    const url = new URL(location.href);
    if (url.searchParams.has('platform')) return;
    const platform = /Android/i.test(navigator.userAgent) ? 'Android' : /Windows/i.test(navigator.userAgent) ? 'Windows' : null;
    if (platform) { url.searchParams.set('platform', platform); location.replace(url); }
}
detectDownloadPlatform();
Blazor.addEventListener('enhancedload', detectDownloadPlatform);
