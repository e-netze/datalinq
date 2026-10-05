/**
 * DataLinq Code - namespace
 *
 * Creates the global dataLinqCode object. MUST be the first datalinq-code script.
 * 
 * Extending the IDE:
 *   - Feature event handlers: dataLinqCode.features.register(name, function (state) { ... })
 *     in a <feature>/<feature>-handlers.js file. All registered features are initialized
 *     (in script order) by dataLinqCode.start().
 *   - Server calls: add methods to dataLinqCode.api (see api/api-client.js).
 *   - Dialogs: add methods to dataLinqCode.ui (see ui/dialogs.js).
 *   - Add the new script to Views/Shared/_DataLinqCodeScripts.cshtml.
 */
/**
 * @typedef {Object} DataLinqCodeState
 * @property {string} targetUrl          DataLinq Code backend url
 * @property {string} dataLinqEngineUrl  DataLinq engine url (used to run documents)
 * @property {string} username           logged-in user
 * @property {Object} userPrivileges     raw privileges (see core/privileges.js)
 * @property {string} editorTheme        monaco theme: "vs-dark" or "vs"
 * @property {jQuery} $tree              tree plugin element
 * @property {jQuery} $editor            editor plugin element
 * @property {jQuery} $toolbar           toolbar plugin element
 * @property {Object<string,string>} gitStatuses git status per document id
 */

var dataLinqCode = new function ($) {
    var me = this;

    /** @type {DataLinqCodeState} internal shared state, do not use outside datalinq-code scripts */
    this._state = {
        targetUrl: null,
        dataLinqEngineUrl: null,
        username: null,
        userPrivileges: {},
        editorTheme: 'vs-dark',
        $tree: null,
        $editor: null,
        $toolbar: null,
        gitStatuses: {}
    };

    /** @returns {string} DataLinq Code backend url */
    this.targetUrl = () => me._state.targetUrl;
    /** @returns {string} logged-in user name */
    this.loginUsername = () => me._state.username;
    /** @returns {string} current monaco theme ("vs-dark" or "vs") */
    this.editorTheme = () => me._state.editorTheme;

    /** Server API. Populated by api/api-client.js and the feature *-api.js files. */
    this.api = {};

    /** UI helpers (dialogs, editors). Populated by ui/dialogs.js and feature dialog files. */
    this.ui = {};

    /**
     * Adds an event controller (on/off/fire) as obj.events.
     * @param {Object} obj
     */
    this.implementEventController = function (obj) {
        obj.events = new dataLinqCode.eventController(obj);
    };

    /** Feature registry. Features wire their event handlers when the IDE starts. */
    this.features = new function () {
        var _features = [];

        /**
         * Registers a feature initializer.
         * @param {string} name unique feature name
         * @param {function(DataLinqCodeState):void} init called once by dataLinqCode.start()
         */
        this.register = function (name, init) {
            _features.push({ name: name, init: init });
        };

        /**
         * Initializes all registered features in registration (= script) order.
         * @param {DataLinqCodeState} state
         */
        this.initAll = function (state) {
            for (var i = 0; i < _features.length; i++) {
                _features[i].init.call(me, state);
            }
        };

        /** @returns {string[]} names of the registered features */
        this.names = function () {
            return _features.map(function (f) { return f.name; });
        };
    };
}(jQuery);
