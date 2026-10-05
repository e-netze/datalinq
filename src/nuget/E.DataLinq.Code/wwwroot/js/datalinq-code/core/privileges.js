/**
 * DataLinq Code - user privileges
 *
 * dataLinqCode.privileges.* answers what the logged-in user may create/delete.
 * Reads state.userPrivileges, which is set by dataLinqCode.start().
 * Depends on: core/namespace.js
 */
(function ($) {
    var state = dataLinqCode._state;

    this.privileges = new function () {
        this.createEndpoints = function () { return state.userPrivileges.createEndpoints === true; };
        this.createQueries = function () { return state.userPrivileges.createQueries === true; };
        this.createViews = function () { return state.userPrivileges.createViews === true; };
        this.deleteEndpoints = function () { return state.userPrivileges.deleteEndpoints === true; };
        this.deleteQueries = function () { return state.userPrivileges.deleteQueries === true; };
        this.deleteViews = function () { return state.userPrivileges.deleteViews === true; };
        this.useEndpointSelection = function () { return state.userPrivileges.useAppPrefixFilters === true; };
    };
}).call(dataLinqCode, jQuery);
