/* global McpApp */
(function () {
  "use strict";

  var _toolResultCallback = null;
  var _hostContextCallback = null;
  var _isTestHarness = false;

  function isInTestHarness() {
    // Detect if running in test harness (parent sends mock messages)
    try {
      return window.parent !== window && window.location.pathname.includes("/templates/");
    } catch (e) {
      return false;
    }
  }

  function setupTestHarnessListener() {
    window.addEventListener("message", function (event) {
      if (!event || !event.data) return;
      var msg = event.data;

      // Handle JSON-RPC style messages from test harness
      if (msg.jsonrpc === "2.0" && msg.method) {
        if (msg.method === "ui/notifications/tool-result" && _toolResultCallback) {
          _toolResultCallback(msg.params);
        } else if (msg.method === "ui/notifications/host-context-changed" && _hostContextCallback) {
          _hostContextCallback(msg.params.context || msg.params);
        } else if (msg.method === "ui/notifications/tool-input") {
          // Tool input handling if needed
        }
      }
    });
  }

  function ensureSdk() {
    if (!window.McpApp) {
      return null; // SDK not loaded, will use fallback
    }
    return window.McpApp;
  }

  async function initialize(options) {
    _toolResultCallback = options && typeof options.onToolResult === "function" ? options.onToolResult : null;
    _hostContextCallback = options && typeof options.onHostContextChanged === "function" ? options.onHostContextChanged : null;

    // Always set up test harness listener as fallback
    _isTestHarness = isInTestHarness();
    setupTestHarnessListener();

    var sdk = ensureSdk();
    if (sdk) {
      try {
        var context = await sdk.initialize();

        if (_toolResultCallback) {
          sdk.onToolResult(_toolResultCallback);
        }

        if (_hostContextCallback) {
          sdk.onHostContextChanged(_hostContextCallback);
        }

        if (options && typeof options.onInitialized === "function") {
          options.onInitialized(context);
        }

        return context;
      } catch (err) {
        // SDK initialization failed (e.g., no real MCP host)
        console.warn("MCP Apps SDK initialization failed, using test harness fallback:", err.message);
      }
    }

    // Fallback for test harness mode when SDK fails or isn't available
    var fallbackContext = {
      hostContext: { theme: "light" },
      capabilities: {},
    };

    if (options && typeof options.onInitialized === "function") {
      options.onInitialized(fallbackContext);
    }

    return fallbackContext;
  }

  function applyTheme(theme) {
    var value = theme || "light";
    document.documentElement.setAttribute("data-theme", value);
  }

  window.DwsimApp = {
    initialize: initialize,
    applyTheme: applyTheme,
  };
})();
