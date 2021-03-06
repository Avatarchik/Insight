﻿using Mirror;
using System;
using System.Collections.Generic;
using Telepathy;
using UnityEngine;

namespace Insight
{
    public class InsightClient : MonoBehaviour
    {
        public bool AutoStart;
        public int clientID = -1;
        public int connectionID = 0;

        public string networkAddress = "localhost";
        public int networkPort = 5000;
        public bool logNetworkMessages;

        InsightNetworkConnection insightNetworkConnection;

        Client client = new Client();

        Dictionary<short, InsightNetworkMessageDelegate> messageHandlers;

        protected enum ConnectState
        {
            None,
            Connecting,
            Connected,
            Disconnected,
        }
        protected ConnectState connectState = ConnectState.None;

        public bool isConnected { get { return connectState == ConnectState.Connected; } }

        public virtual void Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;

            // use Debug.Log functions for Telepathy so we can see it in the console
            Telepathy.Logger.LogMethod = Debug.Log;
            Telepathy.Logger.LogWarningMethod = Debug.LogWarning;
            Telepathy.Logger.LogErrorMethod = Debug.LogError;

            messageHandlers = new Dictionary<short, InsightNetworkMessageDelegate>();

            if (AutoStart)
            {
                StartClient();
            }
        }
        public virtual void Update()
        {
            HandleNewMessages();
        }

        public void StartClient(string Address, int Port)
        {
            networkAddress = Address;
            networkPort = Port;

            StartClient();
        }

        public void StartClient()
        {
            client.Connect(networkAddress, networkPort);
            clientID = 0;
            insightNetworkConnection = new InsightNetworkConnection();
            insightNetworkConnection.Initialize(this, networkAddress, clientID, connectionID);
            OnClientStart();
        }

        public void StopClient()
        {
            client.Disconnect();

            OnClientStop();
        }

        public bool IsConnecting()
        {
            return client.Connecting;
        }

        public void HandleNewMessages()
        {
            if (clientID == -1)
                return;

            // grab all new messages. do this in your Update loop.
            Message msg;
            while (client.GetNextMessage(out msg))
            {
                switch (msg.eventType)
                {
                    case Telepathy.EventType.Connected:
                        connectState = ConnectState.Connected;
                        OnConnected(msg);
                        break;
                    case Telepathy.EventType.Data:
                        HandleBytes(msg.data);
                        break;
                    case Telepathy.EventType.Disconnected:
                        connectState = ConnectState.Disconnected;
                        OnDisconnected(msg);
                        break;
                }
            }
        }

        public bool Send(byte[] data)
        {
            if (client.Connected)
            {
                return SendBytes(connectionID, data);
            }
            Debug.Log("Client.Send: not connected!");
            return false;
        }

        public bool SendMsg(short msgType, MessageBase msg)
        {
            NetworkWriter writer = new NetworkWriter();
            msg.Serialize(writer);

            // pack message and send
            byte[] message = Protocol.PackMessage((ushort)msgType, writer.ToArray());
            return SendBytes(0, message);
        }

        private bool SendBytes(int connectionId, byte[] bytes)
        {
            if (logNetworkMessages) { Debug.Log("ConnectionSend con:" + connectionId + " bytes:" + BitConverter.ToString(bytes)); }

            if (bytes.Length > int.MaxValue)
            {
                Debug.LogError("NetworkConnection:SendBytes cannot send packet larger than " + int.MaxValue + " bytes");
                return false;
            }

            if (bytes.Length == 0)
            {
                // zero length packets getting into the packet queues are bad.
                Debug.LogError("NetworkConnection:SendBytes cannot send zero bytes");
                return false;
            }

            return client.Send(bytes);
        }

        protected void HandleBytes(byte[] buffer)
        {
            // unpack message
            ushort msgType;
            byte[] content;
            if (Protocol.UnpackMessage(buffer, out msgType, out content))
            {
                //if (logNetworkMessages) { Debug.Log("ConnectionRecv con:" + connectionId + " msgType:" + msgType + " content:" + BitConverter.ToString(content)); }
                if (logNetworkMessages) { Debug.Log(" msgType:" + msgType + " content:" + BitConverter.ToString(content)); }

                InsightNetworkMessageDelegate msgDelegate;
                if (messageHandlers.TryGetValue((short)msgType, out msgDelegate))
                {
                    // create message here instead of caching it. so we can add it to queue more easily.
                    InsightNetworkMessage msg = new InsightNetworkMessage();
                    msg.msgType = (short)msgType;
                    msg.reader = new NetworkReader(content);
                    //msg.conn = this;

                    msgDelegate(msg);
                    //lastMessageTime = Time.time;
                }
                else
                {
                    //NOTE: this throws away the rest of the buffer. Need moar error codes
                    Debug.LogError("Unknown message ID " + msgType);// + " connId:" + connectionId);
                }
            }
            else
            {
                Debug.LogError("HandleBytes UnpackMessage failed for: " + BitConverter.ToString(buffer));
            }
        }

        public void RegisterHandler(short msgType, InsightNetworkMessageDelegate handler)
        {
            if (messageHandlers.ContainsKey(msgType))
            {
                //if (LogFilter.Debug) { Debug.Log("NetworkConnection.RegisterHandler replacing " + msgType); }
                Debug.Log("NetworkConnection.RegisterHandler replacing " + msgType);
            }
            messageHandlers[msgType] = handler;
        }

        public void UnRegisterHandler(short msgType, InsightNetworkMessageDelegate handler)
        {
            if (messageHandlers.ContainsKey(msgType))
            {
                messageHandlers[msgType] -= handler;
            }
        }

        public void ClearHandlers()
        {
            messageHandlers.Clear();
        }

        private void OnApplicationQuit()
        {
            StopClient();
        }

        //------------Virtual Handlers-------------
        public virtual void OnConnected(Message msg)
        {
            Debug.Log("OnConnected");
        }

        public virtual void OnDisconnected(Message msg)
        {
            Debug.Log("OnDisconnected");
        }

        public virtual void OnClientStart()
        {
            Debug.Log("Connecting to Insight Server: " + networkAddress + ":" + networkPort);
        }

        public virtual void OnClientStop()
        {
            Debug.Log("Disconnecting from Insight Server");
        }
    }
}