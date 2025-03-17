dataLinqCode.eventController = function (baseObj) {
    this._baseObj = baseObj;
    this._events = [];
    this.on = function (channel, fn, context) {
        if (typeof channel === 'string') {
            if (!this._events[channel])
                this._events[channel] = [];
            this._events[channel].push({ context: context || this, callback: fn });
        }
        else {
            for (var i = 0; i < channel.length; i++) {
                this.on(channel[i], fn, context);
            }
        }
        return this;
    };
    this.off = function (channel, fn) {
        if (typeof channel === 'string') {
            if (!this._events[channel])
                return;

            var events = [];

            for (var e in this._events[channel]) {
                var event = this._events[channel][e];

                if (event.callback === fn) {
                    //console.log('off event', event);
                    continue;
                }

                events.push(event);
            }

            this._events[channel] = events;
        } else {
            for (var i = 0; i < channel.length; i++) {
                this.off(channel[i], fn);
            }
        }
    };
    this.fire = function (channel) {
        if (!this._events[channel])
            return false;
        var args = Array.prototype.slice.call(arguments, 1);
        var eventArgs = [];
        eventArgs.push({ channel: channel });
        for (var i = 0; i < args.length; i++)
            eventArgs.push(args[i]);
        for (var i = 0, l = this._events[channel].length; i < l; i++) {
            var subscription = this._events[channel][i];
            subscription.callback.apply(subscription.context, eventArgs);
        }
        return this;
    };
};

new dataLinqCode.implementEventController(dataLinqCode);