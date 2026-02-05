/* global window */
(function () {
  "use strict";

  function MockAppBridge(iframe, options) {
    this.iframe = iframe;
    this.onMessage = options && options.onMessage ? options.onMessage : null;
    this.messageHandler = this._handleMessage.bind(this);
    window.addEventListener("message", this.messageHandler);
  }

  MockAppBridge.prototype._postMessage = function (message) {
    if (!this.iframe.contentWindow) return;
    this.iframe.contentWindow.postMessage(message, "*");
  };

  MockAppBridge.prototype.sendToolResult = function (result) {
    this._postMessage({
      jsonrpc: "2.0",
      method: "ui/notifications/tool-result",
      params: {
        content: result.content || [],
        structuredContent: result.structuredContent || null,
      },
    });
  };

  MockAppBridge.prototype.sendToolInput = function (input) {
    this._postMessage({
      jsonrpc: "2.0",
      method: "ui/notifications/tool-input",
      params: { input: input },
    });
  };

  MockAppBridge.prototype.sendHostContext = function (context) {
    this._postMessage({
      jsonrpc: "2.0",
      method: "ui/notifications/host-context-changed",
      params: { context: context },
    });
  };

  MockAppBridge.prototype._handleMessage = function (event) {
    if (!event || !event.data) return;
    if (this.onMessage) {
      try {
        this.onMessage(JSON.stringify(event.data));
      } catch (err) {
        this.onMessage(String(event.data));
      }
    }
  };

  MockAppBridge.prototype.destroy = function () {
    window.removeEventListener("message", this.messageHandler);
  };

  window.MockAppBridge = MockAppBridge;
})();
