/**
 * DataLinq Code - color scheme
 *
 * Handles: toggle-color-scheme (fires theme-changed and notifies all iframes).
 * Restores the persisted scheme (localStorage "editorColorScheme") on start.
 * Depends on: core
 */
/**
 * Feature "theme": dark/light color scheme.
 * Initialized by dataLinqCode.start() via dataLinqCode.features.initAll().
 * @param {DataLinqCodeState} state shared IDE state (see core/namespace.js)
 */
dataLinqCode.features.register('theme', function (state) {
        dataLinqCode.events.on('toggle-color-scheme', function (channel) {
            const ide = $('.datalinq-code-ide');
            ide.toggleClass('colorscheme-light');
            state.editorTheme = ide.hasClass('colorscheme-light') ? 'vs' : 'vs-dark';

            dataLinqCode.events.fire('theme-changed', {
                theme: state.editorTheme
            });

            sessionStorage.setItem('editorTheme', state.editorTheme);
            localStorage.setItem('editorColorScheme', state.editorTheme);

            ide.find('iframe').each(function () {
                this.contentWindow.postMessage({ theme: state.editorTheme }, '*');
            });

            const helpFrame = document.getElementById('help-frame');
            if (helpFrame && helpFrame.contentWindow) {
                helpFrame.contentWindow.postMessage({ theme: state.editorTheme }, '*');
            }
        }, this);

        const savedTheme = localStorage.getItem('editorColorScheme');
        if (savedTheme === 'vs') {
            dataLinqCode.events.fire('toggle-color-scheme');
        } 
});
