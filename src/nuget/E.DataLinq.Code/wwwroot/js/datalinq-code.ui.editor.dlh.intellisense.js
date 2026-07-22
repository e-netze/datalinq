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
                        label: 'DLH',
                        kind: monaco.languages.CompletionItemKind.Module,
                        insertText: 'DLH',
                        documentation: 'DataLinqHelper (DLH) — helper methods for building DataLinq views.'
                    },
                    {
                        label: 'PDF',
                        kind: monaco.languages.CompletionItemKind.Module,
                        insertText: 'PDF',
                        documentation: 'DataLinqPdfHelper (PDF) — helper methods for building PDF reports.'
                    },
                    {
                        label: 'SECURITY',
                        kind: monaco.languages.CompletionItemKind.Module,
                        insertText: 'SECURITY',
                        documentation: 'DataLinqSecurityHelper (SECURITY) — security related helper methods.'
                    },
                    {
                        label: 'region',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            '* #region ${1:name} *@',
                            '${2:}',
                            '@* #endregion *@'
                        ].join('\n'),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'A foldable region.\n\nWrapped in Razor comments (@* *@) so the markers are removed at compile time and never appear in the rendered page.'
                    },
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

function registerPDFCompletions(monaco, language, completions) {
    monaco.languages.registerCompletionItemProvider(language, {
        triggerCharacters: ['.'],
        provideCompletionItems: function (model, position) {
            const textUntilPosition = model.getValueInRange({
                startLineNumber: position.lineNumber,
                startColumn: 1,
                endLineNumber: position.lineNumber,
                endColumn: position.column
            });

            if (!textUntilPosition.trim().endsWith('@PDF.')) {
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

function registerSecurityCompletions(monaco, language, completions) {
    monaco.languages.registerCompletionItemProvider(language, {
        triggerCharacters: ['.'],
        provideCompletionItems: function (model, position) {
            const textUntilPosition = model.getValueInRange({
                startLineNumber: position.lineNumber,
                startColumn: 1,
                endLineNumber: position.lineNumber,
                endColumn: position.column
            });

            if (!textUntilPosition.trim().endsWith('@SECURITY.')) {
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

function registerViewFolding(monaco, language) {
    const pdfPageStart = /@PDF\s*\.\s*NewPage\s*\(/i;
    const pdfPageEnd = /@PDF\s*\.\s*EndPage\s*\(/i;
    const regionStart = /@\*\s*#region\b/i;
    const regionEnd = /@\*\s*#endregion\b/i;

    monaco.languages.registerFoldingRangeProvider(language, {
        provideFoldingRanges: function (model) {
            const ranges = [];
            const pdfStack = [];
            const regionStack = [];
            const lineCount = model.getLineCount();

            for (let lineNumber = 1; lineNumber <= lineCount; lineNumber++) {
                const line = model.getLineContent(lineNumber);

                if (pdfPageStart.test(line)) {
                    pdfStack.push(lineNumber);
                } else if (pdfPageEnd.test(line)) {
                    const start = pdfStack.pop();
                    if (start !== undefined && lineNumber > start) {
                        ranges.push({
                            start: start,
                            end: lineNumber,
                            kind: monaco.languages.FoldingRangeKind.Region
                        });
                    }
                }

                if (regionStart.test(line)) {
                    regionStack.push(lineNumber);
                } else if (regionEnd.test(line)) {
                    const start = regionStack.pop();
                    if (start !== undefined && lineNumber > start) {
                        ranges.push({
                            start: start,
                            end: lineNumber,
                            kind: monaco.languages.FoldingRangeKind.Region
                        });
                    }
                }
            }

            return ranges;
        }
    });
}
