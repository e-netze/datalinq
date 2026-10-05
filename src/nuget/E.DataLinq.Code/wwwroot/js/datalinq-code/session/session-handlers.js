/**
 * DataLinq Code - session
 *
 * Clears session data when the page is left. Handles: logout.
 * Depends on: core
 */
/**
 * Feature "session": session cleanup and logout.
 * Initialized by dataLinqCode.start() via dataLinqCode.features.initAll().
 * @param {DataLinqCodeState} state shared IDE state (see core/namespace.js)
 */
dataLinqCode.features.register('session', function (state) {
        function clearSessionData() {
            sessionStorage.removeItem('currentChatId');
        }

        window.addEventListener('beforeunload', clearSessionData);
        window.addEventListener('unload', clearSessionData);
        window.addEventListener('pagehide', clearSessionData);

        dataLinqCode.events.on('logout', function (channel) {
            document.location = document.location + '/logout';
        });
});
