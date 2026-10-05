/**
 * DataLinq Code - intellisense: completions
 *
 * registerDLHCompletions, registerPDFCompletions, registerSecurityCompletions, registerRecordsCompletions
 * Depends on: monaco. Used by editor/editor-frame.js
 */
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

function registerRecordsCompletions(monaco, language, completions) {
    // matches @Model.Records. / @(Model.Records. / var x = Model.Records.
    const recordsExpression = /Model\s*\.\s*Records\s*\.$/;

    monaco.languages.registerCompletionItemProvider(language, {
        triggerCharacters: ['.'],
        provideCompletionItems: function (model, position) {
            const textUntilPosition = model.getValueInRange({
                startLineNumber: position.lineNumber,
                startColumn: 1,
                endLineNumber: position.lineNumber,
                endColumn: position.column
            });

            if (!recordsExpression.test(textUntilPosition.trim())) {
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
