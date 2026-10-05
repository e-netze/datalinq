/**
 * DataLinq Code - keyboard shortcuts
 *
 * dataLinqCode.bindDocumentEvents(doc) binds the IDE shortcuts to a document
 * (main window and editor iframes): Ctrl+S save, Ctrl+Shift+S save all,
 * F5 run, Ctrl+F5 run in new tab, Esc close blockframe.
 * Depends on: core/namespace.js, core/event-controller.js
 */
(function ($) {
    this.bindDocumentEvents = function (doc) {
        $(doc).bind("keyup keydown", function (e) {
            if (e.ctrlKey && e.shiftKey && e.which == 83) { // Ctrl + Shift + s
                if (e.type == 'keyup') {
                    e.stopPropagation();
                    dataLinqCode.events.fire('save-all-documents');
                }
                return false;
            }
            if (e.ctrlKey && e.which == 83) {  // Ctrl + s
                if (e.type == 'keyup') {
                    e.stopPropagation();
                    dataLinqCode.events.fire('save-current-document');
                }
                return false;
            }

            if (e.which === 116) { // F5 ( Ctrl + F5 )
                if (e.type == 'keyup') {
                    e.stopPropagation();
                    dataLinqCode.events.fire(e.ctrlKey ? 'run-current-document-in-tab' : 'run-current-document');
                }
                return false;
            }

            if (e.key === "Escape") {
                $('body').dataLinqCode_blockframe('close');
                return false;
            }
        });
    };
}).call(dataLinqCode, jQuery);
