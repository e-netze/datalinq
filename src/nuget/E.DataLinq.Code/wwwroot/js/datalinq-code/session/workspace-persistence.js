/**
 * DataLinq Code - workspace persistence
 *
 * On beforeunload: stores open tabs, sidebar width and color scheme in localStorage
 * so they can be restored on the next visit.
 * Depends on: nothing (plain DOM)
 */
(function () {
        window.addEventListener('beforeunload', function () {
            // save last opened tabs to localstorage to possible restore them on next login
            const tabs = document.querySelectorAll('.datalinq-code-tab[data-id]');
            const tabIds = Array.from(tabs)
                .map(tab => tab.getAttribute('data-id'))
                .filter(id => id !== '_start');
            localStorage.setItem('datalinq-open-tabs', JSON.stringify(tabIds));

            // save the individual width of sidebar of user
            const sidebarWidth = getComputedStyle(document.documentElement).getPropertyValue('--sidebar-width').trim();
            if (sidebarWidth !== '300px') {
                localStorage.setItem('sidebarWidth', sidebarWidth);
            } else {
                localStorage.removeItem('sidebarWidth'); // Optional: clear if default
            }

            //save current editor theme
            const ide = document.querySelector('.datalinq-code-ide');

            if (ide && ide.classList.contains('colorscheme-light')) {
                localStorage.setItem('editorColorScheme', 'vs');
            } else {
                localStorage.removeItem('editorColorScheme');
            }

        });
})();
