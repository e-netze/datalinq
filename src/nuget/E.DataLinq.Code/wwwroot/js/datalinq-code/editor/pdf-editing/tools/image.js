/**
 * PDF editing tool: image
 *
 * Source template for images added in the PDF preview (see E.DataLinq.Web pdf-report/tools/image.js).
 * The preview sends the settings as JSON text:
 *   { kind: 'image', baseUrl, path, parameters: [{key, value}], htmlAttributes: [{key, value}] }
 *     -> @(await DLH.GetImage("baseUrl", "path", new { key = "value" }, new { @class = "..." }))
 *   { kind: 'ags', serviceType, serverKey, serviceName, parameters, htmlAttributes }
 *     -> @(await DLH.GetAgsImage(AgsServiceType.MapService, "serverKey", "serviceName", new { ... }, new { ... }))
 *   { kind: 'secureAgs', serviceType, serverKey, serviceName, parameters, usernameKey, passwordKey, htmlAttributes }
 *     -> @(await SECURITY.GetAgsImage(AgsServiceType.MapService, "serverKey", "serviceName", new { ... },
 *              new AgsTokenObject { UsernameKey = "...", PasswordKey = "..." }, new { ... }))
 *   { kind: 'embedded', dataUrl, htmlAttributes }
 *     -> <img src="data:image/(png|jpeg);base64,..." key="value" />   (uploaded image, max 200 KB, see
 *        E.DataLinq.Web pdf-report/tools/image-embed.js). The data URI is checked against a strict pattern
 *        (base64 cannot contain @ " < > or whitespace), src/srcset/on* attributes and unsafe styles are
 *        rejected, attribute values are html encoded and @ is written as @@ (literal in Razor).
 *
 * Source actions (edit an existing image, double click / context menu in the preview):
 *   'image.get': { index, originalX, originalY } -> { data: settings } (first GetImage call / embedded img in the draggable)
 *   'image.set': { index, originalX, originalY, settings } -> replaces the @(await GetImage(...)) expression or img tag
 * Only string literals are editable; calls with expressions (variables, interpolation, ...) are rejected.
 * Depends on: pdf-editing/pdf-source.js
 */
