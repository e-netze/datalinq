/**
 * DataLinq Code - documents API
 *
 * Endpoint/query/view CRUD, verification, doc info, folder structure, editor snippets.
 * Depends on: api/api-client.js
 */
(function ($) {
        this.saveFolderStructure = function (folderStructure, callback) {
            this.post('saveFolderStructure', folderStructure, callback);
        };

        this.getFolderStructure = function (callback) {
            this.get('getFolderStructure', callback);
        };

        this.getMonacoSnippit = function (callback, lang, helper) {
            this.get('getMonacoSnippit', callback, { lang: lang, helper: helper || 'dlh' });
        };

        this.getEndPoints = function (callback) {
            this.get('getEndPoints', callback)
        };
        this.getQueries = function (endPoint, callback) {
            this.get('getQueries?endPoint=' + endPoint, callback);
        };
        this.getViews = function (endPoint, query, callback) {
            this.get('getViews?endPoint=' + endPoint + '&query=' + query, callback);
        };

        this.createEndPoint = function (endPoint, callback) {
            this.get('createEndPoint', callback, { endPoint: endPoint });
        };
        this.createQuery = function (endPoint, query, callback) {
            this.get('createEndPointQuery', callback, { endPoint: endPoint, query: query });
        };
        this.createView = function (endPoint, query, view, callback) {
            this.get('createEndPointQueryView', callback, { endPoint: endPoint, query: query, view: view });
        };

        this.deleteEndPoint = function (endPoint, callback) {
            this.get('deleteEndPoint', callback, { endPoint: endPoint });
        };
        this.deleteQuery = function (endPoint, query, callback) {
            this.get('deleteEndPointQuery', callback, { endPoint: endPoint, query: query });
        };
        this.deleteView = function (endPoint, query, view, callback) {
            this.get('deleteEndPointQueryView', callback, { endPoint: endPoint, query: query, view: view });
        };

        this.verifyView = function (endPoint, query, view, callback) {
            this.get('verifyEndPointQueryView', callback, { endPoint: endPoint, query: query, view: view });
        };

        this.docInfo = function (endPoint, query, view, rewrite, callback) {
            this.get('docInfo', callback, { endPoint: endPoint, query: query || '', view: view || '', rewrite: rewrite });
        }
}).call(dataLinqCode.api, jQuery);
