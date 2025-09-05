using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DispCtrl
{
    public static class WiFiSender
    {
        private const int Port = 5000;
        private static readonly List<NetworkStream> _streams = new();
        private static readonly List<TcpClient> _clients = new();
        private static readonly SemaphoreSlim _lock = new(1, 1);

        private static bool _reconnectInProgress;
        private static int _retries;

        private static Action? _onDisconnected;
        private static Timer? _monitorTimer;

        public static bool IsConnected => _streams.Count > 0;

        public static async Task<bool> ConnectAllAsync()
        {
            if (_clients.Any(c => c != null && c.Connected))
            {
                Disconnect();
                Console.WriteLine("[WF] 🔄 기존 연결 정리 후 재시도");
            }

            bool connectedA = false;
            bool connectedB = false;

            static bool HasValidIPv4()
            {
                var validIps = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ip => ip.Address.ToString())
                    .Where(ip => ip.StartsWith("192.168.4."))
                    .ToList();

                Console.WriteLine($"[WF] 사용 중인 IPv4 주소들: {string.Join(", ", validIps)}");
                return validIps.Any();
            }

            static async Task<bool> TryConnect(string ip, bool checkHandshake = false)
            {
                var client = new TcpClient();
                try
                {
                    var connectTask = client.ConnectAsync(ip, Port);
                    var timeoutTask = Task.Delay(1000);
                    var completed = await Task.WhenAny(connectTask, timeoutTask);

                    if (completed != connectTask || !client.Connected)
                    {
                        Console.WriteLine($"[WF] ❌ 연결 타임아웃: {ip}");
                        client.Dispose();
                        return false;
                    }

                    var stream = client.GetStream();

                    // Ping 테스트
                    if (!await IsHostAlive(ip))
                    {
                        Console.WriteLine($"[WF] ❌ Ping 응답 없음 (연결 직후): {ip}");
                        client.Dispose();
                        return false;
                    }

                    // 핸드셰이크 (MAC 요청)
                    if (checkHandshake)
                    {
                        try
                        {
                            var message = Encoding.ASCII.GetBytes("![00830!]");
                            await stream.WriteAsync(message);
                            await stream.FlushAsync();

                            var buffer = new byte[128];
                            var cts = new CancellationTokenSource(2000);

                            var readTask = stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token).AsTask();
                            var completedRead = await Task.WhenAny(readTask, Task.Delay(1100));

                            if (completedRead != readTask)
                            {
                                Console.WriteLine($"[WF] ❌ 핸드셰이크 응답 시간 초과: {ip}");
                                client.Dispose();
                                return false;
                            }

                            var read = await readTask;
                            var response = Encoding.ASCII.GetString(buffer, 0, read);
                            Console.WriteLine($"[WF] 📡 핸드셰이크 응답: {response}");

                            if (!response.StartsWith("![0083") || !response.EndsWith("!]"))
                            {
                                Console.WriteLine($"[WF] ❌ 잘못된 핸드셰이크 응답 형식: {response}");
                                client.Dispose();
                                return false;
                            }

                            var mac = response.Substring(7, response.Length - 8);
                            Console.WriteLine($"[WF] ✔ MAC 주소 수신: {mac}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WF] ❌ 핸드셰이크 응답 오류 ({ip}): {ex.Message}");
                            client.Dispose();
                            return false;
                        }
                    }

                    // 성공한 연결만 리스트에 추가
                    _clients.Add(client);
                    _streams.Add(stream);
                    Console.WriteLine($"[WF] ✔ 연결 성공: {ip}");
                    return true;
                }
                catch (SocketException sockEx)
                {
                    Console.WriteLine($"[WF] ❌ 소켓 예외: {ip} - {sockEx.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WF] ❌ 연결 예외: {ip} - {ex.Message}");
                }

                client.Dispose();
                return false;
            }

            // IP 할당 대기
            int waitedMs = 0;
            while (!HasValidIPv4() && waitedMs < 5000)
            {
                await Task.Delay(250);
                waitedMs += 250;
            }

            if (!HasValidIPv4())
            {
                Console.WriteLine("[WF] ❌ 유효한 192.168.4.x IP를 얻지 못함. 연결 중단");
                return false;
            }

            // A 연결
            connectedA = false;

            for (int attempt = 0; attempt < 5 && !connectedA; attempt++)
            {
                if (attempt == 0)
                {
                    Console.WriteLine("[WF] ⏳ A 컨트롤러 접속 전 초기 대기 중 (500ms)");
                    await Task.Delay(500);
                }
                else
                {
                    Console.WriteLine("[WF] ⏱️ A 컨트롤러 재시도 중...");
                    await Task.Delay(100);
                }

                connectedA = await TryConnect("192.168.4.1");
            }

            // B 연결 시도 (1회 재시도 포함)
            if (connectedA)
            {
                for (int i = 2; i <= 10; i++)
                {
                    string ip = $"192.168.4.{i}";
                    if (await TryConnect(ip, checkHandshake: true))
                    {
                        connectedB = true;
                        break;
                    }
                    else
                    {
                        // ⏱️ 1회 재시도
                        Console.WriteLine($"[WF] ⏱️ B 재시도 중: {ip}");
                        await Task.Delay(100);
                        if (await TryConnect(ip, checkHandshake: true))
                        {
                            connectedB = true;
                            break;
                        }
                    }
                }
            }

            if (!connectedA)
                Console.WriteLine("[WF] ❌ A 컨트롤러(192.168.4.1) 연결 실패");

            if (!connectedB)
                Console.WriteLine("[WF] ⚠️ B 컨트롤러(192.168.4.2~10) 연결 실패");

            return connectedA;
        }

        public static async Task TryReconnectAsync(CancellationToken? ct = null)
        {
            if (_reconnectInProgress) return;
            _reconnectInProgress = true;

            try
            {
                var delayMs = Math.Min(60_000, (int)Math.Pow(2, _retries) * 1000); // 1s→2s→4s… max 60s
                await Task.Delay(delayMs, ct ?? CancellationToken.None);

                var ok = await ConnectAllAsync();
                _retries = ok ? 0 : Math.Min(_retries + 1, 6);
                if (ok) Console.WriteLine("[WF] 🔁 재연결 성공");
                else Console.WriteLine("[WF] 🔁 재연결 실패(백오프 유지)");
            }
            catch { /* 무시 */ }
            finally { _reconnectInProgress = false; }
        }

        public static bool HasStream(int index)
        {
            return IsValidIndex(index) && _streams[index].CanWrite;
        }

        private static async Task<bool> IsHostAlive(string ip)
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync(ip, 500);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        public static Task DisconnectAsync()
        {
            Disconnect();
            return Task.CompletedTask;
        }

        public static void Disconnect()
        {
            foreach (var stream in _streams)
            {
                try { stream.Dispose(); } catch { }
            }

            foreach (var client in _clients)
            {
                try { client.Client?.Shutdown(SocketShutdown.Both); } catch { }
                try { client.Close(); } catch { }
                try { client.Dispose(); } catch { }
            }

            _streams.Clear();
            _clients.Clear();
            Console.WriteLine("[WF] 모든 Wi-Fi 연결 해제");
        }

        public static void StartConnectionMonitor(Action? onDisconnected)
        {
            _onDisconnected = onDisconnected;

            _monitorTimer?.Dispose();
            _monitorTimer = new Timer(_ =>
            {
                bool anyConnected = _clients.Any(c => IsStillConnected(c));

                Console.WriteLine($"[WF] 🔍 연결 감시 결과: {anyConnected}");

                if (!anyConnected)
                {
                    Console.WriteLine("[WF] ⚠️ 연결 끊김 감지됨");
                    StopConnectionMonitor();
                    Disconnect();
                    _onDisconnected?.Invoke(); // 여기서 UpdateConnectionStatus(false) 호출됨
                }
            }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public static bool IsStillConnected(TcpClient client)
        {
            try
            {
                if (client?.Client == null || !client.Client.Connected)
                    return false;

                // 클라이언트 소켓이 닫혔는지 확인 (non-blocking 방식)
                return !(client.Client.Poll(1, SelectMode.SelectRead) && client.Client.Available == 0);
            }
            catch
            {
                return false;
            }
        }

        public static void StopConnectionMonitor()
        {
            _monitorTimer?.Dispose();
            _monitorTimer = null;
        }

        public static async Task<bool> SendCommandToHostAsync(int index, string message)
        {
            // === 가드 추가 시작 ===
            if (!IsConnected || !HasStream(index))
            {
                _ = TryReconnectAsync(); // 백그라운드 재연결 시도
                return false;            // 이번 전송은 스킵
            }
            // === 가드 추가 끝 ===

            await _lock.WaitAsync();
            try
            {
                if (!IsValidIndex(index))
                {
                    Console.WriteLine($"[WF] ❌ 잘못된 인덱스 또는 연결 없음: {index}");
                    return false;
                }

                string packet = $"![00{message}!]";

                var sendBuf = Encoding.UTF8.GetBytes(packet);
                await _streams[index].WriteAsync(sendBuf);
                Console.WriteLine($"[WF] ▶ 전송 to {_clients[index].Client.RemoteEndPoint}: {packet}");
                return true;
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"[WF] ❌ I/O 오류 from {_clients[index].Client.RemoteEndPoint}: {ioEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WF] ❌ 전송 예외 from {_clients[index].Client.RemoteEndPoint}: {ex.Message}");
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task<string?> SendAndReceiveAsync(int index, string message, int readTimeoutMs = 2000)
        {
            // === 가드 추가 시작 ===
            if (!IsConnected || !HasStream(index))
            {
                _ = TryReconnectAsync();
                return null;
            }
            // === 가드 추가 끝 ===

            await _lock.WaitAsync();
            try
            {
                if (!IsValidIndex(index))
                {
                    Console.WriteLine($"[WF] ❌ 잘못된 인덱스 또는 연결 없음: {index}");
                    return null;
                }

                string packet = $"![00{message}!]";
                var sendBuf = Encoding.UTF8.GetBytes(packet);

                await _streams[index].WriteAsync(sendBuf.AsMemory());
                Console.WriteLine($"[WF] ▶ 보내기 to {_clients[index].Client.RemoteEndPoint}: {packet}");

                var respBuf = new byte[1024];
                var readTask = _streams[index].ReadAsync(respBuf, 0, respBuf.Length);
                var timeoutTask = Task.Delay(readTimeoutMs);
                var completed = await Task.WhenAny(readTask, timeoutTask);

                if (completed == timeoutTask)
                {
                    Console.WriteLine($"[WF] ❌ 타임아웃({readTimeoutMs}ms) on reading from {_clients[index].Client.RemoteEndPoint}");
                    return null;
                }

                int bytesRead = await readTask;
                if (bytesRead > 0)
                {
                    string resp = Encoding.UTF8.GetString(respBuf, 0, bytesRead);
                    Console.WriteLine($"[WF] ✔ 응답 from {_clients[index].Client.RemoteEndPoint}: {resp}");
                    return resp;
                }
            }
            catch (IOException ioEx)
            {
                Console.WriteLine($"[WF] ❌ I/O 오류 from {_clients[index].Client.RemoteEndPoint}: {ioEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WF] ❌ 전송/수신 예외 from {_clients[index].Client.RemoteEndPoint}: {ex}");
            }
            finally
            {
                _lock.Release();
            }

            return null;
        }

        private static bool IsValidIndex(int index)
        {
            return index >= 0 &&
                   index < _clients.Count &&
                   index < _streams.Count &&
                   _clients[index] != null &&
                   _clients[index].Connected &&
                   _streams[index] != null;
        }
    }
}