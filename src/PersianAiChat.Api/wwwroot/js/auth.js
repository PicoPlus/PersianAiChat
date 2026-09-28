/**
 * auth.js — Authentication flow controller
 * Phone input → OTP request → OTP verification → session
 */

const Auth = (() => {
  let currentPhone = '';   // normalized canonical phone from backend perspective
  let rawPhone = '';       // raw user input
  let countdownTimer = null;

  // DOM references
  const screens = {
    login: () => document.getElementById('screen-login'),
    chat:  () => document.getElementById('screen-chat')
  };

  const el = {
    cardPhone:      () => document.getElementById('card-phone'),
    cardOtp:        () => document.getElementById('card-otp'),
    inputPhone:     () => document.getElementById('input-phone'),
    phoneError:     () => document.getElementById('phone-error'),
    btnRequestOtp:  () => document.getElementById('btn-request-otp'),
    otpBoxes:       () => Array.from(document.querySelectorAll('.otp-box')),
    otpError:       () => document.getElementById('otp-error'),
    btnVerifyOtp:   () => document.getElementById('btn-verify-otp'),
    otpPhoneDisplay:() => document.getElementById('otp-phone-display'),
    btnResend:      () => document.getElementById('btn-resend'),
    countdownSpan:  () => document.getElementById('resend-countdown'),
    btnBackPhone:   () => document.getElementById('btn-back-phone'),
    btnRequestText: () => document.querySelector('#btn-request-otp .btn-text'),
    btnRequestLoader:()=> document.querySelector('#btn-request-otp .btn-loader'),
    btnVerifyText:  () => document.querySelector('#btn-verify-otp .btn-text'),
    btnVerifyLoader:()=> document.querySelector('#btn-verify-otp .btn-loader'),
  };

  // ── Screen transitions ────────────────────────────────────────
  function showScreen(name) {
    document.querySelectorAll('.screen').forEach(s => {
      s.classList.remove('active');
      s.classList.add('hidden');
    });
    const target = screens[name]();
    if (target) {
      target.classList.remove('hidden');
      requestAnimationFrame(() => target.classList.add('active'));
    }
  }

  function showCard(name) {
    el.cardPhone().classList.add('hidden');
    el.cardOtp().classList.add('hidden');
    if (name === 'phone') {
      el.cardPhone().classList.remove('hidden');
      el.inputPhone().focus();
    } else {
      el.cardOtp().classList.remove('hidden');
      el.otpBoxes()[0]?.focus();
    }
  }

  // ── Loading state helpers ─────────────────────────────────────
  function setRequestLoading(loading) {
    const btn = el.btnRequestOtp();
    btn.disabled = loading;
    el.btnRequestText().classList.toggle('hidden', loading);
    el.btnRequestLoader().classList.toggle('hidden', !loading);
  }

  function setVerifyLoading(loading) {
    const btn = el.btnVerifyOtp();
    btn.disabled = loading;
    el.btnVerifyText().classList.toggle('hidden', loading);
    el.btnVerifyLoader().classList.toggle('hidden', !loading);
  }

  // ── Phone input handling ──────────────────────────────────────
  function onPhoneInput(e) {
    // Allow Persian/Arabic digits through; will normalize on submit
    const val = e.target.value;
    el.phoneError().textContent = '';
    el.inputPhone().classList.remove('error');
  }

  function onPhoneKeydown(e) {
    if (e.key === 'Enter') requestOtp();
  }

  // ── OTP input handling ────────────────────────────────────────
  function onOtpKeydown(e) {
    const boxes = el.otpBoxes();
    const idx = parseInt(e.target.dataset.index);

    if (e.key === 'Backspace') {
      if (e.target.value) {
        e.target.value = '';
      } else if (idx > 0) {
        boxes[idx - 1].focus();
        boxes[idx - 1].value = '';
      }
      e.preventDefault();
    } else if (e.key === 'ArrowRight' && idx < boxes.length - 1) {
      boxes[idx + 1].focus();
    } else if (e.key === 'ArrowLeft' && idx > 0) {
      boxes[idx - 1].focus();
    } else if (e.key === 'Enter') {
      verifyOtp();
    }
  }

  function onOtpInput(e) {
    const boxes = el.otpBoxes();
    const idx = parseInt(e.target.dataset.index);
    el.otpError().textContent = '';
    boxes.forEach(b => b.classList.remove('error'));

    // Normalize Persian/Arabic digit
    let val = toAsciiDigits(e.target.value);
    // Accept only digits
    val = val.replace(/\D/g, '').slice(-1);
    e.target.value = val;

    if (val) {
      e.target.classList.add('filled');
      if (idx < boxes.length - 1) {
        boxes[idx + 1].focus();
      } else {
        // Auto-submit when last box filled
        const code = boxes.map(b => b.value).join('');
        if (code.length === 6) verifyOtp();
      }
    } else {
      e.target.classList.remove('filled');
    }
  }

  function onOtpPaste(e) {
    e.preventDefault();
    const pasted = toAsciiDigits((e.clipboardData || window.clipboardData).getData('text'));
    const digits = pasted.replace(/\D/g, '').slice(0, 6);
    const boxes = el.otpBoxes();
    digits.split('').forEach((d, i) => {
      if (boxes[i]) {
        boxes[i].value = d;
        boxes[i].classList.add('filled');
      }
    });
    const lastFilled = Math.min(digits.length, boxes.length - 1);
    boxes[lastFilled]?.focus();
    if (digits.length === 6) setTimeout(verifyOtp, 100);
  }

  // ── OTP request ───────────────────────────────────────────────
  async function requestOtp() {
    const phoneRaw = el.inputPhone().value.trim();
    if (!phoneRaw) {
      el.phoneError().textContent = 'شماره موبایل را وارد کنید.';
      el.inputPhone().classList.add('error');
      return;
    }

    setRequestLoading(true);
    el.phoneError().textContent = '';
    el.inputPhone().classList.remove('error');

    try {
      const resp = await apiFetch('/api/auth/request-otp', {
        method: 'POST',
        body: JSON.stringify({ phone: phoneRaw })
      });
      const data = await resp.json();

      if (!resp.ok) {
        el.phoneError().textContent = data.title || 'خطایی رخ داده است.';
        el.inputPhone().classList.add('error');
        return;
      }

      rawPhone = phoneRaw;
      // Show OTP card
      el.otpPhoneDisplay().textContent = formatPhoneDisplay(toAsciiDigits(phoneRaw));
      el.otpBoxes().forEach(b => { b.value = ''; b.classList.remove('filled', 'error'); });
      el.otpError().textContent = '';
      showCard('otp');
      startCountdown(60);

    } catch (err) {
      el.phoneError().textContent = 'خطا در برقراری ارتباط با سرور.';
    } finally {
      setRequestLoading(false);
    }
  }

  // ── OTP verification ──────────────────────────────────────────
  async function verifyOtp() {
    const boxes = el.otpBoxes();
    const code = boxes.map(b => b.value).join('');

    if (code.length < 6) {
      el.otpError().textContent = 'لطفاً کد ۶ رقمی را کامل وارد کنید.';
      boxes.forEach(b => b.classList.add('error'));
      return;
    }

    setVerifyLoading(true);
    el.otpError().textContent = '';

    try {
      const resp = await apiFetch('/api/auth/verify-otp', {
        method: 'POST',
        body: JSON.stringify({ phone: rawPhone, otp: code })
      });
      const data = await resp.json();

      if (!resp.ok || !data.success) {
        el.otpError().textContent = data.message || 'کد تأیید نادرست است.';
        boxes.forEach(b => b.classList.add('error'));
        return;
      }

      // Auth successful
      clearCountdown();
      showScreen('chat');
      window.Chat && Chat.init();

    } catch (err) {
      el.otpError().textContent = 'خطا در برقراری ارتباط با سرور.';
    } finally {
      setVerifyLoading(false);
    }
  }

  // ── Countdown ─────────────────────────────────────────────────
  function startCountdown(seconds) {
    clearCountdown();
    el.btnResend().classList.add('hidden');
    el.countdownSpan().classList.remove('hidden');

    let remaining = seconds;

    function tick() {
      const mins = Math.floor(remaining / 60);
      const secs = remaining % 60;
      el.countdownSpan().textContent =
        `ارسال مجدد کد در ${toPersianDigits(mins)}:${toPersianDigits(String(secs).padStart(2,'0'))}`;
      remaining--;

      if (remaining < 0) {
        el.countdownSpan().classList.add('hidden');
        el.btnResend().classList.remove('hidden');
        return;
      }
      countdownTimer = setTimeout(tick, 1000);
    }
    tick();
  }

  function clearCountdown() {
    if (countdownTimer) { clearTimeout(countdownTimer); countdownTimer = null; }
  }

  // ── Init ──────────────────────────────────────────────────────
  function init() {
    // Phone input
    el.inputPhone().addEventListener('input', onPhoneInput);
    el.inputPhone().addEventListener('keydown', onPhoneKeydown);
    el.btnRequestOtp().addEventListener('click', requestOtp);

    // OTP inputs
    el.otpBoxes().forEach(box => {
      box.addEventListener('keydown', onOtpKeydown);
      box.addEventListener('input', onOtpInput);
      box.addEventListener('paste', onOtpPaste);
    });

    el.btnVerifyOtp().addEventListener('click', verifyOtp);

    el.btnResend().addEventListener('click', async () => {
      el.btnResend().classList.add('hidden');
      await requestOtp();
    });

    el.btnBackPhone().addEventListener('click', () => {
      clearCountdown();
      showCard('phone');
    });
  }

  return { init, showScreen };
})();

window.Auth = Auth;
