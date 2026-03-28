#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;

namespace BBBNexus
{
    [InitializeOnLoad]
    internal static class ConfigToolMainThread
    {
        private static readonly Queue<Action> Queue = new Queue<Action>();
        private static readonly object Gate = new object();

        static ConfigToolMainThread()
        {
            EditorApplication.update += Drain;
        }

        public static Task<T> Invoke<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();

            lock (Gate)
            {
                Queue.Enqueue(() =>
                {
                    try
                    {
                        tcs.SetResult(func());
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                });
            }

            return tcs.Task;
        }

        private static void Drain()
        {
            while (true)
            {
                Action action;
                lock (Gate)
                {
                    if (Queue.Count == 0)
                    {
                        return;
                    }

                    action = Queue.Dequeue();
                }

                action();
            }
        }
    }
}
#endif
