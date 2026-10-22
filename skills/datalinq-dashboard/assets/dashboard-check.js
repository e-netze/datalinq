// Run inside the rendered DataLinq dashboard preview (browser tool "evaluate").
// Returns a JSON report; it does not change the page.
(() => {
  const issues = [];
  const add = (kind, msg, el) => issues.push({ kind, msg, where: el ? describe(el) : undefined });
  const describe = el => {
    let s = el.tagName.toLowerCase();
    if (el.id) s += '#' + el.id;
    if (el.classList && el.classList.length) s += '.' + [...el.classList].slice(0, 2).join('.');
    return s;
  };
  const text = document.body ? document.body.innerText : '';

  if (/CompilationErrorException|Razor|error CS\d{4}|Exception:/i.test(text)) add('razor', 'Error text rendered in page');
  for (const word of ['undefined', 'NaN', '[object Object]']) {
    if (new RegExp('\\b' + word.replace(/[[\]]/g, '\\$&') + '\\b').test(text)) add('value', `"${word}" visible in page`);
  }
  document.querySelectorAll('img').forEach(img => { if (img.complete && img.naturalWidth === 0) add('image', 'Broken image ' + img.src, img); });
  document.querySelectorAll('table').forEach(t => { if (t.tBodies.length && ![...t.tBodies].some(b => b.rows.length)) add('empty', 'Table without rows', t); });
  document.querySelectorAll('svg, canvas').forEach(c => {
    const r = c.getBoundingClientRect();
    if (r.width > 0 && r.height > 0 && c.tagName === 'svg' && !c.querySelector('*')) add('empty', 'Empty chart', c);
  });
  const vw = document.documentElement.clientWidth;
  if (document.documentElement.scrollWidth > vw + 2) add('layout', `Horizontal overflow (${document.documentElement.scrollWidth}px > ${vw}px)`);
  document.querySelectorAll('body *').forEach(el => {
    const r = el.getBoundingClientRect();
    if (r.width && r.right > vw + 2 && getComputedStyle(el).position !== 'fixed') { add('layout', 'Element exceeds viewport', el); }
  });
  const failed = performance.getEntriesByType('resource').filter(e => e.responseStatus >= 400).map(e => `${e.responseStatus} ${e.name}`);
  failed.forEach(f => add('request', 'Failed request ' + f));
  if (window.__dlErrors && window.__dlErrors.length) window.__dlErrors.forEach(e => add('js', e));

  return JSON.stringify({ url: location.pathname, viewport: vw, issues: issues.slice(0, 50), count: issues.length }, null, 2);
})();
