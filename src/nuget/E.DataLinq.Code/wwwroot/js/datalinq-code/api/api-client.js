/**
 * DataLinq Code - API client (transport)
 *
 * dataLinqCode.api.get/post: authorized AJAX calls against dataLinqCode.targetUrl().
 * Feature endpoints are added by documents/documents-api.js, git/git-api.js,
 * key-value-store/key-value-store-api.js.
 * Depends on: core/namespace.js
 */
(function ($) {
        this.get = function (route, callback, data) {
            $.ajax({
                url: dataLinqCode.targetUrl() + '/' + route,
                data: data,
                headers: {
                    'Authorization': 'Bearer ' + window._datalinqCodeAccessToken
                },
                success: function (result) {
                    callback(result)
                }
            });
        };

        this.post = function (route, data, callback) {
            $.ajax({
                url: dataLinqCode.targetUrl() + '/' + route,
                type: 'POST',
                data: JSON.stringify(data),
                contentType: 'application/json',
                headers: {
                    'Authorization': 'Bearer ' + window._datalinqCodeAccessToken
                },
                success: function (result) {
                    callback(result)
                }
            });
        };
}).call(dataLinqCode.api, jQuery);
