/**
 * DataLinq Code - MCP agent token dialog
 *
 * dataLinqCode.api.createMcpAgentToken(): requests a bearer token for the DataLinq MCP server
 * of the currently connected instance (Test/Development only).
 * dataLinqCode.ui.mcpToken(): modal to generate and copy the token and a client config.
 * Depends on: core, api/api-client.js, ui/modal.js
 */
(function ($) {
        this.createMcpAgentToken = function (lifetimeHours, name, callback) {
            this.post('createMcpAgentToken', { lifetimeHours: lifetimeHours, name: name }, callback);
        };

        this.listMcpAgentTokens = function (callback) {
            this.get('listMcpAgentTokens', callback);
        };

        this.revokeMcpAgentToken = function (id, callback) {
            this.post('revokeMcpAgentToken', { id: id }, callback);
        };
}).call(dataLinqCode.api, jQuery);

(function ($) {
        this.mcpToken = function () {
            $('body').dataLinq_code_modal({
                title: 'MCP Agent Token',
                height: '80%',
                width: '680px',
                id: 'datalinq-code-mcp-token',
                onload: function ($content) {
                    $content.addClass('datalinq-code-mcp-token-content');

                    $("<div>")
                        .addClass('mcp-desc')
                        .text('Generate a personal bearer token for AI agents (VS Code, Claude, Copilot CLI, ...) to use the DataLinq MCP server of this instance with your permissions. The token is shown only once.')
                        .appendTo($content);

                    var $row = $("<div>").addClass('mcp-toolbar').appendTo($content);
                    var $name = $("<input>")
                        .attr({ type: 'text', placeholder: 'Name (e.g. VS Code laptop)', maxlength: 80 })
                        .addClass('mcp-input')
                        .appendTo($row);
                    $("<label>").addClass('mcp-label').text('Valid for').appendTo($row);
                    var $lifetime = $("<select>").addClass('mcp-select').appendTo($row);
                    $.each([[8, '8 hours'], [24, '24 hours'], [72, '3 days'], [168, '7 days']], function (i, o) {
                        $("<option>").attr('value', o[0]).text(o[1]).appendTo($lifetime);
                    });
                    $lifetime.val('24');

                    var $generate = $("<button>")
                        .attr('type', 'button')
                        .addClass('datalinq-code-button mcp-generate')
                        .text('Generate token')
                        .appendTo($row);

                    var $result = $("<div>").addClass('mcp-result').appendTo($content);

                    var $listSection = $("<div>").addClass('mcp-list-section').appendTo($content);
                    $("<div>").addClass('mcp-label').text('Active tokens').appendTo($listSection);
                    var $list = $("<div>").addClass('mcp-list').appendTo($listSection);

                    var formatDate = function (d) { return d ? new Date(d).toLocaleString() : '-'; };

                    var reloadList = function () {
                        dataLinqCode.api.listMcpAgentTokens(function (response) {
                            $list.empty();
                            var tokens = (response && response.tokens) || [];

                            if (tokens.length === 0) {
                                $("<div>").addClass('mcp-empty').text('No active tokens.').appendTo($list);
                                return;
                            }

                            $.each(tokens, function (i, t) {
                                var $item = $("<div>").addClass('mcp-list-item').appendTo($list);
                                var $info = $("<div>").addClass('mcp-list-info').appendTo($item);
                                $("<div>").addClass('mcp-list-name').text(t.Name || t.name).appendTo($info);
                                $("<div>").addClass('mcp-list-meta').text(
                                    'Created ' + formatDate(t.Created || t.created) +
                                    '  ·  Expires ' + formatDate(t.Expires || t.expires) +
                                    '  ·  Last used ' + formatDate(t.LastUsed || t.lastUsed)).appendTo($info);

                                $("<button>")
                                    .attr('type', 'button')
                                    .addClass('datalinq-code-button mcp-revoke')
                                    .text('Revoke')
                                    .appendTo($item)
                                    .click(function () {
                                        if (!confirm('Revoke token "' + (t.Name || t.name) + '"? Agents using it lose access immediately.')) {
                                            return;
                                        }
                                        dataLinqCode.api.revokeMcpAgentToken(t.Id || t.id, function () {
                                            reloadList();
                                        });
                                    });
                            });
                        });
                    };
                    reloadList();

                    var field = function (title, value, rows) {
                        var $field = $("<div>").addClass('mcp-field').appendTo($result);
                        var $head = $("<div>").addClass('mcp-field-head').appendTo($field);
                        $("<span>").addClass('mcp-label').text(title).appendTo($head);
                        var $text = $("<textarea>")
                            .addClass('mcp-code')
                            .attr({ rows: rows, readonly: true, spellcheck: false })
                            .val(value);
                        $("<button>")
                            .attr('type', 'button')
                            .addClass('datalinq-code-button mcp-copy')
                            .text('Copy')
                            .appendTo($head)
                            .click(function () {
                                var $btn = $(this);
                                navigator.clipboard.writeText($text.val()).then(function () {
                                    $btn.text('Copied').addClass('copied');
                                    setTimeout(function () { $btn.text('Copy').removeClass('copied'); }, 1500);
                                });
                            });
                        $text.appendTo($field);
                        $text.css('height', 'auto').css('height', ($text[0].scrollHeight + 2) + 'px');
                    };

                    $generate.click(function () {
                        $generate.prop('disabled', true).text('Generating...');
                        $result.empty();

                        dataLinqCode.api.createMcpAgentToken(parseInt($lifetime.val(), 10), $name.val(), function (response) {
                            $generate.prop('disabled', false).text('Generate token');
                            reloadList();

                            if (!response || !response.token) {
                                $("<div>")
                                    .addClass('mcp-message mcp-error')
                                    .text((response && (response.error_message || response.errorMessage || response.ErrorMessage)) || 'Token could not be created.')
                                    .appendTo($result);
                                return;
                            }

                            var config = {
                                servers: {
                                    datalinq: {
                                        type: 'http',
                                        url: response.mcpUrl,
                                        headers: { Authorization: 'Bearer ' + response.token }
                                    }
                                }
                            };

                            var $meta = $("<div>").addClass('mcp-meta').appendTo($result);
                            $("<span>").addClass('mcp-label').text('MCP url').appendTo($meta);
                            $("<span>").addClass('mcp-value').text(response.mcpUrl).appendTo($meta);
                            $("<span>").addClass('mcp-label').text('Expires').appendTo($meta);
                            $("<span>").addClass('mcp-value').text(new Date(response.expires).toLocaleString()).appendTo($meta);

                            field('Token', response.token, 3);
                            field('Client config (e.g. .vscode/mcp.json)', JSON.stringify(config, null, 2), 10);

                            $("<div>")
                                .addClass('mcp-message mcp-warning')
                                .text('Treat this token like a password. Do not commit it to source control; prefer an input variable or environment variable in your client config.')
                                .appendTo($result);
                        });
                    });
                }
            });
        };
}).call(dataLinqCode.ui, jQuery);
