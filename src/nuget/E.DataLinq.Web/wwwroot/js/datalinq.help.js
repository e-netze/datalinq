(function () {
    'use strict';

    // --- color scheme (sent by DataLinq Code via postMessage) -------------------------------

    function applyHelpColorScheme(theme) {
        var isLight = theme === 'vs';
        document.documentElement.classList.toggle('colorscheme-light', isLight);
        if (document.body) {
            document.body.classList.toggle('colorscheme-light', isLight);
        }
    }

    // restore the scheme remembered from a previous message, so it survives in-frame navigations
    var storedHelpTheme = sessionStorage.getItem('helpColorScheme');
    if (storedHelpTheme) {
        applyHelpColorScheme(storedHelpTheme);
    }

    window.addEventListener('message', function (event) {
        if (event.data && event.data.theme) {
            sessionStorage.setItem('helpColorScheme', event.data.theme);
            applyHelpColorScheme(event.data.theme);
        }
    });

    // --- language ---------------------------------------------------------------------------

    function navigateToLanguage(lang) {
        var url = new URL(window.location.href);
        url.searchParams.set('lang', lang);
        window.location.href = url.toString();
    }

    window.changeLanguage = function (lang) {
        sessionStorage.setItem('selectedLang', lang);
        if (window.parent && window.parent !== window) {
            window.parent.postMessage({ lang: lang }, '*');
        }
        navigateToLanguage(lang);
    };

    // --- search -------------------------------------------------------------------------------

    window.applyHelpSearch = function (input) {
        var value = (input.value || '').trim().toLowerCase();
        var terms = value.split(/\s+/).filter(function (t) { return t.length > 0; });
        var panel = document.querySelector('.class-panel.active');

        if (!panel) {
            return;
        }

        panel.querySelectorAll('.help-item').forEach(function (item) {
            var text = item.getAttribute('data-search') || item.textContent.toLowerCase();
            var visible = terms.every(function (t) { return text.indexOf(t) >= 0; });
            item.hidden = !visible;
        });

        var anyVisible = false;
        panel.querySelectorAll('.help-group').forEach(function (group) {
            var groupVisible = group.querySelector('.help-item:not([hidden])') !== null;
            group.hidden = !groupVisible;
            anyVisible = anyVisible || groupVisible;
        });

        var about = panel.querySelector('.help-about');
        if (about) {
            about.hidden = terms.length > 0;
        }

        var noResults = panel.querySelector('.help-no-results');
        if (noResults) {
            noResults.hidden = anyVisible;
        }
    };

    // --- copy ---------------------------------------------------------------------------------

    function copyText(text, button) {
        text = text.replace(new RegExp(String.fromCharCode(160), 'g'), ' ');

        var done = function () {
            var original = button.getAttribute('data-label') || button.textContent;
            button.setAttribute('data-label', original);
            button.textContent = '✓';
            button.classList.add('copied');
            setTimeout(function () {
                button.textContent = original;
                button.classList.remove('copied');
            }, 1200);
        };

        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(done, function () { });
        }
    }

    // --- init ---------------------------------------------------------------------------------

    document.addEventListener('DOMContentLoaded', function () {
        if (storedHelpTheme) {
            applyHelpColorScheme(storedHelpTheme);
        }

        var page = document.querySelector('.help-page');
        var currentLang = page ? page.getAttribute('data-lang') : 'en';

        // keep the language chosen before (e.g. in the editor) when the frame is (re)loaded
        var storedLang = sessionStorage.getItem('selectedLang');
        if (storedLang && storedLang !== currentLang) {
            navigateToLanguage(storedLang);
            return;
        }

        document.querySelectorAll('.help-lang-switch button[data-lang]').forEach(function (button) {
            button.addEventListener('click', function () {
                var lang = button.getAttribute('data-lang');
                if (lang !== currentLang) {
                    window.changeLanguage(lang);
                }
            });
        });

        // class tabs
        var panels = Array.prototype.slice.call(document.querySelectorAll('.class-panel'));
        var tabs = Array.prototype.slice.call(document.querySelectorAll('.help-tab'));
        var searchInput = document.querySelector('input.content-search');

        function setActiveClass(classIndex) {
            panels.forEach(function (panel) {
                panel.classList.toggle('active', panel.getAttribute('data-class-index') === classIndex);
            });
            tabs.forEach(function (tab) {
                var active = tab.getAttribute('data-class-index') === classIndex;
                tab.classList.toggle('active', active);
                tab.setAttribute('aria-selected', active ? 'true' : 'false');
            });

            sessionStorage.setItem('selectedHelpClass', classIndex);

            if (searchInput) {
                window.applyHelpSearch(searchInput);
            }
        }

        if (panels.length > 0) {
            var storedClass = sessionStorage.getItem('selectedHelpClass');
            var initialClass = storedClass && panels.some(function (p) { return p.getAttribute('data-class-index') === storedClass; })
                ? storedClass
                : panels[0].getAttribute('data-class-index');

            tabs.forEach(function (tab) {
                tab.addEventListener('click', function () {
                    setActiveClass(tab.getAttribute('data-class-index'));
                });
            });

            setActiveClass(initialClass);
        }

        if (searchInput) {
            var storedSearch = sessionStorage.getItem('helpSearch');
            if (storedSearch) {
                searchInput.value = storedSearch;
                window.applyHelpSearch(searchInput);
            }

            searchInput.addEventListener('input', function (e) {
                e.stopPropagation();
                sessionStorage.setItem('helpSearch', searchInput.value);
                window.applyHelpSearch(searchInput);
            });
            searchInput.addEventListener('keyup', function (e) { e.stopPropagation(); });
        }

        // copy buttons
        document.addEventListener('click', function (e) {
            var button = e.target.closest ? e.target.closest('button.copy-button') : null;
            if (!button) {
                return;
            }

            e.stopPropagation();

            var block = button.closest('.help-code-block');
            var code = block ? block.querySelector('.copyable-content') : null;
            if (code) {
                copyText(code.innerText, button);
            }
        });
    });
})();
