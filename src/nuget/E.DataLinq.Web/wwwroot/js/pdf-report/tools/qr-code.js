/*
 * PDF report editing tool: QR code (own toolbar button)
 *
 * Asks for the QR code settings (URL, mail, geolocation, Girocode) after placing and sends them as JSON text:
 *   { kind: 'qr', qrType, url | reciever/subject/body/mailEncoding | latitude/longitude/geolocationEncoding |
 *     iban/bic/name/amount/purpose, htmlAttributes }
 * The source template and actions (@(PDF.Generate...QRCode(...)), qr.get / qr.set) live in
 * DataLinq Code editor/pdf-editing/tools/qr-code.js (same type id 'qr').
 * Editing (double click / "Edit image") is hooked into tools/image.js via window.pdfImageEditHandlers:
 * if the element contains a QR code call, the QR dialog is opened instead of the image dialog.
 */
(function () {
    const icon = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/>' +
        '<path d="M14 14h3v3h-3zM20 14v.01M14 20h.01M17 20h4v-3"/></svg>';

    const ATTRIBUTE = /^[A-Za-z_][A-Za-z0-9_-]*$/;
    const FORBIDDEN_ATTRIBUTE = /^(src|srcset|on.*|asp-.*)$/i;
    const UNSAFE_STYLE = /[\\(]|\/\*|expression|javascript:/i;

    const QR_TYPES = [
        { value: 'url', label: 'URL', description: 'Link to a web page' },
        { value: 'mail', label: 'Mail', description: 'Prepared e-mail' },
        { value: 'geo', label: 'Geolocation', description: 'Coordinates' },
        { value: 'giro', label: 'Girocode', description: 'SEPA payment' }
    ];
    const FIELDS = ['qrType', 'url', 'reciever', 'subject', 'body', 'mailEncoding', 'latitude', 'longitude', 'geolocationEncoding', 'iban', 'bic', 'name', 'amount', 'purpose'];

    const isType = type => v => (v.qrType || 'url') === type;

    const validateAttributes = function (pairs) {
        const bad = (pairs || []).find(p => FORBIDDEN_ATTRIBUTE.test(p.key) || (p.key.toLowerCase() === 'style' && UNSAFE_STYLE.test(p.value)));
        return bad ? 'HTML attributes: "' + bad.key + '" is not allowed for QR codes.' : null;
    };

    const openDialog = function (initial, submitLabel) {
        initial = initial || {};
        return pdfEditDialog.open({
            title: 'QR code',
            submitLabel: submitLabel,
            steps: [{
                title: 'QR code type',
                fields: [
                    { name: 'qrType', label: 'Type', type: 'choice', value: initial.qrType || 'url', options: QR_TYPES }
                ]
            }, {
                title: 'Content',
                description: 'Plain text; may contain DLH.QueryParameter("name"), DLH.GetConstant("name") or SECURITY.GetSecret("name") anywhere (resolved on the server), e.g. https://example.com/?id=DLH.QueryParameter("id").',
                fields: [
                    { name: 'url', label: 'URL', type: 'text', value: initial.url || '', placeholder: 'https://example.com/object/4711', required: true, showIf: isType('url') },
                    { name: 'reciever', label: 'Receiver', type: 'text', value: initial.reciever || '', placeholder: 'name@example.com', required: true, showIf: isType('mail') },
                    { name: 'subject', label: 'Subject', type: 'text', value: initial.subject || '', showIf: isType('mail') },
                    { name: 'body', label: 'Body', type: 'textarea', rows: 3, value: initial.body || '', showIf: isType('mail') },
                    { name: 'mailEncoding', label: 'Encoding', type: 'select', value: initial.mailEncoding || 'MAILTO', options: ['MAILTO', 'MATMSG', 'SMTP'], showIf: isType('mail') },
                    { name: 'latitude', label: 'Latitude', type: 'text', value: initial.latitude || '', placeholder: '47.05566', required: true, showIf: isType('geo') },
                    { name: 'longitude', label: 'Longitude', type: 'text', value: initial.longitude || '', placeholder: '15.4365', required: true, showIf: isType('geo') },
                    { name: 'geolocationEncoding', label: 'Encoding', type: 'select', value: initial.geolocationEncoding || 'GEO', options: ['GEO', 'GoogleMaps'], showIf: isType('geo') },
                    { name: 'iban', label: 'IBAN', type: 'text', value: initial.iban || '', required: true, showIf: isType('giro') },
                    { name: 'bic', label: 'BIC', type: 'text', value: initial.bic || '', required: true, showIf: isType('giro') },
                    { name: 'name', label: 'Name', type: 'text', value: initial.name || '', required: true, showIf: isType('giro') },
                    { name: 'amount', label: 'Amount', type: 'text', value: initial.amount || '', placeholder: '0.00', pattern: /^(\d{1,12}([.,]\d{1,2})?)?$/, showIf: isType('giro') },
                    { name: 'purpose', label: 'Purpose', type: 'text', value: initial.purpose || '', showIf: isType('giro') },
                    { name: 'htmlAttributes', label: 'HTML attributes', type: 'keyvalue', value: initial.htmlAttributes || [], keyPattern: ATTRIBUTE, keyPlaceholder: 'Attribute', addLabel: 'Add attribute', emptyText: 'No attributes', validate: validateAttributes }
                ]
            }]
        }).then(values => {
            if (!values) return null;
            const result = { kind: 'qr', parameters: [], htmlAttributes: values.htmlAttributes || [] };
            FIELDS.forEach(name => {
                if (values[name] !== undefined) result[name] = typeof values[name] === 'string' ? values[name].trim() : values[name];
            });
            return result;
        });
    };

    // ---- edit an existing QR code -----------------------------------------------------------

    const locate = element => ({
        index: parseInt(element.getAttribute('data-dl-edit-index'), 10),
        originalX: parseFloat(element.getAttribute('data-dl-edit-ox')) || 0,
        originalY: parseFloat(element.getAttribute('data-dl-edit-oy')) || 0
    });

    // returns true if the element is a QR code (handled here), false to let the image tool handle it
    const editQrCode = async function (element) {
        const current = await pdfEditActions.request('qr.get', locate(element));
        if (!current.success || !current.data) {
            if (current.message && !/does not contain/.test(current.message)) {
                pdfEditActions.notify(current.message);
                return true;
            }
            return false;
        }

        const settings = await openDialog(current.data, 'Apply');
        if (!settings || !element.isConnected) return true;

        const result = await pdfEditActions.request('qr.set', Object.assign(locate(element), { settings: settings }));
        if (!result.success) {
            pdfEditActions.notify(result.message || 'The QR code could not be changed.');
            return true;
        }

        const page = pdfEditActions.pageOf(element.closest('.page'));
        pdfEditActions.saveAndReload(page ? page.sourceIndex : 0);
        return true;
    };

    (window.pdfImageEditHandlers = window.pdfImageEditHandlers || []).push(editQrCode);

    // ---- add a new QR code ------------------------------------------------------------------

    pdfEditElementTypes.register({
        type: 'qr',
        label: 'QR code',
        icon: icon,
        saveAfterAdd: true,
        prompt: function () {
            return openDialog(null, 'Insert').then(settings => settings ? JSON.stringify(settings) : null);
        },
        create: function () {
            const box = document.createElement('div');
            box.className = 'pdf-edit-image-placeholder';
            box.innerHTML = icon + '<span></span>';
            box.querySelector('span').textContent = 'QR code';
            return box;
        },
        textTarget: function () {
            return null;
        }
    });
})();
