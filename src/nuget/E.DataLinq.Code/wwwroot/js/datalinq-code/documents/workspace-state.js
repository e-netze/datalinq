/**
 * DataLinq Code - workspace state
 *
 * dataLinqCode.allDocuments(): all tree documents with their open state.
 * dataLinqCode.get/setEndpointSelection(), addEndpointToSelectionIfActive(): endpoint filter (null = all).
 * Depends on: core/namespace.js
 */
(function ($) {
    var state = dataLinqCode._state;

    this.allDocuments = function () {
        var documents = [];

        state.$tree.find('.tree-node').not('.folder').each(function (i, node) {
            var $node = $(node);
            var route = $node.data('data-route');
            if (route) {
                documents.push({
                    id: route,
                    isOpen: state.$editor.dataLinqCode_editor('isOpen', { id: route })
                });
            }
        });

        return documents;
    };

    // Endpoint selection (null = all endpoints)
    var _endpointSelection = null;
    this.getEndpointSelection = function () {
        return _endpointSelection;
    };
    this.setEndpointSelection = function (endPoints) {
        _endpointSelection = Array.isArray(endPoints) ? endPoints.slice() : null;
    };
    this.addEndpointToSelectionIfActive = function (endPoint) {
        if (_endpointSelection !== null && $.inArray(endPoint, _endpointSelection) < 0) {
            _endpointSelection.push(endPoint);
        }
    };
}).call(dataLinqCode, jQuery);
