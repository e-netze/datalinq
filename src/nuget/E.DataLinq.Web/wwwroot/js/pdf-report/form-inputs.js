/*
 * PDF report - form inputs runtime (inputs placed with the editing tool js/pdf-report/tools/input.js).
 *
 *   - signature fields: draw with mouse / pen / touch into .dl-signature-canvas, clear button
 *   - required fields (.dl-input[data-required="true"]): Download PDF and Print are blocked while one
 *     is empty; the empty fields are highlighted and a modal tells the user to fill them in.
 *     Not checked in the editing mode (DataLinq Code live preview).
 */
window.pdfFormInputs = (function () {

    // ---- signature ----------------------------------------------------------------------------------

    const sizeCanvas = function (canvas) {
        const ratio = window.devicePixelRatio || 1;
        const w = Math.round(canvas.offsetWidth * ratio), h = Math.round(canvas.offsetHeight * ratio);
        if (w > 0 && h > 0 && (canvas.width !== w || canvas.height !== h)) {
            canvas.width = w;
            canvas.height = h;
        }
    };

    const pointOf = function (canvas, e) {
        const r = canvas.getBoundingClientRect();
        return {
            x: (e.clientX - r.left) * canvas.width / Math.max(1, r.width),
            y: (e.clientY - r.top) * canvas.height / Math.max(1, r.height)
        };
    };

    document.addEventListener('pointerdown', function (e) {
        const canvas = e.target instanceof Element ? e.target.closest('.dl-signature-canvas') : null;
        if (!canvas || e.button !== 0) return;
        if (document.body.classList.contains('pdf-edit-inputs-inert')) return;

        const pad = canvas.closest('.dl-signature');
        // resizing clears the canvas, only before the first stroke
        if (pad.getAttribute('data-signed') !== 'true') sizeCanvas(canvas);

        e.preventDefault();
        canvas.setPointerCapture(e.pointerId);
        const ctx = canvas.getContext('2d');
        const ratio = canvas.width / Math.max(1, canvas.offsetWidth);
        ctx.strokeStyle = window.getComputedStyle(canvas).color || '#1f2328';
        ctx.lineWidth = 2 * ratio;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';

        let last = pointOf(canvas, e);
        ctx.beginPath();
        ctx.arc(last.x, last.y, ctx.lineWidth / 2, 0, Math.PI * 2);
        ctx.fillStyle = ctx.strokeStyle;
        ctx.fill();

        const move = function (ev) {
            const p = pointOf(canvas, ev);
            ctx.beginPath();
            ctx.moveTo(last.x, last.y);
            ctx.lineTo(p.x, p.y);
            ctx.stroke();
            last = p;
        };
        const end = function () {
            canvas.removeEventListener('pointermove', move);
            canvas.removeEventListener('pointerup', end);
            canvas.removeEventListener('pointercancel', end);
            pad.setAttribute('data-signed', 'true');
            changed(pad);
        };
        canvas.addEventListener('pointermove', move);
        canvas.addEventListener('pointerup', end);
        canvas.addEventListener('pointercancel', end);
    });

    document.addEventListener('click', function (e) {
        const button = e.target instanceof Element ? e.target.closest('.dl-signature-clear') : null;
        if (!button) return;
        const pad = button.closest('.dl-signature');
        const canvas = pad.querySelector('.dl-signature-canvas');
        canvas.getContext('2d').clearRect(0, 0, canvas.width, canvas.height);
        pad.removeAttribute('data-signed');
    });

    // ---- required validation ------------------------------------------------------------------------

    const isFilled = function (field) {
        switch (field.getAttribute('data-type')) {
            case 'checkbox': return !!field.querySelector('input[type=checkbox]:checked');
            case 'radio': return !!field.querySelector('input[type=radio]:checked');
            case 'signature': return field.querySelector('.dl-signature[data-signed="true"]') !== null;
            default: {
                const control = field.querySelector('.dl-input-control');
                return !!control && String(control.value || '').trim() !== '';
            }
        }
    };

    const changed = function (target) {
        const field = target.closest('.dl-input.dl-input-invalid');
        if (field && isFilled(field)) field.classList.remove('dl-input-invalid');
    };

    ['input', 'change'].forEach(type => document.addEventListener(type, function (e) {
        if (!(e.target instanceof Element)) return;
        // the PDF export clones the DOM (html-to-image), which copies values but not the checked state
        const field = e.target.closest('.dl-input');
        if (field) {
            field.querySelectorAll('input[type=checkbox], input[type=radio]').forEach(i => i.toggleAttribute('checked', i.checked));
        }
        // empty date fields hide the format text (form-inputs.css)
        if (e.target.matches('input[type=date].dl-input-control')) e.target.toggleAttribute('data-filled', e.target.value !== '');
        changed(e.target);
    }));

    const showModal = function () {
        if (document.querySelector('.dl-input-modal-backdrop')) return;
        const backdrop = document.createElement('div');
        backdrop.className = 'dl-input-modal-backdrop';
        backdrop.setAttribute('data-html2canvas-ignore', 'true');
        backdrop.innerHTML =
            '<div class="dl-input-modal" role="alertdialog" aria-modal="true" aria-labelledby="dl-input-modal-title">' +
            '<h3 id="dl-input-modal-title">Required fields missing</h3>' +
            '<p>Please fill in all required fields (highlighted in red) before downloading or printing.</p>' +
            '<button type="button">OK</button>' +
            '</div>';
        const close = function () {
            backdrop.remove();
            document.removeEventListener('keydown', onKey, true);
            const first = document.querySelector('.dl-input.dl-input-invalid');
            if (first) {
                first.scrollIntoView({ block: 'center', behavior: 'smooth' });
                const control = first.querySelector('input, textarea, select');
                if (control) control.focus({ preventScroll: true });
            }
        };
        const onKey = function (e) {
            if (e.key === 'Escape' || e.key === 'Enter') { e.preventDefault(); close(); }
        };
        backdrop.querySelector('button').addEventListener('click', close);
        backdrop.addEventListener('click', e => { if (e.target === backdrop) close(); });
        document.addEventListener('keydown', onKey, true);
        document.body.appendChild(backdrop);
        backdrop.querySelector('button').focus();
    };

    /**
     * Checks the required inputs before the report is downloaded or printed.
     * @returns {boolean} True when the output may be created.
     */
    const validateBeforeOutput = function () {
        if (typeof isPdfReportEditingMode === 'function' && isPdfReportEditingMode()) return true;

        const missing = Array.from(document.querySelectorAll('.page .dl-input[data-required="true"]'))
            .filter(field => !isFilled(field));
        document.querySelectorAll('.dl-input.dl-input-invalid').forEach(f => f.classList.remove('dl-input-invalid'));
        if (missing.length === 0) return true;

        missing.forEach(f => f.classList.add('dl-input-invalid'));
        showModal();
        return false;
    };

    // ---- default values (data-default, resolved on the server) --------------------------------------
    // empty / invalid values are ignored, the field keeps showing its placeholder

    const applyDefault = function (field) {
        if (field.hasAttribute('data-default-applied')) return;
        field.setAttribute('data-default-applied', 'true');
        const value = String(field.getAttribute('data-default') || '').trim();
        if (value === '') return;

        switch (field.getAttribute('data-type')) {
            case 'signature':
                return;
            case 'checkbox': {
                const box = field.querySelector('input[type=checkbox]');
                if (box && (value === 'true' || value === 'false')) {
                    box.checked = value === 'true';
                    box.toggleAttribute('checked', box.checked);
                }
                return;
            }
            case 'radio': {
                const radio = Array.from(field.querySelectorAll('input[type=radio]')).find(r => r.value === value);
                if (radio) {
                    radio.checked = true;
                    radio.setAttribute('checked', '');
                }
                return;
            }
            case 'select': {
                const select = field.querySelector('select');
                const option = select ? Array.from(select.options).find(o => o.value !== '' && o.value === value) : null;
                if (option) {
                    select.value = value;
                    option.setAttribute('selected', '');
                }
                return;
            }
            default: {
                const control = field.querySelector('.dl-input-control');
                if (!control) return;
                control.value = value;
                // invalid values for number / date inputs are dropped by the browser -> placeholder
                if (control.value === '') return;
                if (control.type === 'date') control.setAttribute('data-filled', '');
                if (control.tagName === 'TEXTAREA') control.textContent = value;
                else control.setAttribute('value', control.value);
            }
        }
    };

    const applyDefaults = root => root.querySelectorAll('.dl-input[data-default]').forEach(applyDefault);

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => applyDefaults(document));
    else applyDefaults(document);
    new MutationObserver(records => records.forEach(r => r.addedNodes.forEach(n => {
        if (!(n instanceof Element)) return;
        if (n.matches('.dl-input[data-default]')) applyDefault(n);
        applyDefaults(n);
    }))).observe(document.documentElement, { childList: true, subtree: true });

    // ---- placeholders are never printed / downloaded (entered and default values are) ----------------

    const hidePlaceholders = function () {
        const restore = [];
        document.querySelectorAll('.dl-input [placeholder]').forEach(el => {
            const value = el.getAttribute('placeholder');
            el.setAttribute('placeholder', '');
            restore.push(() => el.setAttribute('placeholder', value));
        });
        document.querySelectorAll('.dl-input select.dl-input-control').forEach(select => {
            const option = Array.from(select.options).find(o => o.value === '');
            if (!option) return;
            const text = option.textContent;
            option.textContent = '';
            restore.push(() => { option.textContent = text; });
        });
        document.body.classList.add('dl-input-output');
        restore.push(() => document.body.classList.remove('dl-input-output'));
        return () => restore.forEach(fn => fn());
    };

    let restorePrint = null;
    window.addEventListener('beforeprint', () => { if (!restorePrint) restorePrint = hidePlaceholders(); });
    window.addEventListener('afterprint', () => { if (restorePrint) { restorePrint(); restorePrint = null; } });

    return { validateBeforeOutput: validateBeforeOutput, applyDefault: applyDefault, hidePlaceholders: hidePlaceholders };
})();
