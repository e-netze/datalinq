/*
 * PDF report editing tool: data
 *
 * "Data" section of the editing toolbar:
 *   - Filter Model.Records: LINQ pipeline (where/sort/skip/take/distinct/select/custom) + optional "at least one row" check
 *   - Add query data: records of another query (DLH.GetRecordsAsync) stored in a variable + the same pipeline
 *   - List of the current data blocks (Model.Records is always listed) with edit/remove
 * The source is changed by DataLinq Code (editor/pdf-editing/tools/data.js,
 * actions 'data.list' / 'data.preview' / 'data.set' / 'data.remove').
 */
(function () {
    const OPERATORS = [
        { value: 'eq', label: '=' },
        { value: 'ne', label: '\u2260' },
        { value: 'gt', label: '>' },
        { value: 'ge', label: '\u2265' },
        { value: 'lt', label: '<' },
        { value: 'le', label: '\u2264' },
        { value: 'contains', label: 'contains' },
        { value: 'notContains', label: 'not contains' },
        { value: 'startsWith', label: 'starts with' },
        { value: 'endsWith', label: 'ends with' },
        { value: 'in', label: 'in (a, b, ...)' },
        { value: 'notIn', label: 'not in (a, b, ...)' },
        { value: 'isNull', label: 'is empty' },
        { value: 'isNotNull', label: 'is not empty' }
    ];
    const VALUE_TYPES = [
        { value: 'string', label: 'Text' },
        { value: 'number', label: 'Number' },
        { value: 'date', label: 'Date' },
        { value: 'bool', label: 'Bool' },
        { value: 'expr', label: 'C# expr.' }
    ];
    const OPS = [
        { value: 'where', label: 'Where (filter)' },
        { value: 'orderBy', label: 'Sort' },
        { value: 'take', label: 'Take (limit)' },
        { value: 'skip', label: 'Skip' },
        { value: 'takeLast', label: 'Take last' },
        { value: 'skipLast', label: 'Skip last' },
        { value: 'distinct', label: 'Distinct by columns' },
        { value: 'select', label: 'Select columns' },
        { value: 'custom', label: 'Custom LINQ' }
    ];
    const IDENTIFIER = /^[A-Za-z_][A-Za-z0-9_]{0,63}$/;
    const QUERY_ID = /^[\w\-.]+@[\w\-.]+$/;

    const connected = () => !!(window.dataLinqEditBridge && dataLinqEditBridge.isConnected());
    const clone = o => JSON.parse(JSON.stringify(o));
    const noEnter = input => { input.pdfEditOnEnter = () => { }; return input; };

    const el = function (tag, className, text) {
        const e = document.createElement(tag);
        if (className) e.className = className;
        if (text !== undefined) e.textContent = text;
        return e;
    };

    const textInput = function (value, placeholder, onInput) {
        const input = noEnter(el('input'));
        input.type = 'text';
        input.value = value ?? '';
        input.placeholder = placeholder || '';
        input.spellcheck = false;
        input.autocomplete = 'off';
        input.addEventListener('input', () => onInput(input.value));
        return input;
    };

    const select = function (options, value, onChange) {
        const s = el('select');
        options.forEach(o => {
            const option = el('option', null, o.label);
            option.value = o.value;
            option.selected = o.value === value;
            s.appendChild(option);
        });
        s.addEventListener('change', () => onChange(s.value));
        return s;
    };

    const smallButton = function (text, title, onClick, className) {
        const b = el('button', 'pdf-data-btn' + (className ? ' ' + className : ''), text);
        b.type = 'button';
        b.title = title;
        b.setAttribute('aria-label', title);
        b.addEventListener('click', onClick);
        return b;
    };

    const newCondition = () => ({ column: '', operator: 'eq', value: '', valueType: 'string' });
    const newGroup = () => ({ logic: 'and', not: false, items: [newCondition()] });
    const newOp = function (type) {
        switch (type) {
            case 'where': return { op: 'where', group: newGroup() };
            case 'orderBy': return { op: 'orderBy', column: '', descending: false };
            case 'take': return { op: 'take', count: 10 };
            case 'skip':
            case 'takeLast':
            case 'skipLast': return { op: type, count: 1 };
            case 'distinct':
            case 'select': return { op: type, columns: [] };
            default: return { op: 'custom', expression: '' };
        }
    };

    // ---------- pipeline editor (custom dialog field) ----------

    const createPipelineEditor = function (initial) {
        const state = { ops: clone(Array.isArray(initial) ? initial : []) };
        const root = el('div', 'pdf-data-pipeline');
        const changed = () => root.dispatchEvent(new Event('change', { bubbles: true }));

        const renderCondition = function (condition, parent, index, container) {
            const row = el('div', 'pdf-data-condition');
            const column = textInput(condition.column, 'Column', v => { condition.column = v.trim(); changed(); });
            column.className = 'pdf-data-column';
            const value = textInput(condition.value, 'Value', v => { condition.value = v; changed(); });
            value.className = 'pdf-data-value';
            const type = select(VALUE_TYPES, condition.valueType, v => {
                condition.valueType = v;
                value.placeholder = v === 'expr' ? 'e.g. DLH.QueryParameter("id")' : v === 'date' ? 'yyyy-MM-dd' : 'Value';
                changed();
            });
            const toggleValue = () => {
                const hidden = condition.operator === 'isNull' || condition.operator === 'isNotNull';
                value.hidden = type.hidden = hidden;
            };
            const operator = select(OPERATORS, condition.operator, v => { condition.operator = v; toggleValue(); changed(); });
            toggleValue();
            row.append(column, operator, type, value,
                smallButton('\u00d7', 'Remove condition', () => { parent.items.splice(index, 1); renderAll(); changed(); }, 'remove'));
            container.appendChild(row);
        };

        const renderGroup = function (group, parent, index, container, depth) {
            const box = el('div', 'pdf-data-group');
            const header = el('div', 'pdf-data-group-header');
            const logic = select([{ value: 'and', label: 'All (AND)' }, { value: 'or', label: 'Any (OR)' }], group.logic, v => { group.logic = v; changed(); });
            const notLabel = el('label', 'pdf-data-not');
            const not = el('input');
            not.type = 'checkbox';
            not.checked = group.not === true;
            not.addEventListener('change', () => { group.not = not.checked; changed(); });
            notLabel.append(not, document.createTextNode(' NOT'));
            header.append(logic, notLabel,
                smallButton('+ Condition', 'Add condition', () => { group.items.push(newCondition()); renderAll(); changed(); }, 'add'));
            if (depth < 5) {
                header.appendChild(smallButton('+ Group', 'Add nested group', () => { group.items.push(newGroup()); renderAll(); changed(); }, 'add'));
            }
            if (parent) {
                header.appendChild(smallButton('\u00d7', 'Remove group', () => { parent.items.splice(index, 1); renderAll(); changed(); }, 'remove'));
            }
            box.appendChild(header);

            const items = el('div', 'pdf-data-group-items');
            group.items.forEach((item, i) => {
                if (Array.isArray(item.items)) renderGroup(item, group, i, items, depth + 1);
                else renderCondition(item, group, i, items);
            });
            if (group.items.length === 0) items.appendChild(el('div', 'pdf-data-empty', 'No conditions'));
            box.appendChild(items);
            container.appendChild(box);
        };

        const columnsInput = function (op) {
            const input = textInput((op.columns || []).join(', '), 'Column1, Column2, ...', v => {
                op.columns = v.split(',').map(c => c.trim()).filter(c => c !== '');
                changed();
            });
            input.className = 'pdf-data-wide';
            return input;
        };

        const renderOp = function (op, index, container) {
            const box = el('div', 'pdf-data-op');
            const header = el('div', 'pdf-data-op-header');
            const label = (OPS.find(o => o.value === op.op) || { label: op.op }).label;
            const isThenBy = op.op === 'orderBy' && index > 0 && state.ops[index - 1].op === 'orderBy';
            header.appendChild(el('span', 'pdf-data-op-title', (index + 1) + '. ' + (isThenBy ? 'Then sort' : label)));
            const move = (delta) => {
                const target = index + delta;
                if (target < 0 || target >= state.ops.length) return;
                const [item] = state.ops.splice(index, 1);
                state.ops.splice(target, 0, item);
                renderAll();
                changed();
            };
            const up = smallButton('\u2191', 'Move up', () => move(-1));
            up.disabled = index === 0;
            const down = smallButton('\u2193', 'Move down', () => move(1));
            down.disabled = index === state.ops.length - 1;
            header.append(up, down, smallButton('\u00d7', 'Remove step', () => { state.ops.splice(index, 1); renderAll(); changed(); }, 'remove'));
            box.appendChild(header);

            const body = el('div', 'pdf-data-op-body');
            switch (op.op) {
                case 'where':
                    renderGroup(op.group, null, 0, body, 0);
                    break;
                case 'orderBy': {
                    const column = textInput(op.column, 'Column', v => { op.column = v.trim(); changed(); });
                    const direction = select([{ value: 'asc', label: 'Ascending' }, { value: 'desc', label: 'Descending' }],
                        op.descending ? 'desc' : 'asc', v => { op.descending = v === 'desc'; changed(); });
                    body.append(column, direction);
                    break;
                }
                case 'take':
                case 'skip':
                case 'takeLast':
                case 'skipLast': {
                    const count = noEnter(el('input'));
                    count.type = 'number';
                    count.min = '0';
                    count.step = '1';
                    count.value = op.count;
                    count.addEventListener('input', () => { op.count = Math.max(0, Math.round(parseFloat(count.value)) || 0); changed(); });
                    body.append(count, el('span', 'pdf-data-hint', 'rows'));
                    break;
                }
                case 'distinct':
                case 'select':
                    body.appendChild(columnsInput(op));
                    break;
                case 'custom': {
                    const expression = textInput(op.expression, 'e.g. Where(r => r["STATUS"]?.ToString() == "A")', v => { op.expression = v; changed(); });
                    expression.className = 'pdf-data-wide pdf-data-code';
                    body.append(el('span', 'pdf-data-hint', '.'), expression);
                    break;
                }
            }
            box.appendChild(body);
            container.appendChild(box);
        };

        const list = el('div', 'pdf-data-ops');
        const footer = el('div', 'pdf-data-add-op');
        let addType = 'where';
        const addSelect = select(OPS, addType, v => { addType = v; });
        footer.append(addSelect, smallButton('+ Add step', 'Add step to the pipeline', () => {
            state.ops.push(newOp(addType));
            renderAll();
            changed();
        }, 'add primary'));

        const renderAll = function () {
            list.textContent = '';
            if (state.ops.length === 0) {
                list.appendChild(el('div', 'pdf-data-empty', 'No steps - all records are used.'));
            }
            state.ops.forEach((op, i) => renderOp(op, i, list));
        };

        root.append(list, footer);
        root.pdfDataState = state;
        renderAll();
        return root;
    };

    // ---------- dialogs ----------

    const pipelineField = function (block, capture) {
        return {
            name: 'ops',
            label: 'Steps (executed top to bottom)',
            type: 'custom',
            value: block.ops || [],
            create: value => createPipelineEditor(value),
            read: element => clone(element.pdfDataState.ops),
            validate: (value, values) => { capture(values); return null; }
        };
    };

    const checkFields = function (block) {
        return [
            { name: 'requireRows', label: 'Throw an error if there are no records', type: 'checkbox', value: block.requireRows === true },
            {
                name: 'errorMessage', label: 'Error message', type: 'text', value: block.errorMessage || '',
                placeholder: 'No records found.', showIf: v => v.requireRows
            }
        ];
    };

    const save = async function (block, originalName) {
        const result = await pdfEditActions.request('data.set', { block: block, originalName: originalName || '' });
        if (!result.success) {
            pdfEditActions.notify(result.message || 'The data block could not be saved.');
            return;
        }
        const page = pdfEditActions.currentPage();
        pdfEditActions.saveAndReload(page ? page.sourceIndex : 0);
    };

    const editModelRecords = async function (existing) {
        const block = existing || { type: 'model', ops: [], requireRows: false, errorMessage: '' };
        let latest = {};
        const toBlock = () => ({
            type: 'model',
            ops: latest.ops || [],
            requireRows: latest.requireRows === true,
            errorMessage: latest.requireRows ? (latest.errorMessage || '') : ''
        });

        const values = await pdfEditDialog.open({
            title: 'Filter Model.Records',
            submitLabel: 'Apply',
            steps: [
                {
                    title: 'Model.Records',
                    description: 'Records of the query this report view belongs to. The result is assigned to Model.Records again (IDictionary<string, object>[]).',
                    fields: checkFields(block).concat([pipelineField(block, v => { latest = Object.assign({}, v); })])
                }
            ]
        });
        if (!values) return;
        latest = values;
        await save(toBlock());
    };

    const editQueryData = async function (existing) {
        const block = existing || { type: 'query', name: '', queryId: '', filter: '', orderby: '', ops: [], requireRows: false, errorMessage: '' };
        const originalName = existing ? existing.name : '';
        let latest = {};
        const toBlock = () => ({
            type: 'query',
            name: (latest.name || '').trim(),
            queryId: (latest.queryId || '').trim(),
            filter: (latest.filter || '').trim(),
            orderby: (latest.orderby || '').trim(),
            ops: latest.ops || [],
            requireRows: latest.requireRows === true,
            errorMessage: latest.requireRows ? (latest.errorMessage || '') : ''
        });

        const values = await pdfEditDialog.open({
            title: existing ? 'Edit query data: ' + existing.name : 'Add query data',
            submitLabel: existing ? 'Apply' : 'Add',
            steps: [
                {
                    title: 'Source',
                    description: 'var <name> = await DLH.GetRecordsAsync(id, filter, orderby)',
                    fields: [
                        { name: 'name', label: 'Variable name', type: 'text', value: block.name, placeholder: 'e.g. orders', required: true, pattern: IDENTIFIER },
                        { name: 'queryId', label: 'Query id (endpoint@query)', type: 'text', value: block.queryId, placeholder: 'endpoint@query', required: true, pattern: QUERY_ID },
                        { name: 'filter', label: 'Filter (url parameters, optional)', type: 'text', value: block.filter, placeholder: 'NAME=Franz&STRASSE=Eberweg' },
                        { name: 'orderby', label: 'Order by (optional, -FIELD = descending)', type: 'text', value: block.orderby, placeholder: 'NAME,-DATE' }
                    ]
                },
                {
                    title: 'Filter records',
                    description: 'Further processing of the loaded records with LINQ. The result stays IDictionary<string, object>[].',
                    fields: checkFields(block).concat([pipelineField(block, v => { latest = Object.assign({}, latest, v); })])
                }
            ]
        });
        if (!values) return;
        latest = values;
        await save(toBlock(), originalName);
    };

    const removeBlock = async function (block) {
        const label = block.type === 'model' ? 'the Model.Records filter' : 'the data source "' + block.name + '"';
        const ok = await pdfEditDialog.confirm({
            title: 'Remove data',
            message: 'Remove ' + label + '? Code using ' + (block.type === 'model' ? 'Model.Records keeps working (unfiltered).' : 'the variable "' + block.name + '" will not compile anymore.'),
            confirmLabel: 'Remove'
        });
        if (!ok) return;
        const result = await pdfEditActions.request('data.remove', { type: block.type, name: block.name });
        if (!result.success) {
            pdfEditActions.notify(result.message || 'The data block could not be removed.');
            return;
        }
        const page = pdfEditActions.currentPage();
        pdfEditActions.saveAndReload(page ? page.sourceIndex : 0);
    };

    // ---------- toolbar section ----------

    const describe = function (block) {
        const steps = (block.ops || []).map(o => (OPS.find(x => x.value === o.op) || { label: o.op }).label.split(' ')[0]);
        const parts = [];
        if (steps.length) parts.push(steps.join(' \u2192 '));
        if (block.requireRows) parts.push('required');
        return parts.join(', ') || 'unfiltered';
    };

    const createItem = function (title, subtitle, onEdit, onRemove) {
        const item = el('li', 'pdf-data-item');
        const text = el('div', 'pdf-data-item-text');
        text.appendChild(el('span', 'pdf-data-item-title', title));
        if (subtitle) text.appendChild(el('small', null, subtitle));
        item.appendChild(text);
        const actions = el('div', 'pdf-data-item-actions');
        actions.appendChild(smallButton('\u270e', 'Edit', onEdit));
        if (onRemove) actions.appendChild(smallButton('\u00d7', 'Remove', onRemove, 'remove'));
        item.appendChild(actions);
        return item;
    };

    window.createDataControl = function () {
        const container = el('div', 'pdf-data-control');

        const button = function (label, title, svg, onClick) {
            const b = el('button', 'pdf-editing-guides pdf-data-action');
            b.type = 'button';
            b.title = title;
            b.innerHTML = '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">' +
                svg + '</svg>';
            b.appendChild(el('span', null, label));
            b.addEventListener('click', () => {
                if (!connected()) {
                    pdfEditActions.notify('DataLinq Code is not connected.');
                    return;
                }
                onClick();
            });
            return b;
        };

        let blocks = [];
        const list = el('ul', 'pdf-data-list');

        const render = function () {
            list.textContent = '';
            blocks.filter(b => b.type === 'query').forEach(b => {
                list.appendChild(createItem(b.name, null,
                    () => editQueryData(clone(b)),
                    () => removeBlock(b)));
            });
        };

        const refresh = async function () {
            if (!connected()) return false;
            const result = await pdfEditActions.request('data.list', {});
            if (result.success && result.data) {
                blocks = Array.isArray(result.data.blocks) ? result.data.blocks : [];
                render();
            }
            return true;
        };

        container.append(
            button('Filter records', 'Filter/sort/limit Model.Records of this query (LINQ)',
                '<path d="M3 5h18l-7 8v6l-4-2v-4z"/>',
                () => editModelRecords(clone(blocks.find(b => b.type === 'model') || null) || null)),
            button('Add query data', 'Load records of another query (DLH.GetRecordsAsync) into a variable',
                '<ellipse cx="12" cy="6" rx="7" ry="3"/><path d="M5 6v12c0 1.7 3.1 3 7 3s7-1.3 7-3V6"/><line x1="12" y1="11" x2="12" y2="17"/><line x1="9" y1="14" x2="15" y2="14"/>',
                () => editQueryData(null)),
            list);

        render();

        // the bridge connects asynchronously: retry until the list could be requested
        let attempts = 0;
        const tryRefresh = async () => {
            if (await refresh() || ++attempts > 40) return;
            setTimeout(tryRefresh, 250);
        };
        tryRefresh();

        container.pdfDataRefresh = refresh;
        return container;
    };
})();
