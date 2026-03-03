const Localizer = {
    _cache: {},
    _ready: null, 

    init: function (api) {
        if (this._ready) return this._ready; 

        const dfd = $.Deferred();
        this._ready = dfd.promise();

        api.getTranslations(function (data) {
            try {
                Localizer._cache = (typeof data === 'string') ? JSON.parse(data) : data;
                dfd.resolve(Localizer._cache);
            } catch (e) {
                console.error('Failed to parse translations', e, data);
                dfd.reject(e);
            }
        });

        return this._ready;
    },

    get: function (key) {
        return this._cache[key] || key;
    }
};