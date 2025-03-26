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
        public NetworkStream Stream => Client.GetStream();
    }

	private static List<ClientInfos> _clients = new List<ClientInfos>();
	private static int _guestNumber = 0;

    public static void Main (string[] args)
	{
		Console.WriteLine("Server started on port 55555");

		TcpListener listener = new TcpListener (IPAddress.Any, 55555);
		listener.Start ();

		while (true)
		{
            ProcessNewClients(listener);
            ProcessExistingClients();
            CleanupFaultyClients();

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
            ClientInfos clientInfo = new ClientInfos();
            clientInfo.Client = client;
            clientInfo.Nickname = $"Guest{_guestNumber++}";
            _clients.Add(clientInfo);

            SendToClient(clientInfo, $"You joined as {clientInfo.Nickname}!");
            Broadcast($"{clientInfo.Nickname} joined the chat", exclude: clientInfo);
        }
    }
    private static void ProcessExistingClients()
    {
        List<ClientInfos> faultyClients = new List<ClientInfos>();


        foreach (ClientInfos client in _clients)
        {
            NetworkStream stream = client.Stream;
            if (stream.DataAvailable)
            {
                try
                {
                    byte[] inBytes = StreamUtil.Read(stream);
                    string inString = Encoding.UTF8.GetString(inBytes);
                    string formatted = $"[{DateTime.Now:HH:mm:ss} - {client.Client.Client.RemoteEndPoint}] {client.Nickname}: {strrev(inString)}";


                    Broadcast(formatted);
                }
                catch(Exception e)
                {
                    Console.WriteLine($"Error > {e.Message}");
                    faultyClients.Add(client);
                }
            }
        }
    }

    private static void CleanupFaultyClients()
    {
        List<ClientInfos> faultyClients = new List<ClientInfos>();
        foreach (ClientInfos client in _clients)
        {
            if (!IsClientConnected(client.Client))
            {
                faultyClients.Add(client);
                Broadcast($"{client.Nickname} left the chat");
            }
        }

        foreach (ClientInfos client in faultyClients)
        {
            client.Client.Close();
        }

        _clients.RemoveAll(c => faultyClients.Contains(c));
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

    private static void Broadcast(string message, ClientInfos exclude = null)
    {
        byte[] outBytes = Encoding.UTF8.GetBytes(message);
        foreach (ClientInfos client in _clients)
        {
            if (client != exclude)
            {
                try
                {
                    StreamUtil.Write(client.Stream, outBytes);
                }
                catch
                {
                    
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

}


