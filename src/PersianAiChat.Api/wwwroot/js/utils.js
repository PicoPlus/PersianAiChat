/**
 * utils.js — Shared utility functions
 * Persian digit handling, formatting, safe markdown, CSRF helpers
 */

// ── Persian digit maps ─────────────────────────────────────────
const PERSIAN_DIGITS = ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'];
const ARABIC_DIGITS  = ['٠','١','٢','٣','٤','٥','٦','٧','٨','٩'];

/**
 * Converts ASCII digits to Persian digits.
 * @param {number|string} value
 * @returns {string}
 */
function toPersianDigits(value) {
  return String(value).replace(/[0-9]/g, d => PERSIAN_DIGITS[+d]);
}

/**
 * Normalizes Persian/Arabic digits in a string to ASCII.
 * @param {string} str
 * @returns {string}
 */
function toAsciiDigits(str) {
  let result = str;
  for (let i = 0; i < 10; i++) {
    result = result.replaceAll(PERSIAN_DIGITS[i], String(i));
    result = result.replaceAll(ARABIC_DIGITS[i], String(i));
  }
  return result;
}

/**
 * Formats a number as Persian with Persian digit separators.
 * e.g. 31332 → "۳۱٬۳۳۲"
 * @param {number} n
 * @returns {string}
 */
function formatPersianNumber(n) {
  if (n == null || isNaN(n)) return '۰';
  // Use Intl to get separators, then convert digits
  const formatted = new Intl.NumberFormat('en-US').format(n)
    .replace(/,/g, '٬');
  return toPersianDigits(formatted);
}

/**
 * Formats a decimal cost value.
 * e.g. 0.04332675 → "۰٫۰۴۳۳۲۶۷۵"
 * @param {number} n
 * @returns {string}
 */
function formatPersianCost(n) {
  if (n == null || isNaN(n)) return '۰';
  const s = Number(n).toString().replace('.', '٫');
  return toPersianDigits(s);
}

/**
 * Formats a phone number for display in Persian digits.
 * @param {string} phone  e.g. "989122222222"
 * @returns {string} e.g. "۰۹۱۲۲۲۲۲۲۲۲"
 */
function formatPhoneDisplay(phone) {
  // Convert canonical 989... → 09... for display
  let display = phone;
  if (phone.startsWith('989')) {
    display = '0' + phone.slice(2);
  } else if (phone.startsWith('+989')) {
    display = '0' + phone.slice(3);
  }
  return toPersianDigits(display);
}

// ── CSRF token ─────────────────────────────────────────────────
/**
 * Reads the XSRF-TOKEN cookie value.
 * @returns {string}
 */
function getXsrfToken() {
  const match = document.cookie.match(/(?:^|;\s*)XSRF-TOKEN=([^;]+)/);
  return match ? decodeURIComponent(match[1]) : '';
}

/**
 * Performs a fetch with CSRF token header and JSON body.
 * @param {string} url
 * @param {object} options
 * @returns {Promise<Response>}
 */
async function apiFetch(url, options = {}) {
  const headers = {
    'Content-Type': 'application/json',
    'X-XSRF-TOKEN': getXsrfToken(),
    ...(options.headers || {})
  };
  return fetch(url, { ...options, headers, credentials: 'include' });
}

// ── Simple markdown renderer ───────────────────────────────────
/**
 * Renders a subset of Markdown to safe HTML.
 * Sanitizes to prevent XSS.
 * @param {string} text
 * @returns {string} HTML string
 */
