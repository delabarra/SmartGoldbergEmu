using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using SmartGoldbergEmu;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Extensions;
using SmartGoldbergEmu.Services;

namespace SteamKit
{

/// <summary>
/// Steam client implementation for anonymous connections without external packages.
/// Based on SteamKit2 architecture.
/// </summary>
public class SteamClient
{
    private const uint TcpMagic = 0x31305456; // 'VT01' in little-endian framing
    /// <summary>Public universe + AnonUser account type (64-bit SteamID layout).</summary>
    private const ulong AnonymousSteamId = (1ul << 56) | (10ul << 52);

    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnConnectionFailed;
    public event Action<uint> OnLoggedOn;

    private TcpClient _tcpClient;
    private NetworkStream _networkStream;
    private bool _isConnected;
    private bool _channelEncrypted;
    private CancellationTokenSource _cancellationTokenSource;
    private NetFilterEncryptionWithHMAC _encryption;
    private TaskCompletionSource<bool> _handshakeCompletion;

    // Known Steam CM servers (Content Machine)
    private static readonly string[] SteamServers = new[]
    {
        "162.125.18.133:27015",  // US
        "162.125.18.1:27015",    // US
        "162.125.19.1:27015",    // US
        "205.185.116.151:27015", // EU
    };

    private const int CmDirectoryHttpTimeoutSeconds = 8;

    public bool IsConnected => _isConnected;

    public void Connect()
    {
        if (_isConnected)
            return;

        _cancellationTokenSource = new CancellationTokenSource();
        _ = ConnectAsync(_cancellationTokenSource.Token).ForgetFaults(Program.LogService, "SteamClient.ConnectAsync");
    }

    public void Disconnect()
    {
        _isConnected = false;
        _channelEncrypted = false;
        _encryption = null;
        _cancellationTokenSource?.Cancel();

        _handshakeCompletion?.TrySetResult(false);
        CloseSocket();

        OnDisconnected?.Invoke();
    }

    private void CloseSocket()
    {
        try
        {
            _networkStream?.Close();
            _tcpClient?.Close();
        }
        catch
        {
        }
    }

