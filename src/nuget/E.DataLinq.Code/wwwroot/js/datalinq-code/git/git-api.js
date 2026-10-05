/**
 * DataLinq Code - git API
 *
 * Commit/push, git status and repository initialization endpoints.
 * Depends on: api/api-client.js
 */
(function ($) {
        this.commitAndPushChanges = function (details, callback) {
            this.post('commitAndPushChanges', details, callback);
        };

        this.checkGitStatus = function (endPoint, query, view, callback) {
            this.get('checkGitStatus', callback, { endPoint: endPoint, query: query, view: view });
        };

        this.initializeGitRepository = function (callback) {
            this.get('initializeGitRepository', callback);
        };
}).call(dataLinqCode.api, jQuery);
