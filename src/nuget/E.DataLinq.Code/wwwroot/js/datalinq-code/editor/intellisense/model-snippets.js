/**
 * DataLinq Code - intellisense: model snippets
 *
 * registerModelRazorSnippets(monaco, language)
 * Depends on: monaco. Used by editor/editor-frame.js
 */
function registerModelRazorSnippets(monaco, language) {
    monaco.languages.registerCompletionItemProvider(language, {
        triggerCharacters: ['.'],
        provideCompletionItems: function (model, position) {
            const textUntilPosition = model.getValueInRange({
                startLineNumber: position.lineNumber,
                startColumn: 1,
                endLineNumber: position.lineNumber,
                endColumn: position.column
            });

            if (!/@Model\.$/.test(textUntilPosition)) {
                return { suggestions: [] };
            }

            return {
                suggestions: [
                    {
                        label: 'QueryString',
                        kind: monaco.languages.CompletionItemKind.Module,
                        insertText: 'QueryString["${1:key}"]',
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'QueryString — helper methods to retrieve query string parameters from url by key'
                    },
                    {
                        label: 'FilterString',
                        kind: monaco.languages.CompletionItemKind.Module,
                        insertText: 'FilterString',
                        documentation: 'FilterString — helper methods to retrieve entire parameter string from url'
                    },
                    {
                        label: 'Records',
                        kind: monaco.languages.CompletionItemKind.Module,
                        insertText: 'Records',
                        documentation: 'Records — helper methods to retrieve records from the model'
                    },
                    {
                        label: 'RecordColumns',
                        kind: monaco.languages.CompletionItemKind.Module,
                        insertText: 'RecordColumns()',
                        documentation: 'RecordColumns — helper methods to retrieve record columns from the model'
                    },
                    {
                        label: 'ElapsedMilliseconds',
                        kind: monaco.languages.CompletionItemKind.Module,
                        insertText: 'ElapsedMilliseconds',
                        documentation: 'ElapsedMilliseconds — helper methods to retrieve elapsed milliseconds from the model'
                    },
                    {
                        label: 'CountRecords',
                        kind: monaco.languages.CompletionItemKind.Module,
                        insertText: 'CountRecords',
                        documentation: 'CountRecords — helper methods to retrieve the count of records from the model'
                    },
                    {
                        label: 'Success',
                        kind: monaco.languages.CompletionItemKind.Module,
                        insertText: 'Success',
                        documentation: 'Success — helper methods to retrieve the success status from the model'
                    },
                ]
            };
        }
    });
}
