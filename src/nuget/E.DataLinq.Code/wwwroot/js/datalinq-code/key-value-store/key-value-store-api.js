/**
 * DataLinq Code - key/value store API
 *
 * Environments, secrets and constants endpoints.
 * Depends on: api/api-client.js
 */
(function ($) {
        this.getKeyValueStoreEnvironments = function (callback) {
            this.get('getKeyValueStoreEnvironments', callback);
        };

        this.getSecretKeys = function (callback, environment) {
            this.get('getSecretKeys', callback, { environment: environment });
        };
        this.setSecret = function (key, value, callback, environment) {
            this.post('setSecret', { key: key, value: value, environment: environment }, callback);
        };
        this.deleteSecret = function (key, callback, environment) {
            this.post('deleteSecret', { key: key, environment: environment }, callback);
        };

        this.getConstantKeys = function (callback, environment) {
            this.get('getConstantKeys', callback, { environment: environment });
        };
        this.getConstantValue = function (key, callback, environment) {
            this.get('getConstantValue', callback, { key: key, environment: environment });
        };
        this.setConstant = function (key, value, callback, environment) {
            this.post('setConstant', { key: key, value: value, environment: environment }, callback);
        };
        this.deleteConstant = function (key, callback, environment) {
            this.post('deleteConstant', { key: key, environment: environment }, callback);
        };
}).call(dataLinqCode.api, jQuery);
