
/*
 * PDF report editing tool: image
 *
 * Asks for the GetImage settings (base url, path, parameters, html attributes) after placing
 * and sends them as JSON text. Kind 'embedded' stores an uploaded PNG/JPEG (re-encoded, max 200 KB,
 * see tools/image-embed.js) as <img src="data:..."> in the source; src/srcset/on* attributes are not allowed.
 * menu ("Edit image"): the settings are read/written with the source actions image.get / image.set.
 * The source template and actions (@(await DLH.GetImage(...))) live in
 * DataLinq Code editor/pdf-editing/tools/image.js (same type id).
 */
(function () {
	const icon = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
		'<rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-5-5L5 21"/></svg>';

	const IDENTIFIER = /^[A-Za-z_][A-Za-z0-9_]*$/;
	const ATTRIBUTE = /^[A-Za-z_][A-Za-z0-9_-]*$/;

	const KINDS = [
		{ value: 'image', label: 'Image', description: 'Public image ' },
		{ value: 'ags', label: 'AGS image', description: 'ArcGIS map/image ' },
		{ value: 'secureAgs', label: 'Secure AGS image', description: 'ArcGIS service with token' },
		{ value: 'embedded', label: 'Embedded image', description: 'Uploaded PNG/JPEG stored in the report (max 200 KB)' }
	];

	// attributes that must never be set on embedded images (src is the data URI, no event handlers)
	// asp-*: Razor tag helpers; style: no functions/escapes/comments (blocks url(), u\72l(), image-set(), ...)
	const FORBIDDEN_ATTRIBUTE = /^(src|srcset|on.*|asp-.*)$/i;
	const UNSAFE_STYLE = /[\\(]|\/\*|expression|javascript:/i;

	const isImage = v => v.kind === 'image';
	const isAgs = v => v.kind === 'ags' || v.kind === 'secureAgs';
	const isSecure = v => v.kind === 'secureAgs';
	const isEmbedded = v => v.kind === 'embedded';

	const validateEmbeddedAttributes = function (pairs, values) {
		if (!isEmbedded(values)) return null;
		const bad = (pairs || []).find(p => FORBIDDEN_ATTRIBUTE.test(p.key) || (p.key.toLowerCase() === 'style' && UNSAFE_STYLE.test(p.value)));
		return bad ? 'HTML attributes: "' + bad.key + '" is not allowed for this image type.' : null;
	};

	const openDialog = function (initial, submitLabel) {
		initial = initial || {};
		return pdfEditDialog.open({
			title: 'Image',
			submitLabel: submitLabel,
			steps: [{
				title: 'Image type',
				fields: [
					{ name: 'kind', label: 'Type', type: 'choice', value: initial.kind || 'image', options: KINDS }
				]
			}, {
				title: 'Settings',
				description: 'Values are plain text, or DLH.GetConstant("name") / SECURITY.GetSecret("name") (resolved on the server).',
				fields: [
					{ name: 'baseUrl', label: 'Base url', type: 'text', value: initial.baseUrl || '', placeholder: 'https://server/images', required: true, showIf: isImage },
					{ name: 'path', label: 'Path', type: 'text', value: initial.path || '', placeholder: 'folder/image.png', required: true, showIf: isImage },
					{ name: 'serviceType', label: 'Service type', type: 'select', value: initial.serviceType || 'MapService', options: ['MapService', 'ImageService'], showIf: isAgs },
					{ name: 'serverKey', label: 'Server key', type: 'text', value: initial.serverKey || '', required: true, showIf: isAgs },
					{ name: 'serviceName', label: 'Service name', type: 'text', value: initial.serviceName || '', placeholder: 'folder/service', required: true, showIf: isAgs },
					{ name: 'usernameKey', label: 'Username key', type: 'text', value: initial.usernameKey || '', required: true, showIf: isSecure },
					{ name: 'passwordKey', label: 'Password key', type: 'text', value: initial.passwordKey || '', required: true, showIf: isSecure },
					{
						name: 'dataUrl', label: 'Image file', type: 'custom', value: initial.dataUrl || '', required: true, showIf: isEmbedded,
						create: pdfImageEmbed.createField, read: pdfImageEmbed.readField,
						validate: v => v && !pdfImageEmbed.isValidDataUrl(v) ? 'Image file: invalid image data.' : null
					},
					{ name: 'parameters', label: 'Parameters', type: 'keyvalue', value: initial.parameters || [], keyPattern: IDENTIFIER, addLabel: 'Add parameter', emptyText: 'No parameters', showIf: v => !isEmbedded(v) },
					{ name: 'htmlAttributes', label: 'HTML attributes', type: 'keyvalue', value: initial.htmlAttributes || [], keyPattern: ATTRIBUTE, keyPlaceholder: 'Attribute', addLabel: 'Add attribute', emptyText: 'No attributes', validate: validateEmbeddedAttributes }
				]
			}]
		}).then(values => {
			if (!values) return null;
			const trim = v => typeof v === 'string' ? v.trim() : v;
			const result = { kind: values.kind, parameters: isEmbedded(values) ? [] : values.parameters || [], htmlAttributes: values.htmlAttributes || [] };
			if (isEmbedded(values)) {
				result.dataUrl = values.dataUrl;
				return result;
			}
			['baseUrl', 'path', 'serviceType', 'serverKey', 'serviceName', 'usernameKey', 'passwordKey']
				.forEach(name => { if (values[name] !== undefined) result[name] = trim(values[name]); });
			return result;
		});
	};

	// ---- edit an existing image -------------------------------------------------------------

	const bridge = () => window.dataLinqEditBridge;

	const isEditableImage = element =>
		!!(bridge() && bridge().isConnected() &&
			!element.classList.contains('pdf-edit-pending') &&
			Number.isInteger(parseInt(element.getAttribute('data-dl-edit-index'), 10)) &&
			element.querySelector('img'));

	const locate = element => ({
		index: parseInt(element.getAttribute('data-dl-edit-index'), 10),
		originalX: parseFloat(element.getAttribute('data-dl-edit-ox')) || 0,
		originalY: parseFloat(element.getAttribute('data-dl-edit-oy')) || 0
	});

	let busy = false;

	const editImage = async function (element) {
		if (busy || !isEditableImage(element)) return;
		busy = true;
		try {
			// other tools rendering an <img> (e.g. tools/qr-code.js) may handle the element first
			for (const handler of window.pdfImageEditHandlers || []) {
				if (await handler(element)) return;
			}

			const current = await pdfEditActions.request('image.get', locate(element));
			if (!current.success || !current.data) {
				pdfEditActions.notify(current.message || 'The image settings could not be read.');
				return;
			}

			const settings = await openDialog(current.data, 'Apply');
			if (!settings || !element.isConnected) return;

			const result = await pdfEditActions.request('image.set', Object.assign(locate(element), { settings: settings }));
			if (!result.success) {
				pdfEditActions.notify(result.message || 'The image could not be changed.');
				return;
			}

			const page = pdfEditActions.pageOf(element.closest('.page'));
			pdfEditActions.saveAndReload(page ? page.sourceIndex : 0);
		} finally {
			busy = false;
		}
	};

	document.addEventListener('dblclick', function (e) {
		const element = e.target instanceof Element ? e.target.closest('.page .element') : null;
		if (element && isEditableImage(element)) {
			e.preventDefault();
			editImage(element);
		}
	});

	pdfEditContextMenu.register({
		id: 'edit-image',
		label: 'Edit image',
		icon: icon,
		when: isEditableImage,
		run: editImage
	});

	// ---- add a new image --------------------------------------------------------------------

	pdfEditElementTypes.register({
		type: 'image',
		label: 'Image',
		icon: icon,
		saveAfterAdd: true,
		prompt: function () {
			return openDialog(null, 'Insert').then(settings => settings ? JSON.stringify(settings) : null);
		},
		create: function (text) {
			let config = {};
			try { config = JSON.parse(text); } catch (e) { /* placeholder only */ }

			const box = document.createElement('div');
			box.className = 'pdf-edit-image-placeholder';
			box.innerHTML = icon + '<span></span>';
			box.querySelector('span').textContent = config.kind === 'embedded' ? 'Embedded image' : config.path || config.serviceName || 'Image';
			return box;
		},
		textTarget: function () {
			return null;
		}
	});
})();
