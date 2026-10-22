// DataLinq PDF report render check.
// Run in the browser page opened from the MCP tool get_preview_link
// (e.g. Playwright MCP "browser_evaluate" with this function body).
// Returns page count, per-page overflow/clipping, fonts in use and broken images.
() => {
  const pages = [...document.querySelectorAll('.page')];
  const sx = window.scrollX, sy = window.scrollY;
  const result = pages.map((p, i) => {
    const r = p.getBoundingClientRect();
    const clipped = [];
    for (const e of p.querySelectorAll('*')) {
      if (clipped.length >= 10) break;
      if (e.childElementCount > 0) continue;
      const b = e.getBoundingClientRect();
      if (b.width === 0 && b.height === 0) continue;
      const over = Math.max(b.bottom - r.bottom, b.right - r.right);
      if (over > 1) clipped.push({ tag: e.tagName.toLowerCase(), cls: typeof e.className === 'string' ? e.className : '', text: (e.textContent || '').trim().slice(0, 80), overflow_px: Math.round(over) });
    }
    const inner = [];
    for (const e of p.querySelectorAll('*')) {
      if (inner.length >= 10) break;
      const cs = getComputedStyle(e);
      if ((cs.overflow === 'hidden' || cs.overflowY === 'hidden') && (e.scrollHeight > e.clientHeight + 1 || e.scrollWidth > e.clientWidth + 1))
        inner.push({ tag: e.tagName.toLowerCase(), cls: typeof e.className === 'string' ? e.className : '', hidden_px: Math.max(e.scrollHeight - e.clientHeight, e.scrollWidth - e.clientWidth) });
    }
    return {
      index: i + 1, x: r.left + sx, y: r.top + sy, width: r.width, height: r.height,
      scroll_height: p.scrollHeight, client_height: p.clientHeight,
      overflows: p.scrollHeight > p.clientHeight + 1 || p.scrollWidth > p.clientWidth + 1 || clipped.length > 0,
      clipped_elements: clipped, hidden_overflow_containers: inner
    };
  });
  const fonts = {};
  document.querySelectorAll('.page p, .page td, .page th, .page span, .page div').forEach(e => {
    if (!e.childNodes.length || ![...e.childNodes].some(n => n.nodeType === 3 && n.textContent.trim())) return;
    const c = getComputedStyle(e);
    const k = c.fontFamily.split(',')[0].replace(/["']/g, '').trim() + ' ' + c.fontSize;
    fonts[k] = (fonts[k] || 0) + 1;
  });
  const broken_images = [...document.querySelectorAll('.page img')].filter(i => !i.complete || i.naturalWidth === 0).map(i => i.src.slice(0, 200));
  const issues = [];
  if (!pages.length) issues.push('No .page elements rendered: ' + (document.body ? document.body.innerText.trim().slice(0, 300) : ''));
  result.filter(p => p.overflows).forEach(p => issues.push(`Page ${p.index}: content is cut off (scrollHeight ${p.scroll_height} > clientHeight ${p.client_height}, ${p.clipped_elements.length} clipped element(s))`));
  result.filter(p => p.hidden_overflow_containers.length).forEach(p => issues.push(`Page ${p.index}: ${p.hidden_overflow_containers.length} container(s) hide overflowing content`));
  if (broken_images.length) issues.push(`${broken_images.length} broken image(s)`);
  return { title: document.title, page_count: pages.length, pages: result, fonts, broken_images, issues, success: issues.length === 0 };
}
