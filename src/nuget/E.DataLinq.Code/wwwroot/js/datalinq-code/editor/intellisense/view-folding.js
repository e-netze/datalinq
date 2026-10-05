/**
 * DataLinq Code - intellisense: view folding
 *
 * registerViewFolding(monaco, language): PDF page and #region folding
 * Depends on: monaco. Used by editor/editor-frame.js
 */
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

