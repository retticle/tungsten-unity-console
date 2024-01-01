using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using Newtonsoft.Json;

using UnityEngine;
using UnityEngine.Networking;

namespace Tungsten.Console.Web {
public class WebView : MonoBehaviour {

#region Types
    [Serializable]
    private struct LogResponse {
        public ConsoleLog[] logs;
    }
#endregion Types

#region Fields
    private const int PORT = 3001;

    private HttpListener _listener;
    private Thread _listenerThread;
    private string _dataPath;
    private readonly Queue<Action> _actionQueue = new();
#endregion Fields

#region Private
    private void Awake() {
        _dataPath = Path.Combine(Application.streamingAssetsPath, "TungstenConsole");

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{PORT}/");

        _listenerThread = new Thread(StartListener);
        _listenerThread.Start();
    }

    private void Update() {
        while (_actionQueue.Count > 0) {
            _actionQueue.Dequeue()?.Invoke();
        }
    }

    private void OnDestroy() {
        Cleanup();
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
                    action = () => StartCoroutine(HandleServeFileRoutine(response, Path.Combine(_dataPath, "index.html")));
                }
                else if (request is { HttpMethod: "GET", Url: { AbsolutePath: "/log", }, }) {
                    action = () => HandleLogs(request, response);
                }
                else if (request is { HttpMethod: "POST", Url: { AbsolutePath: "/command", }, }) {
                    action = () => HandleCommand(request);
                }
                else if (request is { HttpMethod: "GET", }) {
                    action = () => StartCoroutine(HandleServeFileRoutine(response, filePath));
                }
                else {
                    action = null;
                }

                // action?.Invoke();
                _actionQueue.Enqueue(action);
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

    private void HandleCommand(HttpListenerRequest request) {
        using StreamReader reader = new(request.InputStream, request.ContentEncoding);
        string content = reader.ReadToEnd();
        Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);

        Console.ExecuteCommand(data["command"]);
    }

    private static IEnumerator HandleServeFileRoutine(HttpListenerResponse response, string filePath) {
        string mimeType = GetMimeType(filePath);

        using UnityWebRequest webRequest = UnityWebRequest.Get(filePath);
        yield return webRequest.SendWebRequest();

        if (webRequest.result != UnityWebRequest.Result.Success) {
            Debug.LogError($"[Console] [WebView] Error loading file: {webRequest.error}");

            response.StatusCode = 404;
            response.StatusDescription = "File not found.";
            response.Close();
        }
        else {
            byte[] fileBytes = webRequest.downloadHandler.data;

            response.ContentType = mimeType;
            response.ContentLength64 = fileBytes.Length;
            Stream output = response.OutputStream;
            output.Write(fileBytes, 0, fileBytes.Length);
            output.Close();
        }
    }

    private static void HandleLogs(HttpListenerRequest request, HttpListenerResponse response) {
        System.Collections.Specialized.NameValueCollection parameters = request.QueryString;
        bool gotTimeStamp = DateTime.TryParse(parameters.Get("timeStamp"), out DateTime dateTime);

        // create response
        List<ConsoleLog> logs = Console.GetLogsSince(gotTimeStamp ? dateTime : DateTime.MinValue);
        LogResponse outputObj = new() {
            logs = logs.ToArray(),
        };
        string outStr = JsonConvert.SerializeObject(outputObj, new ColorJsonConverter());

        byte[] buffer = Encoding.UTF8.GetBytes(outStr);
        response.ContentLength64 = buffer.Length;

        Stream output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
        output.Close();
    }

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