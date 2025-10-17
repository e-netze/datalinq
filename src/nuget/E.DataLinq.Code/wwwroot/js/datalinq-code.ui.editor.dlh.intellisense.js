//function getDlhCompletionProvider(monaco) {
//    return {
//        provideCompletionItems: function (model, position) {
//            console.log('providerCompletionItems', model, position);
//            return [];
//        }
//    };
//}

function registerRazorSnippets(monaco, language) {
    monaco.languages.registerCompletionItemProvider(language, {
        triggerCharacters: ['@'],
        provideCompletionItems: function (model, position) {
            const textUntilPosition = model.getValueInRange({
                startLineNumber: position.lineNumber,
                startColumn: 1,
                endLineNumber: position.lineNumber,
                endColumn: position.column
            });

            if (!/@[^@]*$/.test(textUntilPosition.trim())) {
                return { suggestions: [] };
            }

            return {
                suggestions: [
                    {
                        label: 'foreach',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            'foreach (var ${1:item} in ${2:collection})',
                            '{',
                            '    ${3:// your code here}',
                            '}'
                        ].join('\n'),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'A C# foreach loop.\n\nIterates over each item in a collection.'
                    },
                    {
                        label: 'if',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            'if (${1:condition})',
                            '{',
                            '    ${2:// your code here}',
                            '}'
                        ].join('\n'),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'A C# if statement.\n\nExecutes code only if the condition is true.'
                    },
                    {
                        label: 'ifelse',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            'if (${1:condition})',
                            '{',
                            '    ${2:// your code here}',
                            '}',
                            'else',
                            '{',
                            '    ${3:// your code here}',
                            '}'
                        ].join('\n'),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'A C# if-else statement.\n\nExecutes one block of code if the condition is true, another if false.'
                    },
                    {
                        label: 'ifelseifelse',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            'if (${1:condition1})',
                            '{',
                            '    ${2:// your code here}',
                            '}',
                            'else if (${3:condition2})',
                            '{',
                            '    ${4:// your code here}',
                            '}',
                            'else',
                            '{',
                            '    ${5:// your code here}',
                            '}'
                        ].join('\n'),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'A C# if-else if-else statement.\n\nMultiple conditional branches.'
                    },
                    {
                        label: 'for',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            'for (int ${1:i} = 0; ${1:i} < ${2:count}; ${1:i}++)',
                            '{',
                            '    ${3:// your code here}',
                            '}'
                        ].join('\n'),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'A C# for loop.\n\nLoops a fixed number of times.'
                    },
                    {
                        label: 'switch',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            'switch (${1:expression})',
                            '{',
                            '    case ${2:value}:',
                            '        ${3:// your code here}',
                            '        break;',
                            '',
                            '    default:',
                            '        ${4:// your code here}',
                            '        break;',
                            '}'
                        ].join('\n'),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'A C# switch statement.\n\nSelects one of many blocks of code to be executed.'
                    }
                ]
            };
        }
    });
}

function registerDLHCompletions(monaco, language, completions) {
    monaco.languages.registerCompletionItemProvider(language, {
        triggerCharacters: ['.'],
        provideCompletionItems: function (model, position) {
            const textUntilPosition = model.getValueInRange({
                startLineNumber: position.lineNumber,
                startColumn: 1,
                endLineNumber: position.lineNumber,
                endColumn: position.column
            });

            if (!textUntilPosition.trim().endsWith('@DLH.')) {
                return { suggestions: [] };
            }

            const word = model.getWordUntilPosition(position);
            const range = {
                startLineNumber: position.lineNumber,
                endLineNumber: position.lineNumber,
                startColumn: word.startColumn,
                endColumn: word.endColumn
            };

            const fixedCompletions = completions.map(item => ({
                ...item,
                range
            }));

            return {
                suggestions: fixedCompletions
            };
        }
    });
}