    private static string FormatConnectFailureUserMessage(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SocketException se && se.SocketErrorCode == SocketError.TimedOut)
                return "Connection to Steam timed out. Check your internet connection.";
            if (e is HttpRequestException)
                return "Could not reach Steam (server list). Check your internet connection.";
        }
        return "Could not connect to Steam. Check your internet connection.";
    }

    public void LogOnAnonymous()
    {
        if (!_isConnected)
        {
            Console.WriteLine("Not connected!");
            return;
        }

        try
        {
            var logon = new ClientMsgProtobuf(EMsg.ClientLogon);
            logon.ProtoHeader.client_sessionid = 0;
            logon.ProtoHeader.steamid = AnonymousSteamId;

            // Set required fields
            logon.Body.protocol_version = 65581; // MsgClientLogon.CurrentProtocol
            logon.Body.client_os_type = (uint)GetOSType();
            logon.Body.client_language = "english";
            logon.Body.cell_id = 0;
            logon.Body.machine_id = GetMachineID();

            // Send logon message
            Send(logon);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LogOn error: {ex.Message}");
        }
    }

    public void Send(ClientMsgProtobuf msg)
    {
        if (!_isConnected || _networkStream == null)
            return;

        try
        {
            var payload = msg.Serialize();
            if (_channelEncrypted && _encryption != null)
            {
                var encryptedBuffer = new byte[_encryption.CalculateMaxEncryptedDataLength(payload.Length)];
                int encryptedLength = _encryption.ProcessOutgoing(payload, encryptedBuffer);
                payload = new byte[encryptedLength];
                Buffer.BlockCopy(encryptedBuffer, 0, payload, 0, encryptedLength);
            }

            var packet = new byte[payload.Length + 8];

            // Steam CM over TCP expects: [4-byte little-endian length][4-byte VT01 magic][payload bytes]
            BitConverter.GetBytes(payload.Length).CopyTo(packet, 0);
            BitConverter.GetBytes(TcpMagic).CopyTo(packet, 4);
            Buffer.BlockCopy(payload, 0, packet, 8, payload.Length);

            _networkStream.Write(packet, 0, packet.Length);
            _networkStream.Flush();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Send error: {ex.Message}");
            Disconnect();
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        Exception lastError = null;

        try
        {
            var serverCandidates = await GetServerCandidatesAsync(ct);

            // Try to connect to a Steam CM server
            foreach (var serverAddr in serverCandidates)
            {
                try
                {
                    var parts = serverAddr.Split(':');
                    if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
                        continue;

                    var host = parts[0];

                    _tcpClient = new TcpClient();

                    // Bound each connect attempt to avoid hanging indefinitely.
                    var connectTask = _tcpClient.ConnectAsync(host, port);
                    var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(5), ct));

                    if (completed != connectTask)
                    {
                        _tcpClient.Dispose();
                        continue;
                    }

                    // Propagate socket exceptions from connectTask.
                    await connectTask;

                    _networkStream = _tcpClient.GetStream();
                    _isConnected = true;
                    _channelEncrypted = false;
                    _handshakeCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                    // Start receiving messages
                    _ = ReceiveLoopAsync(ct).ForgetFaults(Program.LogService, "SteamClient.ReceiveLoopAsync");

                    var handshakeTask = _handshakeCompletion.Task;
                    var handshakeCompleted = await Task.WhenAny(handshakeTask, Task.Delay(TimeSpan.FromSeconds(4), ct));
                    if (handshakeCompleted == handshakeTask && handshakeTask.Result)
                    {
                        return;
                    }

                    _isConnected = false;
                    _channelEncrypted = false;
                    _encryption = null;
                    CloseSocket();
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _isConnected = false;
                    _channelEncrypted = false;
                    _encryption = null;
                    CloseSocket();
                    // Try next server
                }
            }

            _isConnected = false;
            var reason = lastError != null
                ? FormatConnectFailureUserMessage(lastError)
                : "Could not connect to any Steam server. Check your internet connection.";
            OnConnectionFailed?.Invoke(reason);
            OnDisconnected?.Invoke();
        }
        catch (Exception ex)
        {
            _isConnected = false;
            OnConnectionFailed?.Invoke(FormatConnectFailureUserMessage(ex));
            OnDisconnected?.Invoke();
        }
    }

    private async Task<List<string>> GetServerCandidatesAsync(CancellationToken ct)
    {
        var serverCandidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var discoveredServers = await FetchCmDirectoryServersAsync(ct);
            if (discoveredServers.Count > 0)
            {
                foreach (var server in discoveredServers)
                {
                    if (seen.Add(server))
                    {
                        serverCandidates.Add(server);
                    }
                }
            }
        }
        catch (Exception)
        {
            // Fall back to built-in CM list silently.
        }

        // IP endpoints tend to be more reliable for direct TCP in this minimal client.
        serverCandidates.Sort((a, b) => IsLikelyIpEndpoint(b).CompareTo(IsLikelyIpEndpoint(a)));

        foreach (var fallback in SteamServers)
        {
            if (seen.Add(fallback))
            {
                serverCandidates.Add(fallback);
            }
        }

        return serverCandidates;
    }

    private static bool IsLikelyIpEndpoint(string endpoint)
    {
        int colonIndex = endpoint.LastIndexOf(':');
        string host = colonIndex > 0 ? endpoint.Substring(0, colonIndex) : endpoint;
        return IPAddress.TryParse(host, out _);
    }

    private static async Task<List<string>> FetchCmDirectoryServersAsync(CancellationToken ct)
    {
        using (var http = HttpServiceFactory.Create(TimeSpan.FromSeconds(CmDirectoryHttpTimeoutSeconds)))
        {
            using (var response = await http.GetAsync(ApplicationConstants.SteamDirectoryGetCmListForConnectUrl, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (ct.IsCancellationRequested)
                    ct.ThrowIfCancellationRequested();

                return ParseServerListFromDirectoryResponse(body);
            }
        }
    }

    private static List<string> ParseServerListFromDirectoryResponse(string response)
    {
        var result = new List<string>();
        var section = Regex.Match(response, "\"serverlist\"\\s*:\\s*\\[(?<items>.*?)\\]", RegexOptions.Singleline);

        if (!section.Success)
            return result;

        var items = section.Groups["items"].Value;
        var serverMatches = Regex.Matches(items, "\"(?<server>[^\"]+)\"");

        foreach (Match match in serverMatches)
        {
            var server = match.Groups["server"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(server))
            {
                result.Add(server);
            }
        }

        return result;
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var readBuffer = new byte[4096];
        var accumulated = new List<byte>(8192);

        try
        {
            while (_isConnected && _networkStream != null && !ct.IsCancellationRequested)
            {
                int read = await _networkStream.ReadAsync(readBuffer, 0, readBuffer.Length, ct);

                if (read == 0)
                {
                    if (!_isConnected)
                        return;

                    Disconnect();
                    return;
                }

                for (int i = 0; i < read; i++)
                {
                    accumulated.Add(readBuffer[i]);
                }

                ProcessAccumulatedPackets(accumulated);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disconnected
            return;
        }
        catch (Exception ex)
        {
            if (!_isConnected || ct.IsCancellationRequested)
                return;

            Console.WriteLine($"ReceiveLoop error: {ex.Message}");
            Disconnect();
        }
    }

    private void ProcessAccumulatedPackets(List<byte> accumulated)
    {
        // Parse as many complete packets as available.
        while (accumulated.Count >= 8)
        {
            int packetLength = BitConverter.ToInt32(accumulated.ToArray(), 0);

            if (packetLength <= 0 || packetLength > 1024 * 1024)
            {
                Console.WriteLine($"Invalid packet length from server: {packetLength}");
                Disconnect();
                return;
            }

            if (accumulated.Count < packetLength + 8)
            {
                // Wait for more data.
                return;
            }

            uint magic = BitConverter.ToUInt32(accumulated.ToArray(), 4);
            if (magic != TcpMagic)
            {
                Console.WriteLine($"Invalid TCP packet magic from server: 0x{magic:X8}");
                Disconnect();
                return;
            }

            var payload = new byte[packetLength];
            for (int i = 0; i < packetLength; i++)
                payload[i] = accumulated[i + 8];
            accumulated.RemoveRange(0, packetLength + 8);

            if (_channelEncrypted && _encryption != null)
            {
                try
                {
                    payload = _encryption.ProcessIncoming(payload);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Decrypt error: {ex.Message}");
                    Disconnect();
                    return;
                }
            }

            ProcessMessage(payload, (uint)payload.Length);
        }
    }

    private void ProcessMessage(byte[] data, uint length)
    {
        try
        {
            // Parse message header (simplified - real implementation would be more robust)
            if (length < 4)
                return;

            uint msgType = BitConverter.ToUInt32(data, 0);
            var eMsg = (EMsg)(msgType & 0x7FFFFFFF);

            if (!_channelEncrypted)
            {
                if (eMsg == EMsg.ChannelEncryptRequest)
                {
                    HandleChannelEncryptRequest(data);
                }
                else if (eMsg == EMsg.ChannelEncryptResult)
                {
                    HandleChannelEncryptResult(data);
                }

                return;
            }

            if (eMsg == EMsg.ClientLogOnResponse)
            {
                if (!ClientMsgProtobuf.TryParseLogOnResponse(data, out var response))
                {
                    Console.WriteLine("Failed to parse logon response payload.");
                    OnLoggedOn?.Invoke(SteamLogonWaitResult.LogonResponseParseFailed);
                    Disconnect();
                    return;
                }

                ulong headerSteamId = ClientMsgProtobuf.GetSteamIdFromHeader(data);
                if (headerSteamId != 0)
                    _loggedOnSteamID = headerSteamId;
                else if (response.ClientSuppliedSteamId != 0)
                    _loggedOnSteamID = response.ClientSuppliedSteamId;
                else
                    _loggedOnSteamID = AnonymousSteamId;

                _sessionId = (uint)ClientMsgProtobuf.GetSessionIdFromHeader(data);

                OnLoggedOn?.Invoke(response.EResult);
            }
            else if (eMsg == EMsg.Multi)
            {
                HandleMultiMessage(data);
            }
            else if (eMsg == EMsg.ClientPICSProductInfoResponse)
            {
                HandlePICSProductInfoResponse(data);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ProcessMessage error: {ex.Message}");
        }
    }

    private void HandleMultiMessage(byte[] packet)
    {
        // EMsg.Multi is protobuf-backed on modern CM connections.
        if (packet.Length < 8)
            return;

        int headerLength = BitConverter.ToInt32(packet, 4);
        int bodyOffset = 8 + headerLength;
        if (headerLength < 0 || bodyOffset > packet.Length)
            return;

        if (!TryParseCMsgMulti(packet, bodyOffset, packet.Length - bodyOffset, out var unzippedSize, out var messageBody))
            return;

        byte[] payload = messageBody;

        if (unzippedSize > 0)
        {
            using (var compressedStream = new MemoryStream(payload))
            using (var gzip = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var outStream = new MemoryStream((int)unzippedSize))
            {
                gzip.CopyTo(outStream);
                payload = outStream.ToArray();
            }
        }

        int index = 0;
        while (index + 4 <= payload.Length)
        {
            int innerLength = BitConverter.ToInt32(payload, index);
            index += 4;

            if (innerLength <= 0 || index + innerLength > payload.Length)
            {
                Console.WriteLine($"Invalid inner message length in Multi: {innerLength}");
                return;
            }

            byte[] innerPacket = new byte[innerLength];
            Buffer.BlockCopy(payload, index, innerPacket, 0, innerLength);
            index += innerLength;

            ProcessMessage(innerPacket, (uint)innerPacket.Length);
        }
    }

    private static bool TryParseCMsgMulti(byte[] body, int offset, int length, out uint sizeUnzipped, out byte[] messageBody)
    {
        sizeUnzipped = 0;
        messageBody = Array.Empty<byte>();

        int index = offset;
        int end = offset + length;
        while (index < end)
        {
            if (!TryReadVarUInt64(body, ref index, end, out ulong key))
                return false;

            int fieldNumber = (int)(key >> 3);
            int wireType = (int)(key & 0x07);

            if (fieldNumber == 1 && wireType == 0)
            {
                if (!TryReadVarUInt64(body, ref index, end, out ulong unzipped))
                    return false;
                sizeUnzipped = (uint)unzipped;
            }
            else if (fieldNumber == 2 && wireType == 2)
            {
                if (!TryReadVarUInt64(body, ref index, end, out ulong blobLength))
                    return false;
                if (blobLength > (ulong)(end - index))
                    return false;

                messageBody = new byte[(int)blobLength];
                Buffer.BlockCopy(body, index, messageBody, 0, (int)blobLength);
                index += (int)blobLength;
            }
            else
            {
                if (!SkipUnknownField(body, ref index, end, wireType))
                    return false;
            }
        }

        return messageBody.Length > 0;
    }

    private static bool SkipUnknownField(byte[] body, ref int index, int end, int wireType)
    {
        switch (wireType)
        {
            case 0:
                return TryReadVarUInt64(body, ref index, end, out _);

            case 1:
                if (index + 8 > end)
                    return false;
                index += 8;
                return true;

            case 2:
                if (!TryReadVarUInt64(body, ref index, end, out ulong length))
                    return false;
                if (length > (ulong)(end - index))
                    return false;
                index += (int)length;
                return true;

            case 5:
                if (index + 4 > end)
                    return false;
                index += 4;
                return true;

            default:
                return false;
        }
    }

    private static bool TryReadVarUInt64(byte[] buffer, ref int index, int end, out ulong value)
    {
        value = 0;
        int shift = 0;

        while (index < end && shift < 64)
        {
            byte b = buffer[index++];
            value |= (ulong)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return true;

            shift += 7;
        }

        return false;
    }

    private void HandleChannelEncryptRequest(byte[] packet)
    {
        if (packet.Length < 28)
            return;

        uint protocolVersion = BitConverter.ToUInt32(packet, 20);
        var universe = (EUniverse)BitConverter.ToInt32(packet, 24);
        if (protocolVersion != 1)
        {
            Console.WriteLine($"Unsupported channel encryption protocol version: {protocolVersion}");
            Disconnect();
            return;
        }

        byte[] randomChallenge = new byte[packet.Length - 28];
        Buffer.BlockCopy(packet, 28, randomChallenge, 0, randomChallenge.Length);
        if (randomChallenge.Length < 16)
        {
            Console.WriteLine("Invalid channel encryption request challenge length.");
            Disconnect();
            return;
        }

        byte[] publicKey = KeyDictionary.GetPublicKey(universe);
        if (publicKey == null)
        {
            Console.WriteLine($"No public key available for universe {universe}");
            Disconnect();
            return;
        }

        byte[] tempSessionKey = GenerateRandomBytes(32);
        byte[] encryptedHandshakeBlob;

        byte[] blobToEncrypt = new byte[tempSessionKey.Length + randomChallenge.Length];
        Array.Copy(tempSessionKey, blobToEncrypt, tempSessionKey.Length);
        Array.Copy(randomChallenge, 0, blobToEncrypt, tempSessionKey.Length, randomChallenge.Length);
        encryptedHandshakeBlob = RsaEncryptOaepSha1(publicKey, blobToEncrypt);

        _encryption = new NetFilterEncryptionWithHMAC(tempSessionKey);

        byte[] keyCrc = Crc32Util.Hash(encryptedHandshakeBlob);
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write((uint)EMsg.ChannelEncryptResponse);
            bw.Write(0UL); // target job id
            bw.Write(0UL); // source job id
            bw.Write(1u); // protocol version
            bw.Write(128u); // key size
            bw.Write(encryptedHandshakeBlob);
            bw.Write(keyCrc);
            bw.Write(0u); // trailer

            SendRawPayload(ms.ToArray());
        }
    }

    private void HandleChannelEncryptResult(byte[] packet)
    {
        if (packet.Length < 24)
            return;

        int result = BitConverter.ToInt32(packet, 20);
        if (result == 1 && _encryption != null)
        {
            _channelEncrypted = true;
            SendClientHello();
            _handshakeCompletion?.TrySetResult(true);
            OnConnected?.Invoke();
            return;
        }

        Console.WriteLine($"Channel encryption failed with result {result}.");
        _handshakeCompletion?.TrySetResult(false);
        Disconnect();
    }

    private void SendRawPayload(byte[] payload)
    {
        if (!_isConnected || _networkStream == null)
            return;

        var packet = new byte[payload.Length + 8];
        BitConverter.GetBytes(payload.Length).CopyTo(packet, 0);
        BitConverter.GetBytes(TcpMagic).CopyTo(packet, 4);
        Buffer.BlockCopy(payload, 0, packet, 8, payload.Length);

        _networkStream.Write(packet, 0, packet.Length);
        _networkStream.Flush();
    }

    private void SendClientHello()
    {
        if (!_isConnected)
            return;

        var hello = new ClientMsgProtobuf(EMsg.ClientHello);
        hello.Body.protocol_version = 65581;
        Send(hello);
    }

    // ── PICS product info ────────────────────────────

    private uint _sessionId;
    private ulong _loggedOnSteamID;
    public ulong LoggedOnSteamId => _loggedOnSteamID;
    private TaskCompletionSource<PICSProductInfoResult> _pendingPICS;
    private PICSProductInfoResult _pendingPICSResult;
    private ulong _nextJobId = 1;

    public async Task<PICSProductInfoResult> RequestProductInfo(uint appId = 0, uint packageId = 0, CancellationToken cancellationToken = default(CancellationToken))
    {
        _pendingPICS = new TaskCompletionSource<PICSProductInfoResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingPICSResult = new PICSProductInfoResult();

        var msg = new ClientMsgProtobuf(EMsg.ClientPICSProductInfoRequest);
        msg.ProtoHeader.steamid = _loggedOnSteamID;
        msg.ProtoHeader.client_sessionid = _sessionId;
        msg.ProtoHeader.JobIDSource = _nextJobId++;
        if (appId > 0)     msg.Body.PICSAppIds.Add(appId);
        if (packageId > 0) msg.Body.PICSPackageIds.Add(packageId);
        msg.Body.PICSSingleResponse = true;
        Send(msg);

        try
        {
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            var completed = await Task.WhenAny(_pendingPICS.Task, timeoutTask).ConfigureAwait(false);
            if (completed != _pendingPICS.Task)
                return null;
            return await _pendingPICS.Task.ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
        finally
        {
            _pendingPICSResult = null;
            _pendingPICS = null;
        }
    }

    private void HandlePICSProductInfoResponse(byte[] packet)
    {
        var body   = ClientMsgProtobuf.GetMessageBody(packet);
        var result = ClientMsgProtobuf.ParsePICSProductInfoResponse(body);
        if (_pendingPICSResult == null)
        {
            _pendingPICS?.TrySetResult(result);
            return;
        }

        _pendingPICSResult.Apps.AddRange(result.Apps);
        _pendingPICSResult.Packages.AddRange(result.Packages);
        _pendingPICSResult.UnknownAppIds.AddRange(result.UnknownAppIds);
        _pendingPICSResult.UnknownPackageIds.AddRange(result.UnknownPackageIds);
        _pendingPICSResult.ResponsePending = result.ResponsePending;

        if (!result.ResponsePending)
        {
            _pendingPICS?.TrySetResult(_pendingPICSResult);
        }
    }

    private static uint GetOSType()
    {
         return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 0x0391u :
             RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? 0x90u :
             RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x0304u : 0u;
    }

    private static ulong GetMachineID()
    {
        // Generate a unique machine ID based on environment
        using (var sha256 = SHA256.Create())
        {
            var machineGuid = sha256.ComputeHash(
                System.Text.Encoding.UTF8.GetBytes(Environment.MachineName)
            );
            return BitConverter.ToUInt64(machineGuid, 0);
        }
    }

    private static byte[] GenerateRandomBytes(int length)
    {
        var bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return bytes;
    }

    private static byte[] RsaEncryptOaepSha1(byte[] subjectPublicKeyInfo, byte[] data)
    {
        var publicKey = ReadRsaPublicKeyFromSubjectPublicKeyInfo(subjectPublicKeyInfo);

        using (var rsa = new RSACryptoServiceProvider())
        {
            rsa.PersistKeyInCsp = false;
            rsa.ImportParameters(publicKey);
            return rsa.Encrypt(data, true);
        }
    }

    private static RSAParameters ReadRsaPublicKeyFromSubjectPublicKeyInfo(byte[] spki)
    {
        if (spki == null || spki.Length == 0)
            throw new CryptographicException("Invalid RSA public key.");

        int index = 0;
        ReadAsn1Sequence(spki, ref index, out _);

        ReadAsn1Sequence(spki, ref index, out int algorithmLength);
        index += algorithmLength;

        ReadAsn1BitString(spki, ref index, out int bitStringLength);
        if (bitStringLength < 1 || index >= spki.Length)
            throw new CryptographicException("Invalid RSA public key bit string.");

        byte unusedBits = spki[index++];
        if (unusedBits != 0)
            throw new CryptographicException("Unsupported RSA public key format.");

        ReadAsn1Sequence(spki, ref index, out _);
        var modulus = ReadAsn1Integer(spki, ref index);
        var exponent = ReadAsn1Integer(spki, ref index);

        return new RSAParameters
        {
            Modulus = modulus,
            Exponent = exponent
        };
    }

    private static void ReadAsn1Sequence(byte[] data, ref int index, out int length)
    {
        ReadAsn1Tag(data, ref index, 0x30);
        length = ReadAsn1Length(data, ref index);
    }

    private static void ReadAsn1BitString(byte[] data, ref int index, out int length)
    {
        ReadAsn1Tag(data, ref index, 0x03);
        length = ReadAsn1Length(data, ref index);
    }

    private static byte[] ReadAsn1Integer(byte[] data, ref int index)
    {
        ReadAsn1Tag(data, ref index, 0x02);
        int length = ReadAsn1Length(data, ref index);
        if (length <= 0 || index + length > data.Length)
            throw new CryptographicException("Invalid RSA integer.");

        int start = index;
        index += length;

        while (length > 1 && data[start] == 0)
        {
            start++;
            length--;
        }

        var value = new byte[length];
        Buffer.BlockCopy(data, start, value, 0, length);
        return value;
    }

    private static void ReadAsn1Tag(byte[] data, ref int index, byte expectedTag)
    {
        if (index >= data.Length || data[index] != expectedTag)
            throw new CryptographicException("Unexpected ASN.1 tag.");
        index++;
    }

    private static int ReadAsn1Length(byte[] data, ref int index)
    {
        if (index >= data.Length)
            throw new CryptographicException("Invalid ASN.1 length.");

        int first = data[index++];
        if ((first & 0x80) == 0)
            return first;

        int byteCount = first & 0x7F;
        if (byteCount <= 0 || byteCount > 4 || index + byteCount > data.Length)
            throw new CryptographicException("Invalid ASN.1 length bytes.");

        int length = 0;
        for (int i = 0; i < byteCount; i++)
        {
            length = (length << 8) | data[index++];
        }

        return length;
    }
}
}
