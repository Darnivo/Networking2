using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Generic;
using shared;
using System.Threading;

class TCPServerSample
{
	/**
	 * This class implements a simple concurrent TCP Echo server.
	 * Read carefully through the comments below.
	 */
	class ClientInfos
    {
        public TcpClient Client { get; set; }
        public string Nickname { get; set; }
        public string CurrentRoom { get; set; } = "general"; // default room
        public NetworkStream Stream => Client.GetStream();
    }

	private static Dictionary<string, ClientInfos> _clients = new Dictionary<string, ClientInfos>();
	private static int _guestNumber = 0;

    public static void Main (string[] args)
	{
		Console.WriteLine("Server started on port 55555");

		TcpListener listener = new TcpListener (IPAddress.Any, 55555);
		listener.Start ();

		while (true)
		{
            try
            {
                ProcessNewClients(listener);
                ProcessExistingClients();
            }
            catch  { }
            try
            {
                CleanupFaultyClients();

            }
            catch { }

            //Although technically not required, now that we are no longer blocking, 
            //it is good to cut your CPU some slack
            Thread.Sleep(100);
		}
	}

    private static void ProcessNewClients(TcpListener listener)
    {
        while (listener.Pending())
        {
            TcpClient client = listener.AcceptTcpClient();
            string clientKey = client.Client.RemoteEndPoint.ToString();

            ClientInfos clientInfo = new ClientInfos{
                Client = client,
                Nickname = $"guest{_guestNumber++}"
            };
            _clients.Add(clientKey, clientInfo);

            SendToClient(clientInfo, $"You joined as {clientInfo.Nickname}!");
            Broadcast($"{clientInfo.Nickname} joined the chat", exclude: clientInfo);
        }
    }
    private static void ProcessExistingClients()
    {
        List<string> faultyClientKeys = new List<string>();

        foreach (var keyVal in _clients.ToList())
        {
            ClientInfos clientInfo = keyVal.Value;
            NetworkStream stream = clientInfo.Stream;
            if (stream.DataAvailable)
            {
                try
                {
                    byte[] inBytes = StreamUtil.Read(stream);
                    string inString = Encoding.UTF8.GetString(inBytes);

                    SendToClient(clientInfo, ">> " + inString);

                    if (inString.StartsWith("/setname ") || inString.StartsWith("/sn "))
                    {
                        HandleSetNameCommand(clientInfo, inString);
                    }
                    else if (inString.Equals("/list"))
                    {
                        HandleListCommand(clientInfo);
                    }
                    else if (inString.Equals("/help"))
                    {
                        HandleHelpCommand(clientInfo);
                    }
                    else if (inString.StartsWith("/whisper ") || inString.StartsWith("/w "))
                    {
                        HandleWhisperCommand(clientInfo, inString);
                    }

                    //room related commands
                    else if (inString.StartsWith("/join "))
                    {
                        HandleJoinRoomCommand(clientInfo, inString);
                    }
                    else if (inString.Equals("/listrooms"))
                    {
                        HandleListRoomsCommand(clientInfo);
                    }
                    else if (inString.Equals("/listroom"))
                    {
                        HandleListRoomCommand(clientInfo);
                    }
                    else
                    {
                        string formatted = $"[{DateTime.Now:HH:mm:ss}] {clientInfo.Nickname}: {inString}";
                        // add  {clientInfo.Client.Client.RemoteEndPo int} for port
                        Broadcast(formatted, clientInfo.CurrentRoom, exclude: clientInfo);
                        //Broadcast(formatted, clientInfo.CurrentRoom);
                    }

                    //string formatted = $"[{DateTime.Now:HH:mm:ss} - {client.Client.Client.RemoteEndPoint}] {client.Nickname}: {strrev(inString)}";
                    //Broadcast(formatted);
                }
                catch(Exception e)
                {
                    Console.WriteLine($"Error > {e.Message}");
                    faultyClientKeys.Add(keyVal.Key);
                }
            }
        }
        foreach (string key in faultyClientKeys)
        {
            _clients[key].Client.Close();
            _clients.Remove(key);
        }
    }

    private static void CleanupFaultyClients()
    {
        List<string> faultyClientKeys = new List<string>();
        foreach (var keyVal in _clients)
        {
            if (!IsClientConnected(keyVal.Value.Client))
            {
                faultyClientKeys.Add(keyVal.Key);
                Broadcast($"{keyVal.Value.Nickname} left the chat");
            }
        }

        foreach (string key in faultyClientKeys)
        {
            _clients[key].Client.Close();
            _clients.Remove(key);
        }
    }

