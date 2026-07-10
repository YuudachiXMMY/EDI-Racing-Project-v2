var WebSocketBridgeLib = {
    $webSocket: null,
    $gameObjectName: null,

    WebSocketBridge_Connect: function(urlPtr, goNamePtr) {
        var url = UTF8ToString(urlPtr);
        gameObjectName = UTF8ToString(goNamePtr);

        if (webSocket != null && webSocket.readyState < 2) {
            webSocket.close();
        }

        webSocket = new WebSocket(url);

        webSocket.onopen = function() {
            SendMessage(gameObjectName, 'OnWebSocketOpen', '');
        };

        webSocket.onmessage = function(evt) {
            SendMessage(gameObjectName, 'OnWebSocketMessage', evt.data);
        };

        webSocket.onclose = function(evt) {
            SendMessage(gameObjectName, 'OnWebSocketClose', evt.code.toString());
        };

        webSocket.onerror = function() {
            SendMessage(gameObjectName, 'OnWebSocketError', 'Connection error');
        };
    },

    WebSocketBridge_Send: function(msgPtr) {
        if (webSocket == null || webSocket.readyState !== 1) return;
        var msg = UTF8ToString(msgPtr);
        webSocket.send(msg);
    },

    WebSocketBridge_Close: function() {
        if (webSocket == null) return;
        webSocket.close();
        webSocket = null;
    },

    WebSocketBridge_GetState: function() {
        if (webSocket == null) return 3; // CLOSED
        return webSocket.readyState;
    }
};

autoAddDeps(WebSocketBridgeLib, '$webSocket');
autoAddDeps(WebSocketBridgeLib, '$gameObjectName');
mergeInto(LibraryManager.library, WebSocketBridgeLib);
