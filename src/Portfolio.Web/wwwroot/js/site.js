(function () {
  'use strict';

  var root = document.documentElement;

  /* Day/night toggle: follows the system setting until the visitor chooses. */
  var themeBtn = document.querySelector('.theme-toggle');
  if (themeBtn) {
    themeBtn.addEventListener('click', function () {
      var current = root.getAttribute('data-theme') ||
        (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
      var next = current === 'dark' ? 'light' : 'dark';
      root.setAttribute('data-theme', next);
      try { localStorage.setItem('theme', next); } catch (e) { /* storage unavailable */ }
    });
  }

  /* Mobile navigation */
  var navBtn = document.querySelector('.nav-toggle');
  var navMenu = document.getElementById('nav-menu');
  if (navBtn && navMenu) {
    var setOpen = function (open) {
      navBtn.setAttribute('aria-expanded', String(open));
      navMenu.classList.toggle('is-open', open);
    };
    navBtn.addEventListener('click', function () {
      setOpen(navBtn.getAttribute('aria-expanded') !== 'true');
    });
    navMenu.addEventListener('click', function (e) {
      if (e.target.closest('a')) setOpen(false);
    });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && navBtn.getAttribute('aria-expanded') === 'true') {
        setOpen(false);
        navBtn.focus();
      }
    });
    document.addEventListener('click', function (e) {
      if (!e.target.closest('.nav')) setOpen(false);
    });
  }

  /* Confirm before destructive admin actions (forms marked data-confirm). */
  document.addEventListener('submit', function (e) {
    var form = e.target;
    var message = form.getAttribute && form.getAttribute('data-confirm');
    if (message && !window.confirm(message)) e.preventDefault();
  });

  /* Contact form: instant feedback in the browser. The server always validates again. */
  var form = document.querySelector('[data-contact-form]');
  if (!form) return;

  var status = form.querySelector('[data-form-status]');
  var rules = {
    Name: function (v) {
      if (!v) return 'Enter your name.';
      return v.length > 100 ? 'Keep your name under 100 characters.' : '';
    },
    Email: function (v) {
      if (!v) return 'Enter your email address.';
      return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v) ? '' : 'Enter an email address like name@example.com.';
    },
    Message: function (v) {
      if (!v) return 'Enter a message.';
      return v.length < 10 || v.length > 2000 ? 'Your message needs between 10 and 2,000 characters.' : '';
    }
  };

  function check(field) {
    var msg = rules[field.name](field.value.trim());
    var err = document.getElementById(field.id + '-err');
    field.setAttribute('aria-invalid', msg ? 'true' : 'false');
    if (err) {
      err.textContent = msg;
      err.hidden = !msg;
    }
    return !msg;
  }

  Object.keys(rules).forEach(function (name) {
    var field = form.elements[name];
    if (!field) return;
    field.addEventListener('blur', function () {
      if (field.value.trim() || field.getAttribute('aria-invalid') === 'true') check(field);
    });
    field.addEventListener('input', function () {
      if (field.getAttribute('aria-invalid') === 'true') check(field);
    });
  });

  form.addEventListener('submit', function (e) {
    var firstInvalid = null;
    Object.keys(rules).forEach(function (name) {
      var field = form.elements[name];
      if (field && !check(field) && !firstInvalid) firstInvalid = field;
    });

    if (firstInvalid) {
      e.preventDefault();
      if (status) {
        status.className = 'form-status is-error';
        status.textContent = 'Please fix the highlighted fields.';
        status.hidden = false;
      }
      firstInvalid.focus();
      return;
    }

    // Valid: let the browser submit, and stop double clicks from sending twice.
    var button = form.querySelector('button[type="submit"]');
    if (button) {
      button.disabled = true;
      button.textContent = 'Sending…';
    }
  });
})();