function renderMarkdown(text) {
  if (!text) return '';

  // Escape HTML entities first
  let escaped = text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');

  // Fenced code blocks
  escaped = escaped.replace(/```(\w*)\n([\s\S]*?)```/g, (_, lang, code) => {
    const langLabel = lang ? `<span class="code-lang">${escapeHtml(lang)}</span>` : '';
    return `<div class="code-block-wrapper"><pre><code class="lang-${escapeHtml(lang)}">${code}</code></pre><button class="code-copy-btn" onclick="copyCode(this)" type="button">کپی</button></div>`;
  });

  // Inline code
  escaped = escaped.replace(/`([^`]+)`/g, '<code>$1</code>');

  // Bold
  escaped = escaped.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  escaped = escaped.replace(/__([^_]+)__/g, '<strong>$1</strong>');

  // Italic
  escaped = escaped.replace(/\*([^*]+)\*/g, '<em>$1</em>');
  escaped = escaped.replace(/_([^_]+)_/g, '<em>$1</em>');

  // Headings
  escaped = escaped.replace(/^######\s+(.+)$/gm, '<h6>$1</h6>');
  escaped = escaped.replace(/^#####\s+(.+)$/gm, '<h5>$1</h5>');
  escaped = escaped.replace(/^####\s+(.+)$/gm, '<h4>$1</h4>');
  escaped = escaped.replace(/^###\s+(.+)$/gm, '<h3>$1</h3>');
  escaped = escaped.replace(/^##\s+(.+)$/gm, '<h2>$1</h2>');
  escaped = escaped.replace(/^#\s+(.+)$/gm, '<h1>$1</h1>');

  // Blockquotes
  escaped = escaped.replace(/^&gt;\s+(.+)$/gm, '<blockquote>$1</blockquote>');

  // Unordered lists
  escaped = escaped.replace(/^[\-\*]\s+(.+)$/gm, '<li>$1</li>');
  escaped = escaped.replace(/(<li>.*<\/li>)/s, '<ul>$1</ul>');

  // Ordered lists
  escaped = escaped.replace(/^\d+\.\s+(.+)$/gm, '<li>$1</li>');

  // Links — validate protocol before rendering
  escaped = escaped.replace(/\[([^\]]+)\]\((https?:\/\/[^)]+)\)/g, (_, label, url) => {
    if (isSafeUrl(url)) {
      return `<a href="${url}" target="_blank" rel="noopener noreferrer">${label}</a>`;
    }
    return label;
  });

  // Paragraphs: double newline → paragraph
  const paragraphs = escaped.split(/\n{2,}/);
  escaped = paragraphs.map(p => {
    p = p.trim();
    if (!p) return '';
    // Don't wrap block elements
    if (/^<(h[1-6]|ul|ol|blockquote|pre|div)/.test(p)) return p;
    return `<p>${p.replace(/\n/g, '<br>')}</p>`;
  }).join('\n');

  return escaped;
}

function escapeHtml(str) {
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/**
 * Validates that a URL uses only safe protocols.
 * @param {string} url
 * @returns {boolean}
 */
function isSafeUrl(url) {
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'https:' || parsed.protocol === 'http:';
  } catch {
    return false;
  }
}

/**
 * Copies the code inside a pre element to clipboard.
 * @param {HTMLButtonElement} btn
 */
function copyCode(btn) {
  const pre = btn.previousElementSibling;
  if (!pre) return;
  const code = pre.textContent || '';
  navigator.clipboard.writeText(code).then(() => {
    const orig = btn.textContent;
    btn.textContent = 'کپی شد ✓';
    setTimeout(() => { btn.textContent = orig; }, 1800);
  }).catch(() => {});
}

// ── Theme management ───────────────────────────────────────────
const ThemeManager = {
  /** @type {'light'|'dark'|'system'} */
  current: 'system',

  init() {
    const saved = localStorage.getItem('theme') || 'system';
    this.apply(saved);
  },

  apply(theme) {
    this.current = theme;
    localStorage.setItem('theme', theme);
    const isDark = theme === 'dark' ||
      (theme === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
    document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
    // Update UI buttons
    document.querySelectorAll('.theme-btn').forEach(btn => {
      btn.classList.toggle('active', btn.dataset.theme === theme);
    });
  }
};

// Auto-respond to system preference change
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
  if (ThemeManager.current === 'system') ThemeManager.apply('system');
});

// ── Toast notification ────────────────────────────────────────
function showToast(message, type = 'info', duration = 3000) {
  const existing = document.getElementById('toast-container');
  if (!existing) {
    const container = document.createElement('div');
    container.id = 'toast-container';
    container.style.cssText = `
      position: fixed; bottom: 80px; left: 50%; transform: translateX(-50%);
      z-index: 9999; display: flex; flex-direction: column; gap: 8px;
      align-items: center; pointer-events: none;
    `;
    document.body.appendChild(container);
  }
  const toast = document.createElement('div');
  toast.style.cssText = `
    background: ${type === 'error' ? '#ef4444' : type === 'success' ? '#22c55e' : '#3b82f6'};
    color: #fff; padding: .5rem 1rem; border-radius: 8px; font-size: .875rem;
    box-shadow: 0 4px 12px rgba(0,0,0,.2); direction: rtl; white-space: nowrap;
    animation: fadeInUp .2s ease; pointer-events: auto;
  `;
  toast.textContent = message;
  document.getElementById('toast-container').appendChild(toast);
  setTimeout(() => toast.remove(), duration);
}

// Expose globally
window.toPersianDigits = toPersianDigits;
window.toAsciiDigits = toAsciiDigits;
window.formatPersianNumber = formatPersianNumber;
window.formatPersianCost = formatPersianCost;
window.formatPhoneDisplay = formatPhoneDisplay;
window.apiFetch = apiFetch;
window.renderMarkdown = renderMarkdown;
window.copyCode = copyCode;
window.ThemeManager = ThemeManager;
window.showToast = showToast;
window.isSafeUrl = isSafeUrl;
