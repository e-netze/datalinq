/**
 * DataLinq Code - version control (git) API
 *
 * dataLinqCode.api.git(operation, request, callback): runs a git operation on the DataLinq instance.
 * The callback always receives a result object { success, message, errorCode, data, status, imported, deleted, importErrors }
 * (also on transport errors).
 * Depends on: api/api-client.js
 */
(function ($) {
    this.git = function (operation, request, callback) {
        $.ajax({
            url: dataLinqCode.targetUrl() + '/Git?op=' + encodeURIComponent(operation),
            type: 'POST',
            data: JSON.stringify(request || {}),
            contentType: 'application/json',
            headers: {
                'Authorization': 'Bearer ' + window._datalinqCodeAccessToken
            },
            success: function (result) {
                if (!result || typeof result !== 'object') {
                    result = { success: false, errorCode: 'error', message: 'Empty response from server' };
                }
                if (result.success !== true && !result.message) {
                    result.message = result.errorMessage || result.error_message || result.ErrorMessage || 'Unknown error';
                }
                callback(result);
            },
            error: function (xhr) {
                callback({
                    success: false,
                    errorCode: 'error',
                    message: 'Request failed: ' + xhr.status + ' ' + (xhr.statusText || '')
                });
            }
        });
    };
}).call(dataLinqCode.api, jQuery);
