// Applies a saved day/night choice before the page paints, so it never flashes the wrong theme.
(function () {
  try {
    var theme = localStorage.getItem('theme');
    if (theme === 'light' || theme === 'dark') {
      document.documentElement.setAttribute('data-theme', theme);
    }
  } catch (e) { /* storage blocked: follow the system setting */ }
})();
