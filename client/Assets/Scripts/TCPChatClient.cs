using shared;
using System;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Collections.Concurrent;
using System.Threading;

/**
 * Assignment 2 - Starting project.
 * 
 * @author J.C. Wichman
 */
public class TCPChatClient : MonoBehaviour
{
    [SerializeField] private PanelWrapper _panelWrapper = null;
    [SerializeField] private string _hostname = "localhost";
    [SerializeField] private int _port = 55555;

    private Thread _receiveThread;
    private ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
    private TcpClient _client;

    void Start()
    {
        _panelWrapper.OnChatTextEntered += onTextEntered;
        connectToServer();

        _receiveThread = new Thread(receiveMsg);
        _receiveThread.IsBackground = true;
        _receiveThread.Start();

    }

    private void receiveMsg()
    {
        try
        {
            NetworkStream stream = _client.GetStream();
            while (_client.Connected)
            {
                if (stream.DataAvailable)
                {
                    byte[] inBytes = StreamUtil.Read(_client.GetStream());
                    string inString = Encoding.UTF8.GetString(inBytes);
                    _messageQueue.Enqueue(inString);
                }
                Thread.Sleep(100);
            }
        } 
        catch(Exception e)
        {
            _messageQueue.Enqueue($"Error > {e.Message}");
        }
    }

    void Update()
    {
        while (_messageQueue.TryDequeue(out string message))
        {
            _panelWrapper.AddOutput(message);
        }
    }

    void OnDestroy()
    {
        _client.Close();
        _receiveThread.Abort();
    }

    private void connectToServer()
    {
        try
        {
			_client = new TcpClient();
            _client.Connect(_hostname, _port);
            _panelWrapper.ClearOutput();
            _panelWrapper.AddOutput("Connected to server.");
        }
        catch (Exception e)
        {
            _panelWrapper.AddOutput("Could not connect to server:");
            _panelWrapper.AddOutput(e.Message);
        }
    }

    private void onTextEntered(string pInput)
    {
        if (pInput == null || pInput.Length == 0) return;

        _panelWrapper.ClearInput();

		try 
        {
			//echo client - send one, expect one (hint: that is not how a chat works ...)
			byte[] outBytes = Encoding.UTF8.GetBytes(pInput);
            NetworkStream stream = _client.GetStream();
			StreamUtil.Write(stream, outBytes);
            
   //         if (stream.DataAvailable)
   //         {
			//byte[] inBytes = StreamUtil.Read(_client.GetStream());
   //         string inString = Encoding.UTF8.GetString(inBytes);
   //         _panelWrapper.AddOutput(inString);
   //         }
		} 
        catch (Exception e) 
        {
            _panelWrapper.AddOutput(e.Message);
			//for quicker testing, we reconnect if something goes wrong.
			_client.Close();
			connectToServer();
		}
    }

}

