/**
 * DataLinq Code - intellisense: razor snippets
 *
 * registerRazorSnippets(monaco, language)
 * Depends on: monaco. Used by editor/editor-frame.js
 */
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
                        label: 'Model',
                        kind: monaco.languages.CompletionItemKind.Module,
                        insertText: 'Model',
                        documentation: 'DataLinq Model (Model) — model related helper methods.'
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
                    },
                    {
                        label: 'Report',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            'PDF.BeginReport(',
                            '    new PageNumberOptions',
                            '    {',
                            '        UsePageNumbers = ${1|true,false|},',
                            '        Position = ${2:2},',
                            '        Type = ${3:0},',
                            '        SkipPages = ${4:0}',
                            '    }, //pageNumberOptions',
                            '    new PageDateTimeOptions',
                            '    {',
                            '        UsePageDateTime = ${5|true,false|},',
                            '        Position = ${6:0},',
                            '        Format = "${7:HH:mm dd.MM.yyyy}",',
                            '        SkipPages = ${8:0}',
                            '    }, //pageDateTimeOptions',
                            '    PdfQuality.${9|Best,High,Medium,Low,Preview|}, //quality',
                            '    ${10|true,false|}, //download_button',
                            '    "${11:dataLinqPdfReport}" //fileName',
                            ')',
                            '',
                            '   @PDF.NewPage(',
                            '       new PageTemplateOptions',
                            '       {',
                            '           UsePageTemplate = ${12|true,false|},',
                            '           TemplateId = "${13:endpoint@query@view}"',
                            '       }, //pageTemplateOptions',
                            '       new DynamicTableOptions',
                            '       {',
                            '           Dynamic = ${14|true,false|},',
                            '           DynamicUseTemplate = ${15|true,false|},',
                            '           DefaultMarginTop = ${16:0.0},',
                            '           DefaultMarginBottom = ${17:0.0},',
                            '           RepeatHeader = ${18|true,false|}',
                            '       }, //dynamicTableOptions',
                            '       PaperSize.${19|A4,A1,A2,A3,A5,A6|}, //paperSize',
                            '       ${20|false,true|} //landscape',
                            '   )',
                            '',
                            '       ${21:@* your report content here *@}',
                            '',
                            '   @PDF.EndPage()',
                            '',
                            '@PDF.EndReport()'
                        ].join('\n'),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'A complete PDF report scaffold.\n\nGenerates BeginReport / NewPage / EndPage / EndReport with all option objects. Tab through the placeholders to fill in each value.'
                    },
                    {
                        label: 'Page',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            'PDF.NewPage(',
                            '    new PageTemplateOptions',
                            '    {',
                            '        UsePageTemplate = ${1|true,false|},',
                            '        TemplateId = "${2:endpoint@query@view}"',
                            '    }, //pageTemplateOptions',
                            '    new DynamicTableOptions',
                            '    {',
                            '        Dynamic = ${3|true,false|},',
                            '        DynamicUseTemplate = ${4|true,false|},',
                            '        DefaultMarginTop = ${5:0.0},',
                            '        DefaultMarginBottom = ${6:0.0},',
                            '        RepeatHeader = ${7|true,false|}',
                            '    }, //dynamicTableOptions',
                            '    PaperSize.${8|A4,A1,A2,A3,A5,A6|}, //paperSize',
                            '    ${9|false,true|} //landscape',
                            ')',
                            '',
                            '       ${10:@* your report content here *@}',
                            '',
                            '@PDF.EndPage()'
                        ].join('\n'),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'A new PDF page scaffold.\n\nGenerates NewPage / EndPage with all option objects. Tab through the placeholders to fill in each value.'
                    },
                    {
                        label: 'Draggable',
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            'PDF.NewDraggable(x: ${1:0}, y: ${2:0})',
                            '   ${3:@* your report content here *@}',
                            '@PDF.EndDraggable()'
                        ].join('\n'),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: 'A new PDF draggable scaffold.\n\nGenerates NewDraggable / EndDraggable with all option objects. Tab through the placeholders to fill in each value.'
                    }
                ]
            };
        }
    });
}
