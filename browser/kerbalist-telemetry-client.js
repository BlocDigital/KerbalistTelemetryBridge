/* Kerbalist Telemetry Bridge client — Step 1: automatic connection. */
(function (global) {
  "use strict";

  class KerbalistTelemetryClient {
    constructor(options) {
      options = options || {};
      this.baseUrl = options.baseUrl || "http://127.0.0.1:8765";
      this.wsUrl = options.wsUrl || "ws://127.0.0.1:8765/ws";
      this.reconnectMs = options.reconnectMs || 1000;
      this.maxReconnectMs = options.maxReconnectMs || 5000;
      this.onState = options.onState || function () {};
      this.onStatus = options.onStatus || function () {};
      this.ws = null;
      this.timer = null;
      this.closedByUser = false;
      this.delay = this.reconnectMs;
      this.lastState = null;
    }

    connect() {
      this.closedByUser = false;
      this._clearTimer();
      this._setStatus("connecting");

      try {
        this.ws = new WebSocket(this.wsUrl);
        this.ws.onopen = () => {
          this.delay = this.reconnectMs;
          this._setStatus("connected");
        };
        this.ws.onmessage = (event) => {
          try {
            const state = JSON.parse(event.data);
            this.lastState = state;
            this.onState(state);
          } catch (error) {
            this.onStatus({ state: "error", error: error });
          }
        };
        this.ws.onerror = () => this._setStatus("error");
        this.ws.onclose = () => {
          this.ws = null;
          if (!this.closedByUser) this._scheduleReconnect();
        };
      } catch (error) {
        this._setStatus("error", error);
        this._scheduleReconnect();
      }
    }

    disconnect() {
      this.closedByUser = true;
      this._clearTimer();
      if (this.ws) {
        try { this.ws.close(); } catch (_) {}
        this.ws = null;
      }
      this._setStatus("disconnected");
    }

    async isAvailable() {
      try {
        const response = await fetch(this.baseUrl + "/api/status", {
          method: "GET",
          cache: "no-store"
        });
        return response.ok;
      } catch (_) {
        return false;
      }
    }

    _scheduleReconnect() {
      this._setStatus("disconnected");
      this._clearTimer();
      const wait = this.delay;
      this.delay = Math.min(this.delay * 2, this.maxReconnectMs);
      this.timer = setTimeout(() => this.connect(), wait);
    }

    _clearTimer() {
      if (this.timer !== null) {
        clearTimeout(this.timer);
        this.timer = null;
      }
    }

    _setStatus(state, error) {
      this.onStatus({ state: state, error: error || null });
    }
  }

  global.KerbalistTelemetryClient = KerbalistTelemetryClient;
})(window);
