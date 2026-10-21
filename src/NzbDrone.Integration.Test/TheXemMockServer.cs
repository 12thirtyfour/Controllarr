using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NzbDrone.Integration.Test
{
    public sealed class TheXemMockServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts;

        public string Url { get; }

        public TheXemMockServer()
        {
            _listener = new HttpListener();
            var port = GetAvailablePort();
            Url = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(Url);
            _listener.Start();

            _cts = new CancellationTokenSource();
            Task.Run(ListenLoop);
        }

        private static int GetAvailablePort()
        {
            using var socket = new TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            return ((IPEndPoint)socket.LocalEndpoint).Port;
        }

        private async Task ListenLoop()
        {
            while (!_cts.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync().ConfigureAwait(false);
                    var response = context.Response;

                    var responseString = "{\"result\":\"success\",\"data\":[]}";
                    if (context.Request.Url.AbsolutePath.EndsWith("havemap"))
                    {
                        responseString = "{\"result\":\"success\",\"data\":[\"266189\",\"73065\"]}";
                    }
                    else if (context.Request.Url.AbsolutePath.EndsWith("allNames"))
                    {
                        responseString = "{\"result\":\"success\",\"data\":{}}";
                    }

                    var buffer = Encoding.UTF8.GetBytes(responseString);

                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    response.StatusCode = 200;

                    await response.OutputStream.WriteAsync(buffer, _cts.Token).ConfigureAwait(false);
                    response.OutputStream.Close();
                }
                catch (Exception) when (_cts.IsCancellationRequested || !_listener.IsListening)
                {
                    break;
                }
                catch
                {
                    // Ignore unhandled connection aborts in test harness
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            _listener.Close();
            _cts.Dispose();
        }
    }
}
