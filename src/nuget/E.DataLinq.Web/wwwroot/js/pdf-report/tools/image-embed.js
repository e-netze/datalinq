/*
 * PDF report editing tool: embedded images (used by tools/image.js, kind 'embedded')
 *
 * Turns a local PNG/JPEG file into a data URI that is stored in the report source as
 *   <img src="data:image/(png|jpeg);base64,..." />
 * Security:
 *   - only PNG/JPEG, detected by magic bytes (file name and file.type are not trusted)
 *   - input size/dimension limits (no decompression bombs)
 *   - the image is always re-encoded on a canvas (strips metadata and appended payloads)
 *   - the result is checked against a strict data URI pattern (base64 cannot contain @ " < > or whitespace,
 *     so it can neither start Razor code nor break out of the html attribute)
 * The same data URI check is repeated by the source template (DataLinq Code editor/pdf-editing/tools/image.js).
 */
window.pdfImageEmbed = (function () {
    const MAX_INPUT_BYTES = 5 * 1024 * 1024;
    const MAX_INPUT_DIMENSION = 10000;
    const MAX_OUTPUT_DIMENSION = 2000;
    const MAX_OUTPUT_BYTES = 200 * 1024;
    const JPEG_QUALITIES = [0.85, 0.75, 0.65, 0.55];
    const DATA_URL = /^data:image\/(png|jpeg);base64,[A-Za-z0-9+/]+={0,2}$/;

    const decodedBytes = function (dataUrl) {
        const base64 = dataUrl.substring(dataUrl.indexOf(',') + 1);
        const padding = base64.endsWith('==') ? 2 : base64.endsWith('=') ? 1 : 0;
        return base64.length / 4 * 3 - padding;
    };

    const isValidDataUrl = function (value) {
        if (typeof value !== 'string' || !DATA_URL.test(value)) return false;
        const base64Length = value.length - value.indexOf(',') - 1;
        return base64Length % 4 === 0 && decodedBytes(value) <= MAX_OUTPUT_BYTES;
    };

    const formatBytes = bytes => bytes < 1024 ? bytes + ' B' : (bytes / 1024).toFixed(1) + ' KB';

    const sniffType = async function (file) {
        const head = new Uint8Array(await file.slice(0, 8).arrayBuffer());
        const png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (head.length >= 8 && png.every((b, i) => head[i] === b)) return 'png';
        if (head.length >= 3 && head[0] === 0xFF && head[1] === 0xD8 && head[2] === 0xFF) return 'jpeg';
        return null;
    };

    const hasTransparency = function (context, width, height) {
        const data = context.getImageData(0, 0, width, height).data;
        for (let i = 3; i < data.length; i += 4) {
            if (data[i] < 255) return true;
        }
        return false;
    };

    const render = function (bitmap, scale) {
        const canvas = document.createElement('canvas');
        canvas.width = Math.max(1, Math.round(bitmap.width * scale));
        canvas.height = Math.max(1, Math.round(bitmap.height * scale));
        const context = canvas.getContext('2d');
        context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        return { canvas, context };
    };

    // File -> { dataUrl, width, height, bytes }; throws Error with a user facing message
    const process = async function (file) {
        if (!(file instanceof Blob)) throw new Error('No file selected.');
        if (file.size > MAX_INPUT_BYTES) throw new Error('The file is too large (max. ' + formatBytes(MAX_INPUT_BYTES) + ').');

        const type = await sniffType(file);
        if (!type) throw new Error('Only PNG and JPEG images are allowed.');

        let bitmap;
        try {
            bitmap = await createImageBitmap(file);
        } catch (e) {
            throw new Error('The image could not be read.');
        }

        try {
            if (bitmap.width > MAX_INPUT_DIMENSION || bitmap.height > MAX_INPUT_DIMENSION) {
                throw new Error('The image is too large (max. ' + MAX_INPUT_DIMENSION + ' px per side).');
            }

            let scale = Math.min(1, MAX_OUTPUT_DIMENSION / Math.max(bitmap.width, bitmap.height));
            let first = render(bitmap, scale);
            const transparent = type === 'png' && hasTransparency(first.context, first.canvas.width, first.canvas.height);

            for (let attempt = 0; attempt < 12; attempt++) {
                const current = attempt === 0 ? first : render(bitmap, scale);
                const candidates = transparent
                    ? [current.canvas.toDataURL('image/png')]
                    : JPEG_QUALITIES.map(q => current.canvas.toDataURL('image/jpeg', q));
                for (const dataUrl of candidates) {
                    if (isValidDataUrl(dataUrl)) {
                        return { dataUrl, width: current.canvas.width, height: current.canvas.height, bytes: decodedBytes(dataUrl) };
                    }
                }
                scale *= 0.75;
                if (bitmap.width * scale < 16 || bitmap.height * scale < 16) break;
            }
            throw new Error('The image could not be reduced to ' + formatBytes(MAX_OUTPUT_BYTES) + '.');
        } finally {
            bitmap.close();
        }
    };

    // custom dialog field: file picker + preview, the value is the (validated) data URI or ''
    const createField = function (value) {
        const box = document.createElement('div');
        box.className = 'pdf-edit-dialog-image-embed';
        box._dataUrl = isValidDataUrl(value) ? value : '';

        const input = document.createElement('input');
        input.type = 'file';
        input.accept = 'image/png,image/jpeg';

        const preview = document.createElement('img');
        preview.alt = '';
        preview.style.maxWidth = '160px';
        preview.style.maxHeight = '120px';
        preview.style.display = 'block';
        preview.style.marginTop = '6px';

        const info = document.createElement('small');
        info.style.display = 'block';

        const show = function (text) {
            preview.hidden = !box._dataUrl;
            if (box._dataUrl) preview.src = box._dataUrl; else preview.removeAttribute('src');
            info.textContent = text || (box._dataUrl ? formatBytes(decodedBytes(box._dataUrl)) : 'PNG or JPEG, max. ' + formatBytes(MAX_INPUT_BYTES) + ' (stored max. ' + formatBytes(MAX_OUTPUT_BYTES) + ')');
        };

        input.addEventListener('change', async function () {
            const file = input.files && input.files[0];
            if (!file) return;
            info.textContent = 'Processing...';
            try {
                const result = await process(file);
                box._dataUrl = result.dataUrl;
                show(result.width + ' x ' + result.height + ' px, ' + formatBytes(result.bytes));
            } catch (e) {
                box._dataUrl = '';
                show(e && e.message ? e.message : 'The image could not be processed.');
            }
            input.value = '';
            box.dispatchEvent(new Event('change', { bubbles: true }));
        });

        box.appendChild(input);
        box.appendChild(preview);
        box.appendChild(info);
        show();
        return box;
    };

    const readField = element => element._dataUrl || '';

    return { process, isValidDataUrl, createField, readField, MAX_OUTPUT_BYTES };
})();
