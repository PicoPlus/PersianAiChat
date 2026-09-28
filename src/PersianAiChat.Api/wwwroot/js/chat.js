/**
 * chat.js — Chat UI controller
 * Conversations, messages, token usage, sidebar, markdown rendering
 */

const Chat = (() => {
  let currentConversationId = null;
  let isProcessing = false;
  let usageSummary = { totalTokens: 0, totalPromptTokens: 0, totalCompletionTokens: 0, totalReasoningTokens: 0, totalCost: 0, totalWebSearchRequests: 0 };
  let conversationList = [];

  const el = {
    messagesArea:       () => document.getElementById('messages-area'),
    welcomeScreen:      () => document.getElementById('welcome-screen'),
    typingIndicator:    () => document.getElementById('typing-indicator'),
    typingText:         () => document.getElementById('typing-text'),
    messageInput:       () => document.getElementById('message-input'),
    btnSend:            () => document.getElementById('btn-send'),
    tokenCountDisplay:  () => document.getElementById('token-count-display'),
    tokenPanel:         () => document.getElementById('token-panel'),
    tokenCounter:       () => document.getElementById('token-counter'),
    btnCloseTokenPanel: () => document.getElementById('btn-close-token-panel'),
    tpTotal:            () => document.getElementById('tp-total'),
    tpPrompt:           () => document.getElementById('tp-prompt'),
    tpCompletion:       () => document.getElementById('tp-completion'),
    tpReasoning:        () => document.getElementById('tp-reasoning'),
    tpCost:             () => document.getElementById('tp-cost'),
    tpWebsearch:        () => document.getElementById('tp-websearch'),
    sidebar:            () => document.getElementById('sidebar'),
    sidebarOverlay:     () => document.getElementById('sidebar-overlay'),
    btnToggleSidebar:   () => document.getElementById('btn-toggle-sidebar'),
    btnCloseSidebar:    () => document.getElementById('btn-close-sidebar'),
    btnNewChat:         () => document.getElementById('btn-new-chat'),
    conversationsList:  () => document.getElementById('conversations-list'),
    btnLogout:          () => document.getElementById('btn-logout'),
    btnSettings:        () => document.getElementById('btn-settings'),
    settingsModal:      () => document.getElementById('settings-modal'),
    btnCloseSettings:   () => document.getElementById('btn-close-settings'),
  };

  // ── Token usage ───────────────────────────────────────────────
  async function loadUsageSummary() {
    try {
      const resp = await apiFetch('/api/usage/summary');
      if (!resp.ok) return;
      usageSummary = await resp.json();
      updateTokenDisplay();
    } catch { /* silent */ }
  }

  function updateTokenDisplay() {
    el.tokenCountDisplay().textContent = formatPersianNumber(usageSummary.totalTokens || 0);
    el.tpTotal().textContent = formatPersianNumber(usageSummary.totalTokens || 0);
    el.tpPrompt().textContent = formatPersianNumber(usageSummary.totalPromptTokens || 0);
    el.tpCompletion().textContent = formatPersianNumber(usageSummary.totalCompletionTokens || 0);
    el.tpReasoning().textContent = formatPersianNumber(usageSummary.totalReasoningTokens || 0);
    el.tpCost().textContent = formatPersianCost(usageSummary.totalCost || 0);
    el.tpWebsearch().textContent = toPersianDigits(String(usageSummary.totalWebSearchRequests || 0));
  }

  function updateUsageFromResponse(usage) {
    if (!usage) return;
    // Refresh from server to ensure accuracy
    loadUsageSummary();
  }

  // ── Conversations ─────────────────────────────────────────────
  async function loadConversations() {
    try {
      const resp = await apiFetch('/api/conversations');
      if (!resp.ok) return;
      conversationList = await resp.json();
      renderConversationList();
    } catch { /* silent */ }
  }

  function renderConversationList() {
    const container = el.conversationsList();
    container.innerHTML = '';

    if (!conversationList.length) {
      container.innerHTML = `<p style="padding:.75rem;font-size:.8rem;color:var(--text-muted);text-align:center;">گفت‌وگویی ندارید</p>`;
      return;
    }

    conversationList.forEach(conv => {
      const item = document.createElement('div');
      item.className = `conv-item${conv.id === currentConversationId ? ' active' : ''}`;
      item.dataset.id = conv.id;
      item.setAttribute('role', 'listitem');
      item.innerHTML = `
        <span class="conv-title" title="${escapeAttr(conv.title)}">${escapeHtmlText(conv.title)}</span>
        <div class="conv-actions">
          <button class="conv-action-btn" data-action="rename" data-id="${conv.id}" title="تغییر نام" aria-label="تغییر نام گفت‌وگو">✎</button>
          <button class="conv-action-btn" data-action="delete" data-id="${conv.id}" title="حذف" aria-label="حذف گفت‌وگو">🗑</button>
        </div>
      `;
      item.addEventListener('click', (e) => {
        if (e.target.closest('.conv-action-btn')) return;
        openConversation(conv.id);
      });
      container.appendChild(item);
    });

    // Action handlers
    container.querySelectorAll('[data-action="rename"]').forEach(btn => {
      btn.addEventListener('click', () => renameConversation(btn.dataset.id));
    });
    container.querySelectorAll('[data-action="delete"]').forEach(btn => {
      btn.addEventListener('click', () => deleteConversation(btn.dataset.id));
    });
  }

  async function openConversation(id) {
    currentConversationId = id;
    renderConversationList();
    closeSidebar();
    el.messagesArea().innerHTML = '';
    el.welcomeScreen() && el.welcomeScreen().remove();

    try {
      const resp = await apiFetch(`/api/conversations/${id}`);
      if (!resp.ok) return;
      const conv = await resp.json();

      conv.messages.forEach(msg => {
        appendMessage(msg.role, msg.content, null, msg.id, false);
      });
      scrollToBottom();
    } catch { /* silent */ }
  }

  async function renameConversation(id) {
    const conv = conversationList.find(c => c.id === id);
    if (!conv) return;
    const newTitle = prompt('عنوان جدید:', conv.title);
    if (!newTitle || !newTitle.trim()) return;

    try {
      const resp = await apiFetch(`/api/conversations/${id}`, {
        method: 'PATCH',
        body: JSON.stringify({ title: newTitle.trim() })
      });
      if (resp.ok) await loadConversations();
    } catch { /* silent */ }
  }

  async function deleteConversation(id) {
    if (!confirm('این گفت‌وگو حذف شود؟')) return;
    try {
      const resp = await apiFetch(`/api/conversations/${id}`, { method: 'DELETE' });
      if (resp.ok) {
        if (currentConversationId === id) {
          currentConversationId = null;
          clearMessages();
        }
        await loadConversations();
      }
    } catch { /* silent */ }
  }

  // ── New chat ──────────────────────────────────────────────────
  function startNewChat() {
    currentConversationId = null;
    clearMessages();
    closeSidebar();
    el.messageInput().focus();
    renderConversationList();
  }

  function clearMessages() {
    const area = el.messagesArea();
    area.innerHTML = '';
    // Re-inject welcome screen
    const welcome = document.createElement('div');
    welcome.id = 'welcome-screen';
    welcome.className = 'welcome-screen';
    welcome.innerHTML = `
      <div class="welcome-icon">🤖</div>
      <h2 class="welcome-title">دستیار هوش مصنوعی</h2>
      <p class="welcome-subtitle">چطور می‌توانم کمک کنم؟</p>
      <div class="suggestion-cards" role="list">
        <button class="suggestion-card" role="listitem" data-prompt="آخرین اخبار چیست؟">
          <span class="suggestion-icon">📰</span><span>آخرین اخبار چیست؟</span>
        </button>
        <button class="suggestion-card" role="listitem" data-prompt="ثبت‌نام دانشگاه آزاد چه زمانی شروع می‌شود؟">
          <span class="suggestion-icon">🎓</span><span>ثبت‌نام دانشگاه آزاد چه زمانی است؟</span>
        </button>
        <button class="suggestion-card" role="listitem" data-prompt="این متن را خلاصه کن:">
          <span class="suggestion-icon">✂️</span><span>این متن را خلاصه کن</span>
        </button>
        <button class="suggestion-card" role="listitem" data-prompt="یک برنامه مطالعه برای من طراحی کن">
          <span class="suggestion-icon">📅</span><span>یک برنامه مطالعه برایم طراحی کن</span>
        </button>
      </div>
    `;
    area.appendChild(welcome);
    bindSuggestionCards(welcome);
  }

  function bindSuggestionCards(container) {
    container.querySelectorAll('.suggestion-card').forEach(card => {
      card.addEventListener('click', () => {
        el.messageInput().value = card.dataset.prompt;
        el.messageInput().dispatchEvent(new Event('input'));
        sendMessage();
      });
    });
  }

  // ── Send message ──────────────────────────────────────────────
  async function sendMessage() {
    if (isProcessing) return;
    const text = el.messageInput().value.trim();
    if (!text) return;

    // Remove welcome screen if visible
    document.getElementById('welcome-screen')?.remove();

    isProcessing = true;
    el.btnSend().disabled = true;
    el.messageInput().value = '';
    resizeTextarea();

    appendMessage('user', text, null, null, true);
    showTypingIndicator('در حال پاسخ‌گویی...');
    scrollToBottom();

    try {
      const body = { message: text };
      if (currentConversationId) body.conversationId = currentConversationId;

      const resp = await apiFetch('/api/chat', {
        method: 'POST',
        body: JSON.stringify(body)
      });

      const data = await resp.json();
      hideTypingIndicator();

      if (!resp.ok) {
        appendErrorMessage(data.title || 'خطایی رخ داده است.');
        return;
      }

      currentConversationId = data.conversationId;
      appendMessage('assistant', data.message.content, data.message.citations, data.message.id, true);

      // Show ticket CTA card if backend detected a service intent
      if (data.responseType === 'offerticket' && data.ticketContext) {
        appendTicketCard(data.ticketContext, data.conversationId);
      }

      scrollToBottom();

      // Update usage
      updateUsageFromResponse(data.usage);

      // Refresh conversations list
      await loadConversations();

    } catch (err) {
      hideTypingIndicator();
      appendErrorMessage('خطا در برقراری ارتباط با سرور.');
    } finally {
      isProcessing = false;
      el.btnSend().disabled = !el.messageInput().value.trim();
    }
  }

  // ── Typing indicator ──────────────────────────────────────────
  function showTypingIndicator(text) {
    el.typingText().textContent = text || 'در حال پاسخ‌گویی...';
    el.typingIndicator().classList.remove('hidden');
  }

  function hideTypingIndicator() {
    el.typingIndicator().classList.add('hidden');
  }

  // ── Message rendering ─────────────────────────────────────────
  function appendMessage(role, content, citations, msgId, animate) {
    const area = el.messagesArea();
    const row = document.createElement('div');
    row.className = `message-row ${role}`;
    if (msgId) row.dataset.msgId = msgId;
    if (animate) row.style.cssText = 'animation: fadeInUp .2s ease;';

    const avatarIcon = role === 'user' ? '👤' : '🤖';
    const contentHtml = role === 'assistant'
      ? `<div class="md-content">${renderMarkdown(content)}</div>`
      : `<p>${escapeHtmlText(content)}</p>`;

    let citationsHtml = '';
    if (citations && citations.length > 0) {
      const cards = citations.map(c => {
        let host = '';
        try { host = new URL(c.url).hostname; } catch { host = c.url; }
        const safeUrl = isSafeUrl(c.url) ? c.url : '#';
        return `
          <a href="${safeUrl}" target="_blank" rel="noopener noreferrer" class="citation-card">
            <img class="citation-favicon" src="https://www.google.com/s2/favicons?sz=16&domain=${encodeURIComponent(host)}" alt="" loading="lazy" onerror="this.style.display='none'" />
            <div class="citation-info">
              <div class="citation-title">${escapeHtmlText(c.title || host)}</div>
              <div class="citation-host">${escapeHtmlText(host)}</div>
            </div>
            <span class="citation-link-icon" aria-hidden="true">↗</span>
          </a>`;
      }).join('');

      citationsHtml = `
        <div class="citations">
          <div class="citations-label">📎 منابع</div>
          <div class="citation-cards">${cards}</div>
        </div>`;
    }

    const actionsHtml = role === 'assistant' ? `
      <div class="message-actions" role="group" aria-label="عملیات پیام">
        <button class="msg-action-btn" onclick="copyMessageContent(this)" type="button" title="کپی">کپی</button>
        <button class="msg-action-btn" onclick="regenerateMessage(this)" type="button" title="پاسخ مجدد">پاسخ مجدد</button>
      </div>` : '';

    row.innerHTML = `
      <div class="avatar" aria-hidden="true">${avatarIcon}</div>
      <div class="bubble-wrapper">
        <div class="bubble">${contentHtml}</div>
        ${citationsHtml}
        ${actionsHtml}
      </div>`;

    area.appendChild(row);
  }

  function appendErrorMessage(text) {
    const area = el.messagesArea();
    const div = document.createElement('div');
    div.style.cssText = `
      text-align:center; color:var(--danger); font-size:.875rem;
      padding:.5rem; background:rgba(239,68,68,.1); border-radius:8px;
    `;
    div.textContent = text;
    area.appendChild(div);
  }

  function scrollToBottom() {
    const area = el.messagesArea();
    area.scrollTo({ top: area.scrollHeight, behavior: 'smooth' });
  }

  // ── Message actions ───────────────────────────────────────────
  window.copyMessageContent = function(btn) {
    const bubble = btn.closest('.bubble-wrapper')?.querySelector('.bubble');
    if (!bubble) return;
    const text = bubble.textContent || '';
    navigator.clipboard.writeText(text.trim()).then(() => {
      const orig = btn.textContent;
      btn.textContent = 'کپی شد ✓';
      setTimeout(() => { btn.textContent = orig; }, 1800);
    }).catch(() => {});
  };

  window.regenerateMessage = function(btn) {
    showToast('برای پاسخ مجدد، پیام جدیدی بفرستید.', 'info');
  };

  // ── Sidebar ───────────────────────────────────────────────────
  function openSidebar() {
    el.sidebar().classList.add('open');
    el.sidebarOverlay().classList.add('visible');
    document.body.style.overflow = 'hidden';
  }

  function closeSidebar() {
    el.sidebar().classList.remove('open');
    el.sidebarOverlay().classList.remove('visible');
    document.body.style.overflow = '';
  }

  // ── Token panel ───────────────────────────────────────────────
  function toggleTokenPanel() {
    el.tokenPanel().classList.toggle('hidden');
  }

  // ── Input auto-resize ─────────────────────────────────────────
  function resizeTextarea() {
    const ta = el.messageInput();
    ta.style.height = 'auto';
    ta.style.height = Math.min(ta.scrollHeight, 200) + 'px';
  }

  // ── Logout ────────────────────────────────────────────────────
  async function logout() {
    try {
      await apiFetch('/api/auth/logout', { method: 'POST' });
    } catch { /* silent */ }
    location.href = '/';
  }

  // ── Helpers ───────────────────────────────────────────────────
  function escapeHtmlText(str) {
    return String(str || '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;');
  }

  function escapeAttr(str) {
    return String(str || '').replace(/"/g, '&quot;');
  }

  // ── Ticket CTA ────────────────────────────────────────────────

  function appendTicketCard(ctx, conversationId) {
    const area = el.messagesArea();
    const card = document.createElement('div');
    card.className = 'ticket-cta-card';
    card.innerHTML = `
      <span class="ticket-cta-icon" aria-hidden="true">📋</span>
      <div class="ticket-cta-body">
        <p class="ticket-cta-question">می‌خواهی این درخواست برای اپراتور ثبت بشه؟</p>
        <p class="ticket-cta-subject">${escapeHtmlText(ctx.suggestedSubject)}</p>
        <div class="ticket-cta-actions">
          <button class="btn-ticket-yes" type="button">✓ بله، ثبت کن</button>
          <button class="btn-ticket-no"  type="button">✗ نه، ممنون</button>
        </div>
      </div>`;

    card.querySelector('.btn-ticket-yes').addEventListener('click', () =>
      submitTicket(ctx, conversationId, card));
    card.querySelector('.btn-ticket-no').addEventListener('click', () =>
      dismissTicket(card));

    area.appendChild(card);
    scrollToBottom();
  }

  async function submitTicket(ctx, conversationId, card) {
    const btnYes = card.querySelector('.btn-ticket-yes');
    const btnNo  = card.querySelector('.btn-ticket-no');
    btnYes.disabled = true;
    btnNo.disabled  = true;
    btnYes.textContent = '⏳ در حال تحلیل و ثبت...';

    try {
      const resp = await apiFetch('/api/tickets', {
        method: 'POST',
        body: JSON.stringify({
          userQuestion:   ctx.suggestedSubject,
          botAnswer:      ctx.botAnswer,
          category:       ctx.suggestedCategory,
          conversationId: conversationId
        })
      });
      const data = await resp.json();

      if (resp.ok && data.success) {
        // Replace card with success state
        card.className = 'ticket-success-card';
        card.innerHTML = `
          <span class="ticket-success-icon" aria-hidden="true">✅</span>
          <div class="ticket-success-body">
            <p class="ticket-success-msg">درخواست شما با موفقیت ثبت شد.</p>
            ${data.analysisSummary
              ? `<p class="ticket-analysis">${escapeHtmlText(data.analysisSummary)}</p>`
              : ''}
            ${data.dealId
              ? `<p class="ticket-ref">شماره پیگیری: <span dir="ltr">${escapeHtmlText(data.dealId)}</span></p>`
              : ''}
          </div>`;
      } else {
        // Re-enable on failure
        btnYes.textContent = '✓ بله، ثبت کن';
        btnYes.disabled = false;
        btnNo.disabled  = false;
        const errEl = document.createElement('p');
        errEl.className = 'ticket-error-msg';
        errEl.textContent = data.title || 'خطا در ثبت درخواست. لطفاً دوباره تلاش کنید.';
        card.querySelector('.ticket-cta-body').appendChild(errEl);
      }
    } catch {
      btnYes.textContent = '✓ بله، ثبت کن';
      btnYes.disabled = false;
      btnNo.disabled  = false;
    }
    scrollToBottom();
  }

  function dismissTicket(card) {
    card.style.opacity    = '0';
    card.style.transform  = 'translateY(-6px)';
    card.style.transition = 'opacity .18s ease, transform .18s ease';
    setTimeout(() => card.remove(), 200);
  }

  // ── Init ──────────────────────────────────────────────────────
  async function init() {
    // Load initial data
    await Promise.all([loadUsageSummary(), loadConversations()]);

    // Send button / textarea
    const ta = el.messageInput();
    const btnSend = el.btnSend();

    ta.addEventListener('input', () => {
      resizeTextarea();
      btnSend.disabled = !ta.value.trim() || isProcessing;
    });

    ta.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey && !e.ctrlKey) {
        e.preventDefault();
        sendMessage();
      } else if ((e.key === 'Enter' && e.ctrlKey)) {
        e.preventDefault();
        sendMessage();
      }
    });

    btnSend.addEventListener('click', sendMessage);

    // Suggestion cards (initial welcome)
    const welcome = document.getElementById('welcome-screen');
    if (welcome) bindSuggestionCards(welcome);

    // Sidebar
    el.btnToggleSidebar()?.addEventListener('click', openSidebar);
    el.btnCloseSidebar()?.addEventListener('click', closeSidebar);
    el.sidebarOverlay()?.addEventListener('click', closeSidebar);

    // New chat
    el.btnNewChat()?.addEventListener('click', startNewChat);

    // Token panel
    el.tokenCounter()?.addEventListener('click', toggleTokenPanel);
    el.tokenCounter()?.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') toggleTokenPanel();
    });
    el.btnCloseTokenPanel()?.addEventListener('click', () => {
      el.tokenPanel().classList.add('hidden');
    });

    // Close token panel on outside click
    document.addEventListener('click', (e) => {
      if (!el.tokenPanel().classList.contains('hidden') &&
          !el.tokenPanel().contains(e.target) &&
          !el.tokenCounter().contains(e.target)) {
        el.tokenPanel().classList.add('hidden');
      }
    });

    // Logout
    el.btnLogout()?.addEventListener('click', logout);

    // Settings
    el.btnSettings()?.addEventListener('click', () => {
      el.settingsModal().classList.toggle('hidden');
    });
    el.btnCloseSettings()?.addEventListener('click', () => {
      el.settingsModal().classList.add('hidden');
    });
    el.settingsModal()?.querySelector('.modal-overlay')?.addEventListener('click', () => {
      el.settingsModal().classList.add('hidden');
    });

    // Theme buttons
    document.querySelectorAll('.theme-btn').forEach(btn => {
      btn.addEventListener('click', () => ThemeManager.apply(btn.dataset.theme));
    });

    ta.focus();
  }

  return { init };
})();

window.Chat = Chat;