(function () {
	var KEYWORDS = /^(abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|virtual|void|volatile|while)$/;
	var CALL = /\b(?:DLH|IH|DataLinqHelper|IntegratorHelper|SECURITY)\.(GetImage|GetAgsImage)\s*\(/g;
	var SERVICE_TYPES = ['MapService', 'ImageService'];
	var MAX_PAIRS = 50;

	// embedded images: strict data URI (base64 contains no @ " < > or whitespace -> no Razor/HTML injection)
	var DATA_URL = /^data:image\/(png|jpeg);base64,[A-Za-z0-9+\/]+={0,2}$/;
	var MAX_EMBEDDED_BYTES = 200 * 1024;
	var ATTRIBUTE_NAME = /^[A-Za-z][A-Za-z0-9-]*$/;
	// asp-*: Razor tag helpers; style: no functions/escapes/comments (blocks url(), u\72l(), image-set(), ...)
	var FORBIDDEN_ATTRIBUTE = /^(src|srcset|on.*|asp-.*)$/i;
	var UNSAFE_STYLE = /[\\(]|\/\*|expression|javascript:/i;

	var isValidDataUrl = function (value) {
		if (typeof value !== 'string' || !DATA_URL.test(value)) return false;
		var base64 = value.substring(value.indexOf(',') + 1);
		if (base64.length % 4 !== 0) return false;
		var padding = /==$/.test(base64) ? 2 : /=$/.test(base64) ? 1 : 0;
		return base64.length / 4 * 3 - padding <= MAX_EMBEDDED_BYTES;
	};

	var isAllowedAttribute = function (key, value) {
		if (typeof key !== 'string' || !ATTRIBUTE_NAME.test(key) || FORBIDDEN_ATTRIBUTE.test(key)) return false;
		if (typeof value !== 'string') return false;
		return !(key.toLowerCase() === 'style' && UNSAFE_STYLE.test(value));
	};

	// html attribute value -> safe inside "..." in Razor markup (@ -> @@ is emitted as a literal @)
	var htmlEncode = function (value) {
		return String(value)
			.replace(/&/g, '&amp;')
			.replace(/"/g, '&quot;')
			.replace(/</g, '&lt;')
			.replace(/>/g, '&gt;')
			.replace(/@/g, '@@');
	};

	var htmlDecode = function (value) {
		return value
			.replace(/&quot;/g, '"')
			.replace(/&#39;/g, "'")
			.replace(/&lt;/g, '<')
			.replace(/&gt;/g, '>')
			.replace(/&amp;/g, '&');
	};

	// ---- build ----------------------------------------------------------------------------

	// values may also be helper calls with a plain string argument (resolved on the backend):
	//   DLH.GetConstant("name"), SECURITY.GetSecret("name") -> emitted as code, not quoted
	var HELPER_VALUE = /^(?:(?:DLH|IH|DataLinqHelper|IntegratorHelper)\.GetConstant|SECURITY\.GetSecret)\(\s*"[^"\\\r\n]*"\s*\)$/;

	var normalizeHelper = function (text) {
		return text.trim().replace(/\(\s*/, '(').replace(/\s*\)$/, ')');
	};

	var csString = function (value) {
		if (typeof value === 'string' && HELPER_VALUE.test(value.trim())) {
			return normalizeHelper(value);
		}
		return '"' + String(value)
			.replace(/\\/g, '\\\\')
			.replace(/"/g, '\\"')
			.replace(/\r/g, '\\r')
			.replace(/\n/g, '\\n') + '"';
	};

	var csName = function (key) {
		var name = String(key).replace(/-/g, '_');
		if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(name)) return null;
		return KEYWORDS.test(name) ? '@' + name : name;
	};

	var anonymousObject = function (pairs) {
		var members = (Array.isArray(pairs) ? pairs : [])
			.map(function (p) {
				var name = p && csName(p.key);
				return name ? name + ' = ' + csString(p.value == null ? '' : p.value) : null;
			})
			.filter(function (m) { return m; });
		return members.length ? 'new { ' + members.join(', ') + ' }' : 'null';
	};

	var str = function (v) { return typeof v === 'string' ? v.trim() : ''; };

	var readConfig = function (raw) {
		if (!raw || typeof raw !== 'object') return null;
		var pairs = function (list) {
			return (Array.isArray(list) ? list : []).slice(0, MAX_PAIRS).filter(function (p) {
				return p && typeof p.key === 'string' && (typeof p.value === 'string' || typeof p.value === 'number');
			});
		};
		var kind = raw.kind || 'image';

		if (kind === 'embedded') {
			if (!isValidDataUrl(raw.dataUrl)) return null;
			var attributes = (Array.isArray(raw.htmlAttributes) ? raw.htmlAttributes : []).slice(0, MAX_PAIRS + 1);
			if (attributes.length > MAX_PAIRS) return null;
			for (var a = 0; a < attributes.length; a++) {
				var p = attributes[a];
				if (!p) return null;
				var value = typeof p.value === 'number' ? String(p.value) : p.value;
				if (!isAllowedAttribute(p.key, value)) return null;
				attributes[a] = { key: p.key, value: value };
			}
			return { kind: 'embedded', dataUrl: raw.dataUrl, parameters: [], htmlAttributes: attributes };
		}

		var config = { kind: kind, parameters: pairs(raw.parameters), htmlAttributes: pairs(raw.htmlAttributes) };

		if (kind === 'image') {
			config.baseUrl = str(raw.baseUrl);
			config.path = str(raw.path);
			return config.baseUrl && config.path ? config : null;
		}
		if (kind !== 'ags' && kind !== 'secureAgs') return null;

		config.serviceType = SERVICE_TYPES.indexOf(raw.serviceType) >= 0 ? raw.serviceType : SERVICE_TYPES[0];
		config.serverKey = str(raw.serverKey);
		config.serviceName = str(raw.serviceName);
		if (!config.serverKey || !config.serviceName) return null;
		if (kind === 'secureAgs') {
			config.usernameKey = str(raw.usernameKey);
			config.passwordKey = str(raw.passwordKey);
			if (!config.usernameKey || !config.passwordKey) return null;
		}
		return config;
	};

	// complete call (without @( await ... )): DLH.GetImage(...), DLH.GetAgsImage(...), SECURITY.GetAgsImage(...)
	var buildCall = function (config) {
		var parameters = anonymousObject(config.parameters);
		var htmlAttributes = anonymousObject(config.htmlAttributes);
		var args;

		if (config.kind === 'image') {
			args = [csString(config.baseUrl), csString(config.path)];
			if (parameters !== 'null' || htmlAttributes !== 'null') args.push(parameters);
			if (htmlAttributes !== 'null') args.push(htmlAttributes);
			return 'DLH.GetImage(' + args.join(', ') + ')';
		}

		args = ['AgsServiceType.' + config.serviceType, csString(config.serverKey), csString(config.serviceName), parameters];
		if (config.kind === 'secureAgs') {
			args.push('new AgsTokenObject { UsernameKey = ' + csString(config.usernameKey) + ', PasswordKey = ' + csString(config.passwordKey) + ' }');
		}
		if (htmlAttributes !== 'null') args.push(htmlAttributes);
		return (config.kind === 'secureAgs' ? 'SECURITY' : 'DLH') + '.GetAgsImage(' + args.join(', ') + ')';
	};

	// <img src="data:..." key="value" ... /> (config is validated by readConfig)
	var buildEmbedded = function (config) {
		var html = '<img src="' + config.dataUrl + '"';
		config.htmlAttributes.forEach(function (p) {
			html += ' ' + p.key + '="' + htmlEncode(p.value) + '"';
		});
		return html + ' />';
	};

	// complete source text for a config (embedded: markup, otherwise @(await call))
	var buildSource = function (config) {
		return config.kind === 'embedded' ? buildEmbedded(config) : '@(await ' + buildCall(config) + ')';
	};

	// ---- parse ----------------------------------------------------------------------------

	// <img ...> tag starting at i: { end, attributes: [{key, value}] } | null if not a plain, double quoted tag.
	// attribute values must be literal text: a single @ (Razor code) or a raw < rejects the tag.
	var scanImgTag = function (text, i, limit) {
		if (!/^<img\s/i.test(text.substr(i, 5))) return null;
		var attributes = [], j = i + 4;
		while (j < limit) {
			while (j < limit && /\s/.test(text.charAt(j))) j++;
			if (text.charAt(j) === '>') return { end: j + 1, attributes: attributes };
			if (text.charAt(j) === '/' && text.charAt(j + 1) === '>') return { end: j + 2, attributes: attributes };
			var name = /^[A-Za-z][A-Za-z0-9-]*/.exec(text.substring(j, Math.min(limit, j + 100)));
			if (!name) return null;
			j += name[0].length;
			while (j < limit && /\s/.test(text.charAt(j))) j++;
			if (text.charAt(j) !== '=') return null;
			j++;
			while (j < limit && /\s/.test(text.charAt(j))) j++;
			if (text.charAt(j) !== '"') return null;
			var close = text.indexOf('"', j + 1);
			if (close < 0 || close >= limit) return null;
			var raw = text.substring(j + 1, close);
			if (/[<\r\n]/.test(raw) || /@/.test(raw.replace(/@@/g, ''))) return null;
			attributes.push({ key: name[0], value: htmlDecode(raw.replace(/@@/g, '@')) });
			j = close + 1;
		}
		return null;
	};

	// first <img src="data:..."> start inside [from, to) or -1
	var findEmbeddedTag = function (source, from, to) {
		var regex = /<img\s[^>]*?\bsrc\s*=\s*"data:/gi;
		regex.lastIndex = from;
		var m = regex.exec(source);
		return m && m.index < to ? m.index : -1;
	};

	var parseEmbedded = function (tag) {
		if (!tag) return null;
		var dataUrl = null, htmlAttributes = [];
		for (var i = 0; i < tag.attributes.length; i++) {
			var p = tag.attributes[i];
			if (p.key.toLowerCase() === 'src') {
				if (dataUrl !== null) return null;
				dataUrl = p.value;
			} else if (isAllowedAttribute(p.key, p.value)) {
				htmlAttributes.push(p);
			} else {
				return null;
			}
		}
		if (!isValidDataUrl(dataUrl) || htmlAttributes.length > MAX_PAIRS) return null;
		return { kind: 'embedded', dataUrl: dataUrl, parameters: [], htmlAttributes: htmlAttributes };
	};

	// index after a C# string literal starting at i ("..." or @"..."), -1 if none
	var skipString = function (text, i) {
		if (text.charAt(i) === '@' && text.charAt(i + 1) === '"') {
			for (var j = i + 2; j < text.length; j++) {
				if (text.charAt(j) === '"') {
					if (text.charAt(j + 1) === '"') { j++; continue; }
					return j + 1;
				}
			}
			return -1;
		}
		if (text.charAt(i) !== '"') return -1;
		for (var k = i + 1; k < text.length; k++) {
			var c = text.charAt(k);
			if (c === '\\') { k++; continue; }
			if (c === '"') return k + 1;
			if (c === '\n') return -1;
		}
		return -1;
	};

	// splits at top level commas; returns null if the brackets are unbalanced
	var splitTopLevel = function (text) {
		var parts = [], depth = 0, start = 0;
		for (var i = 0; i < text.length; i++) {
			var c = text.charAt(i);
			if (c === '"' || (c === '@' && text.charAt(i + 1) === '"')) {
				var end = skipString(text, i);
				if (end < 0) return null;
				i = end - 1;
			} else if (c === '(' || c === '{' || c === '[') {
				depth++;
			} else if (c === ')' || c === '}' || c === ']') {
				depth--;
				if (depth < 0) return null;
			} else if (c === ',' && depth === 0) {
				parts.push(text.substring(start, i));
				start = i + 1;
			}
		}
		if (depth !== 0) return null;
		if (text.substring(start).trim() || parts.length) parts.push(text.substring(start));
		return parts;
	};

	var parseString = function (text) {
		text = text.trim();
		if (HELPER_VALUE.test(text)) return normalizeHelper(text);
		var end = skipString(text, 0);
		if (end !== text.length) return null;
		if (text.charAt(0) === '@') {
			return text.substring(2, text.length - 1).replace(/""/g, '"');
		}
		var body = text.substring(1, text.length - 1), result = '';
		for (var i = 0; i < body.length; i++) {
			var c = body.charAt(i);
			if (c !== '\\') { result += c; continue; }
			var n = body.charAt(++i);
			result += n === 'n' ? '\n' : n === 'r' ? '\r' : n === 't' ? '\t' : n === '0' ? '\0' : n;
		}
		return result;
	};

	// "new { a = "x", @class = "y" }" | "null" -> [{key, value}], null if not editable
	var parseObject = function (text, attributes) {
		text = text.trim();
		if (text === 'null' || text === '') return [];
		var m = /^new\s*\{([\s\S]*)\}$/.exec(text);
		if (!m) return null;
		var members = splitTopLevel(m[1]);
		if (!members) return null;

		var result = [];
		for (var i = 0; i < members.length; i++) {
			if (!members[i].trim()) continue;
			var mm = /^\s*@?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*([\s\S]*)$/.exec(members[i]);
			var value = mm ? parseString(mm[2]) : null;
			if (value === null) return null;
			result.push({ key: attributes ? mm[1].replace(/_/g, '-') : mm[1], value: value });
		}
		return result;
	};

	// first image inside the draggable (GetImage/GetAgsImage call or embedded <img src="data:...">):
	//   { method, start, end, [argsStart, argsEnd, wrapped] | [tag] } or { message }
	//   start/end: span to replace (the whole @(await ...) expression if present, or the <img> tag)
	var findImageCall = function (source, args) {
		var element = pdfDraggableSource.findElement(source, args);
		if (!element) {
			return { message: 'Could not find the matching draggable in the source (it may have been edited since the last run).' };
		}

		var regex = new RegExp(CALL.source, 'g');
		regex.lastIndex = element.start;
		var m = regex.exec(source);
		if (m && m.index >= element.end) m = null;

		var tagStart = findEmbeddedTag(source, element.start, element.end);
		if (tagStart >= 0 && (!m || tagStart < m.index)) {
			var tag = scanImgTag(source, tagStart, element.end);
			return tag
				? { method: 'embedded', start: tagStart, end: tag.end, tag: tag }
				: { message: 'The embedded image tag uses Razor code or unsupported markup and can only be edited in the source.' };
		}

		if (!m) {
			return { message: 'The draggable does not contain a GetImage(...) / GetAgsImage(...) call or an embedded image.' };
		}

		var argsStart = m.index + m[0].length, depth = 1;
		for (var i = argsStart; i < element.end; i++) {
			var c = source.charAt(i);
			if (c === '"' || (c === '@' && source.charAt(i + 1) === '"')) {
				var end = skipString(source, i);
				if (end < 0) break;
				i = end - 1;
			} else if (c === '(') {
				depth++;
			} else if (c === ')' && --depth === 0) {
				// enclosing @(await ...) -> replace the whole expression (needed to switch to/from embedded markup)
				var before = /@\(\s*await\s+$/.exec(source.substring(element.start, m.index));
				var after = /^\s*\)/.exec(source.substring(i + 1, element.end));
				var wrapped = !!(before && after);
				return {
					method: m[1], argsStart: argsStart, argsEnd: i, wrapped: wrapped,
					start: wrapped ? element.start + before.index : m.index,
					end: wrapped ? i + 1 + after[0].length : i + 1
				};
			}
		}
		return { message: 'Could not parse the ' + m[1] + '(...) call.' };
	};

	// "new AgsTokenObject { UsernameKey = "..", PasswordKey = ".." }" -> { usernameKey, passwordKey } | null
	var parseToken = function (text) {
		var m = /^\s*new\s+AgsTokenObject\s*(?:\(\s*\))?\s*\{([\s\S]*)\}\s*$/.exec(text);
		if (!m) return null;
		var members = splitTopLevel(m[1]);
		if (!members) return null;
		var token = { usernameKey: '', passwordKey: '' };
		for (var i = 0; i < members.length; i++) {
			if (!members[i].trim()) continue;
			var mm = /^\s*(UsernameKey|PasswordKey)\s*=\s*([\s\S]*)$/.exec(members[i]);
			var value = mm ? parseString(mm[2]) : null;
			if (value === null) return null;
			token[mm[1] === 'UsernameKey' ? 'usernameKey' : 'passwordKey'] = value;
		}
		return token;
	};

	var parseCall = function (method, text) {
		var parts = splitTopLevel(text);
		if (!parts || parts.some(function (p) { return /^\s*\w+\s*:/.test(p); })) return null;

		if (method === 'GetImage') {
			if (parts.length < 2 || parts.length > 4) return null;
			var baseUrl = parseString(parts[0]);
			var path = parseString(parts[1]);
			var parameters = parseObject(parts[2] || '', false);
			var htmlAttributes = parseObject(parts[3] || '', true);
			if (baseUrl === null || path === null || parameters === null || htmlAttributes === null) return null;
			return { kind: 'image', baseUrl: baseUrl, path: path, parameters: parameters, htmlAttributes: htmlAttributes };
		}

		// GetAgsImage(serviceType, serverKey, serviceName, parameters, [token,] [htmlAttributes])
		if (parts.length < 4 || parts.length > 6) return null;
		var type = /^\s*(?:E\.DataLinq\.Web\.Razor\.)?AgsServiceType\.(\w+)\s*$/.exec(parts[0]);
		if (!type || SERVICE_TYPES.indexOf(type[1]) < 0) return null;
		var config = {
			kind: 'ags',
			serviceType: type[1],
			serverKey: parseString(parts[1]),
			serviceName: parseString(parts[2]),
			parameters: parseObject(parts[3], false)
		};
		var rest = parts.slice(4);
		if (rest.length && /^\s*new\s+AgsTokenObject\b/.test(rest[0])) {
			var token = parseToken(rest.shift());
			if (!token) return null;
			config.kind = 'secureAgs';
			config.usernameKey = token.usernameKey;
			config.passwordKey = token.passwordKey;
		} else if (rest.length > 1) {
			return null;   // token given as expression (e.g. null or a variable)
		}
		config.htmlAttributes = parseObject(rest[0] || '', true);
		if (config.serverKey === null || config.serviceName === null || config.parameters === null || config.htmlAttributes === null) return null;
		return config;
	};

	// ---- registration ---------------------------------------------------------------------

	pdfDraggableSource.registerElementTemplate('image', {
		content: function (text) {
			var config;
			try {
				config = readConfig(JSON.parse(text));
			} catch (e) {
				config = null;
			}
			if (!config) {
				return '@* invalid image settings *@';
			}
			return buildSource(config);
		},
		// not text editable
		textPattern: /(?!)/
	});

	pdfDraggableSource.registerAction('image.get', function (source, args) {
		var call = findImageCall(source, args);
		if (call.message) return call;

		var data = call.method === 'embedded'
			? parseEmbedded(call.tag)
			: parseCall(call.method, source.substring(call.argsStart, call.argsEnd));
		return data
			? { data: data }
			: { message: 'The image uses Razor code (variables, expressions, named arguments) and can only be edited in the source.' };
	});

	pdfDraggableSource.registerAction('image.set', function (source, args) {
		var config = readConfig(args && args.settings);
		if (!config) {
			return { message: 'Invalid image settings (required values are missing).' };
		}

		var call = findImageCall(source, args);
		if (call.message) return call;

		// replace the whole call/tag: the image kind (helper / method / embedded) may have changed
		if (call.method === 'embedded' || call.wrapped) {
			return { start: call.start, end: call.end, text: buildSource(config) };
		}
		if (config.kind === 'embedded') {
			return { message: 'The image call is not written as @(await ...) and can only be changed to an embedded image in the source.' };
		}
		return { start: call.start, end: call.end, text: buildCall(config) };
	});
})();
