var WebSocketBridgeLib = {
    $webSocket: null,
    $gameObjectName: null,

    WebSocketBridge_GetPageWebSocketUrl: function() {
        var protocol = (window.location.protocol === 'https:') ? 'wss://' : 'ws://';
        var url = protocol + window.location.host + '/ws';
        var bufferSize = lengthBytesUTF8(url) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(url, buffer, bufferSize);
        return buffer;
    },

    WebSocketBridge_DownloadFile: function(filenamePtr, contentPtr) {
        var filename = UTF8ToString(filenamePtr);
        var content = UTF8ToString(contentPtr);
        var blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
        var link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = filename;
        link.style.display = 'none';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(link.href);
    },

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
