/**
 * app.js — Application bootstrap
 * Checks auth state on load, routes to login or chat
 */

(async function bootstrap() {
  // Initialize theme immediately
  ThemeManager.init();

  // Initialize auth listeners
  Auth.init();

  // Register service worker
  if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
      navigator.serviceWorker.register('/service-worker.js')
        .then(reg => console.log('SW registered', reg.scope))
        .catch(err => console.warn('SW registration failed', err));
    });
  }

  // Check if user is already authenticated
  try {
    const resp = await fetch('/api/auth/me', { credentials: 'include' });
    if (resp.ok) {
      const data = await resp.json();
      if (data.authenticated) {
        // Already logged in — show chat
        document.getElementById('screen-login').classList.remove('active');
        document.getElementById('screen-login').classList.add('hidden');
        document.getElementById('screen-chat').classList.remove('hidden');
        document.getElementById('screen-chat').classList.add('active');
        Chat.init();
        return;
      }
    }
  } catch {
    // Network error — stay on login
  }

  // Show login screen
  document.getElementById('screen-login').classList.add('active');
  document.getElementById('screen-login').classList.remove('hidden');
})();
