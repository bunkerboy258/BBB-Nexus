#if UNITY_EDITOR
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace BBBNexus
{
    [InitializeOnLoad]
    internal static class ConfigToolServer
    {
        private const string Prefix = "http://127.0.0.1:42718/";
        private static readonly object Gate = new object();

        private static HttpListener listener;
        private static Thread listenerThread;

        static ConfigToolServer()
        {
            EditorApplication.delayCall += EnsureStarted;
            EditorApplication.quitting += Stop;
        }

        [MenuItem("Tools/BBB-Nexus/ConfigTool/Start Server")]
        private static void StartMenu()
        {
            EnsureStarted();
        }

        [MenuItem("Tools/BBB-Nexus/ConfigTool/Stop Server")]
        private static void StopMenu()
        {
            Stop();
        }

        [MenuItem("Tools/BBB-Nexus/ConfigTool/Show Status")]
        private static void ShowStatusMenu()
        {
            Debug.Log(IsRunning
                ? $"ConfigTool server listening on {Prefix}"
                : "ConfigTool server is stopped.");
        }

        internal static bool IsRunning
        {
            get
            {
                lock (Gate)
                {
                    return listener != null && listener.IsListening;
                }
            }
        }

        internal static void EnsureStarted()
        {
            Exception startError = null;

            lock (Gate)
            {
                if (listener != null && listener.IsListening)
                {
                    return;
                }

                try
                {
                    listener = new HttpListener();
                    listener.Prefixes.Add(Prefix);
                    listener.Start();

                    listenerThread = new Thread(ListenLoop)
                    {
                        IsBackground = true,
                        Name = "ConfigToolHttpServer"
                    };
                    listenerThread.Start();

                    Debug.Log($"ConfigTool server listening on {Prefix}");
                }
                catch (Exception ex)
                {
                    startError = ex;
                }
            }

            if (startError != null)
            {
                Debug.LogError($"ConfigTool server failed to start: {startError}");
                Stop();
            }
        }

        internal static void Stop()
        {
            lock (Gate)
            {
                try
                {
                    listener?.Stop();
                    listener?.Close();
                }
                catch
                {
                }

                listener = null;

                if (listenerThread != null && listenerThread.IsAlive)
                {
                    try
                    {
                        listenerThread.Join(500);
                    }
                    catch
                    {
                    }
                }

                listenerThread = null;
            }
        }

        private static void ListenLoop()
        {
            while (true)
            {
                HttpListenerContext context;
                try
                {
                    var active = listener;
                    if (active == null || !active.IsListening)
                    {
                        return;
                    }

                    context = active.GetContext();
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                ThreadPool.QueueUserWorkItem(_ => HandleContext(context));
            }
        }

        private static void HandleContext(HttpListenerContext context)
        {
            try
            {
                var response = Dispatch(context.Request);
                WriteJson(context.Response, 200, JsonUtility.ToJson(response));
            }
            catch (Exception ex)
            {
                WriteJson(context.Response, 500, JsonUtility.ToJson(ConfigToolEnvelope.Fail(ex.Message)));
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        private static ConfigToolEnvelope Dispatch(HttpListenerRequest request)
        {
            var path = request.Url == null ? "" : request.Url.AbsolutePath;
            switch (path)
            {
                case "/health":
                    return ConfigToolEnvelope.Ok(JsonUtility.ToJson(new HealthResponse
                    {
                        status = "ok",
                        projectPath = Directory.GetParent(Application.dataPath)?.FullName ?? "",
                        unityVersion = Application.unityVersion
                    }));

                case "/assets/fields":
                    return RunBody(request, body =>
                    {
                        var dto = JsonUtility.FromJson<FieldsRequest>(body);
                        return JsonUtility.ToJson(ConfigToolAssetService.GetFields(dto.path));
                    });

                case "/assets/set":
                    return RunBody(request, body =>
                    {
                        var dto = JsonUtility.FromJson<SetFieldRequest>(body);
                        return JsonUtility.ToJson(ConfigToolAssetService.SetField(dto.path, dto.field, dto.value));
                    });

                case "/assets/set-ref":
                    return RunBody(request, body =>
                    {
                        var dto = JsonUtility.FromJson<SetReferenceRequest>(body);
                        return JsonUtility.ToJson(ConfigToolAssetService.SetReference(dto.path, dto.field, dto.assetName));
                    });

                case "/clips/find":
                    return RunBody(request, body =>
                    {
                        var dto = JsonUtility.FromJson<FindClipRequest>(body);
                        return JsonUtility.ToJson(ConfigToolAssetService.FindClips(dto.query));
                    });

                case "/so-types/list":
                    return RunBody(request, _ =>
                    {
                        return JsonUtility.ToJson(ConfigToolAssetService.ListScriptableObjectTypes());
                    });

                case "/so/create":
                    return RunBody(request, body =>
                    {
                        var dto = JsonUtility.FromJson<CreateScriptableObjectRequest>(body);
                        return JsonUtility.ToJson(ConfigToolAssetService.CreateScriptableObject(dto.type, dto.path));
                    });

                default:
                    throw new InvalidOperationException($"Unknown route: {path}");
            }
        }

        private static ConfigToolEnvelope RunBody(HttpListenerRequest request, Func<string, string> handler)
        {
            if (request.HttpMethod != "POST")
            {
                throw new InvalidOperationException($"Route requires POST: {request.Url?.AbsolutePath}");
            }

            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            var task = ConfigToolMainThread.Invoke(() =>
            {
                EnsureEditorReady();
                return handler(body);
            });
            try
            {
                if (!task.Wait(TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException("ConfigTool request timed out on Unity main thread.");
                }
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException ?? ex;
            }

            return ConfigToolEnvelope.Ok(task.Result);
        }

        private static void EnsureEditorReady()
        {
            if (EditorApplication.isCompiling)
            {
                throw new InvalidOperationException("Unity Editor is compiling scripts. Retry after compilation finishes.");
            }

            if (EditorApplication.isUpdating)
            {
                throw new InvalidOperationException("Unity Editor is updating assets. Retry after it becomes idle.");
            }
        }

        private static void WriteJson(HttpListenerResponse response, int statusCode, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json ?? "");
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
        }
    }
}
#endif