    private static bool IsClientConnected(TcpClient client)
    {
        try
        {
            if (client.Client.Poll(0, SelectMode.SelectRead))
            {
                byte[] buff = new byte[1];
                if (client.Client.Receive(buff, SocketFlags.Peek) == 0)
                {
                    return false;
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Broadcast(string message, string room = null, ClientInfos exclude = null)
    {
        byte[] outBytes = Encoding.UTF8.GetBytes(message);
        foreach (ClientInfos client in _clients.Values)
        {
            if (client != exclude && (room == null || client.CurrentRoom == room))
            {
                try
                {
                    StreamUtil.Write(client.Stream, outBytes);
                }
                catch
                {
                    //disconnect code
                }
            }
        }
    }

    private static void SendToClient(ClientInfos client, string message)
    {
        byte[] outBytes = Encoding.UTF8.GetBytes(message);
        try
        {
            StreamUtil.Write(client.Stream, outBytes);
        }
        catch
        {

        }
    }

    public static string strrev(string str)
    {
		if (str == null) return null;

        char[] arr = str.ToCharArray();
        Array.Reverse(arr);
        return new string(arr);
    }

    //Change name func
    private static void HandleSetNameCommand(ClientInfos client, string command)
    {
        string newNick = command.Split(new[] { ' ' }, 2)[1].Trim().ToLower();   //remove command string & sanitize

        //name validation
        if (string.IsNullOrEmpty(newNick))
        {
            SendToClient(client, "Nickname cannot be empty.");
            return;
        }
        if (_clients.Values.Any(c => c.Nickname.ToLower() == newNick))
        {
            SendToClient(client, "Nickname already taken.");
        }
        else
        {   //change name
            string oldNick = client.Nickname;
            client.Nickname = newNick;
            Broadcast($"{oldNick} changed name to {newNick}", client.CurrentRoom);
            SendToClient(client, $"Your nickname is now {newNick}.");
        }
    }

    //list other clients func
    private static void HandleListCommand(ClientInfos client)
    {
        string names = "Connected users: " + string.Join(", ", _clients.Values.Select(c => c.Nickname));
        SendToClient(client, names);
    }

    //show commands list func
    private static void HandleHelpCommand(ClientInfos client)
    {
        string help = "Commands:\n" +
            "/setname [name] or /sn [name] - Change nickname\n" +
            "/list - List users\n" +
            "/whisper [nickname] [message] or /w [nickname] [message] - Whisper to user\n" +
            "/join [room] - Join / Create room\n" +
            "/listrooms - List available rooms\n" +
            "/listroom - List room members\n" +
            "/help - Show help";
        SendToClient(client, help);
    }

    //whisper func
    private static void HandleWhisperCommand(ClientInfos sender, string command)
    {
        string[] parts = command.Split(new[] { ' ' }, 3); //get target & msg content
        if (parts.Length < 3)
        {
            SendToClient(sender, "Invalid format, Follow this structure: /whisper [nickname] [message]");
            return;
        }

        string targetNick = parts[1].ToLower();
        string message = parts[2];
        ClientInfos target = _clients.Values.FirstOrDefault(c => c.Nickname.ToLower() == targetNick);

        if (target == null)
        {
            SendToClient(sender, $"User {targetNick} not found.");
        }
        else
        {
            SendToClient(target, $"[Whisper] from {sender.Nickname}: {message}");
            SendToClient(sender, $"[You whispered to {target.Nickname}]: {message}");
        }
    }


    //room management funcs
    private static void HandleJoinRoomCommand(ClientInfos client, string command)
    {
        string roomName = command.Substring(6).Trim(); //remove "/join " & sanitize
        if (string.IsNullOrEmpty(roomName)) roomName = "general";

        string oldRoom = client.CurrentRoom;
        client.CurrentRoom = roomName;
        Broadcast($"{client.Nickname} joined room {roomName}", roomName);
        SendToClient(client, $"You joined room {roomName}.");
    }

    private static void HandleListRoomsCommand(ClientInfos client)
    {
        var rooms = _clients.Values.Select(c => c.CurrentRoom).Distinct();
        SendToClient(client, "Rooms: " + string.Join(", ", rooms));
    }

    private static void HandleListRoomCommand(ClientInfos client)
    {
        var members = _clients.Values.Where(c => c.CurrentRoom == client.CurrentRoom).Select(c => c.Nickname);
        SendToClient(client, $"Room members: {string.Join(", ", members)}");
    }

}


