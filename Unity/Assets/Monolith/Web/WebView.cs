﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using Newtonsoft.Json;

using UnityEngine;

namespace Monolith.Web {
public class WebView : MonoBehaviour {

#region Types
    // public class WebSocketMessage<T> {
    //     public string type;
    //     public T data;
    // }

    // public struct Route {
        // public readonly string httpMethod;
        // public readonly string path;

        // public Route(string httpMethod, string path) {
            // this.httpMethod = httpMethod;
            // this.path = path;
        // }
    // }

    [Serializable]
    private struct NewLogsResponse {
        public ConsoleLog[] logs;
    }
#endregion Types

#region Fields
    private const int PORT = 8181;

    private HttpListener _listener;
    private Thread _listenerThread;
    private string _dataPath;

    // private Dictionary<Route, Action> _routeActions = new();

    /// <summary>
    /// Queue of logs that have been added since the last time the queue was processed.
    /// Should we store this per client?
    /// </summary>
    private readonly Queue<ConsoleLog> _logQueue = new();

    private readonly Queue<Action> _actionQueue = new();
#endregion Fields

#region Public
#endregion Public

#region Private
    private void Awake() {
        // todo: can we use resources instead?
        _dataPath = $"{Application.dataPath}/www";

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{PORT}/"); // Bind to all IPs on the machine, port 8080.

        _listenerThread = new Thread(StartListener);
        _listenerThread.Start();

        Console.AddLogAddedListener(OnLogAdded);
    }

    private void OnDestroy() {
        Cleanup();
    }

    private void OnLogAdded(ConsoleLog consoleLog) {
        _logQueue.Enqueue(consoleLog);
    }

    private void Update() {
        // process action queue
        while (_actionQueue.Count > 0) {
            _actionQueue.Dequeue().Invoke();
        }
    }

    private void Cleanup() {
        _listener.Stop();
        _listenerThread.Abort();
    }

    private void StartListener() {
        _listener.Start();

        while (true) {
            try {
                HttpListenerContext context = _listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                string requestedPath = request.Url.AbsolutePath;
                string filePath = Path.Combine(_dataPath, requestedPath.TrimStart('/'));

                Action action;
                if (request is { HttpMethod: "GET", Url: { AbsolutePath: "/", }, }) {
                    action = () => ServeFile(response, Path.Combine(_dataPath, "index.html"));
                }
                else if (request is { HttpMethod: "GET", Url: { AbsolutePath: "/log", }, }) {
                    action = () => ServeNewLogs(response);
                }
                else if (request is { HttpMethod: "POST", Url: { AbsolutePath: "/command", }, }) {
                    action = () => HandleCommand(request);
                }
                else if (request is { HttpMethod: "GET", }) {
                    action = () => {
                        if (File.Exists(filePath)) {
                            ServeFile(response, filePath);
                        }
                        else {
                            response.StatusCode = 404;
                            response.StatusDescription = "File not found.";
                            response.Close();
                        }
                    };
                }
                else {
                    action = null;
                }

                action?.Invoke();
            }
            catch (HttpListenerException e) {
                // listener was stopped, exit the loop
                Debug.LogError($"[Console] [WebView] HttpListenerException: {e.Message}");
                break;
            }
            catch (InvalidOperationException e) {
                // listener was stopped, exit the loop
                Debug.LogError($"[Console] [WebView] InvalidOperationException: {e.Message}");
                break;
            }
        }
    }

    // private async void HandleWebSocketRequest(HttpListenerContext context,  HttpListenerRequest request) {
    //     Debug.Log("[Console] [WebView] WebSocket request received.");
    //
    //     string subProtocol = request.Headers["Sec-WebSocket-Protocol"];
    //     HttpListenerWebSocketContext webSocketContext = await context.AcceptWebSocketAsync(subProtocol);
    //     HandleWebSocket(webSocketContext.WebSocket);
    // }

    // private async void HandleWebSocket(WebSocket webSocket) {
    //     Debug.Log("[Console] [WebView] WebSocket opened.");
    //
    //     var buffer = new byte[1024];
    //     WebSocketReceiveResult result;
    //
    //     do {
    //         result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
    //
    //         string receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
    //         Debug.Log($"[Console] [WebView] Received message: {receivedMessage}");
    //
    //         if(_logQueue.Count > 0) {
    //             NewLogsResponse outputObj = new() {
    //                 logs = _logQueue.ToArray(),
    //             };
    //
    //             _logQueue.Clear();
    //
    //             WebSocketMessage<NewLogsResponse> message = new() {
    //                 type = "logs_new",
    //                 data = outputObj,
    //             };
    //             string outStr = JsonConvert.SerializeObject(message, new ColorJsonConverter());
    //
    //             buffer = Encoding.UTF8.GetBytes(outStr);
    //             await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    //         }
    //     } while (result.CloseStatus.HasValue == false);
    //
    //     Debug.Log($"[Console] [WebView] WebSocket closed: {result.CloseStatus.Value} {result.CloseStatusDescription}");
    // }

    private void HandleCommand(HttpListenerRequest request) {
        using StreamReader reader = new(request.InputStream, request.ContentEncoding);
        string content = reader.ReadToEnd();
        Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);

        _actionQueue.Enqueue(() => { Console.ExecuteCommand(data["command"]); });
    }

    private static void ServeFile(HttpListenerResponse response, string filePath) {
        string mimeType = GetMimeType(filePath);
        byte[] fileBytes = File.ReadAllBytes(filePath);

        response.ContentType = mimeType;
        response.ContentLength64 = fileBytes.Length;
        Stream output = response.OutputStream;
        output.Write(fileBytes, 0, fileBytes.Length);
        output.Close();
    }

    private void ServeNewLogs(HttpListenerResponse response) {
        NewLogsResponse outputObj = new() {
            logs = _logQueue.ToArray(),
        };
        _logQueue.Clear();
        string outStr = JsonConvert.SerializeObject(outputObj, new ColorJsonConverter());

        byte[] buffer = Encoding.UTF8.GetBytes(outStr);
        response.ContentLength64 = buffer.Length;

        Stream output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
        output.Close();
    }

    // private bool IsWebSocketUpgrade(HttpListenerRequest request) {
    //     return
    //         request.Headers["Upgrade"]?.ToLower() == "websocket"
    //      && request.Headers["Connection"]?.ToLower() == "upgrade";
    // }

    private static string GetMimeType(string filePath) {
        string extension = Path.GetExtension(filePath).ToLower();
        return extension switch {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".svg" => "image/svg+xml",
            ".gif" => "image/gif",
            ".jpeg" => "image/jpeg",
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream",
        };
    }
#endregion Private

}
}