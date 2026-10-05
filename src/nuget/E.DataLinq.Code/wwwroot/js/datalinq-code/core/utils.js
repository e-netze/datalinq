/**
 * DataLinq Code - timing utilities
 *
 * dataLinqCode.timer: restartable/cancelable timeout. dataLinqCode.delayed: run a callback later.
 * Depends on: core/namespace.js
 */
(function ($) {
    this.timer = function (callback, duration, arg) {
        var _timer = 0;
        var _callback = callback;
        var _duration = duration;
        var _arg = arg;
        this.SetArgument = function (arg) { _arg = arg; };
        this.SetDuration = function (d) {
            _duration = d;
        };
        this.Duration = function () { return _duration; };
        this.Start = function (arg) {
            window.clearTimeout(_timer);
            if (arg)
                _arg = arg;
            if (_duration == 0) {
                if (_arg)
                    _callback(_arg);
                else
                    _callback();
            }
            else {
                if (_arg)
                    _timer = window.setTimeout(function () { _callback(_arg); }, _duration);
                else
                    _timer = window.setTimeout(_callback, _duration);
            }
        };
        this.StartWith = function (callbackFunction) {
            _callback = callbackFunction;
            this.Start();
        };
        this.Stop = function () { window.clearTimeout(_timer); };
        this.Exec = function () {
            window.clearTimeout(_timer);
            if (_arg)
                _callback(_arg);
            else
                _callback();
        };
        this.start = this.Start;
        this.stop = this.Stop;
        this.startWidth = this.StartWidth;
        this.exec = this.Exec;
    };

    this.delayed = function (callback, duration, arg) {
        var timer = new dataLinqCode.timer(callback, duration ? duration : 1, arg);
        timer.Start();
    };
}).call(dataLinqCode, jQuery);
