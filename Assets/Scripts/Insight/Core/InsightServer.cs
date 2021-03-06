﻿using Mirror;
using System.Collections;
using System.Collections.Generic;
using Telepathy;
using UnityEngine;

namespace Insight
{
    public class InsightServer : MonoBehaviour
    {
        public bool AutoStart;
        public int networkPort = 5000;
        protected int serverHostId = -1;

        Server server;
        protected Dictionary<int, InsightNetworkConnection> connections;
        Dictionary<short, InsightNetworkMessageDelegate> messageHandlers; //Default Handlers

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
            DontDestroyOnLoad(this);
            Application.runInBackground = true;

            // use Debug.Log functions for Telepathy so we can see it in the console
            Telepathy.Logger.LogMethod = Debug.Log;
            Telepathy.Logger.LogWarningMethod = Debug.LogWarning;
            Telepathy.Logger.LogErrorMethod = Debug.LogError;

            // create and start the server
            server = new Server();

            connections = new Dictionary<int, InsightNetworkConnection>();

            messageHandlers = new Dictionary<short, InsightNetworkMessageDelegate>();

            if(AutoStart)
            {
                StartServer();
            }
        }

        public virtual void Update()
        {
            HandleNewMessages();
        }

        public void StartServer(int Port)
        {
            networkPort = Port;

            StartServer();
        }

        public void StartServer()
        {
            Debug.Log("Start Insight Server On Port: " + networkPort);
            server.Start(networkPort);
            serverHostId = 0;

            connectState = ConnectState.Connected;

            OnServerStart();
        }

        public void StopServer()
        {
            connections.Clear();

            // stop the server when you don't need it anymore
            server.Stop();
            serverHostId = -1;

            connectState = ConnectState.Disconnected;

            OnServerStop();
        }

        // grab all new messages. do this in your Update loop.
        public void HandleNewMessages()
        {
            if (serverHostId == -1)
                return;

            Message msg;
            while (server.GetNextMessage(out msg))
            {
                switch (msg.eventType)
                {
                    case Telepathy.EventType.Connected:
                        HandleConnect(msg);
                        break;
                    case Telepathy.EventType.Data:
                        HandleData(msg.connectionId, msg.data, 0);
                        break;
                    case Telepathy.EventType.Disconnected:
                        HandleDisconnect(msg);
                        break;
                }
            }
        }

        void HandleConnect(Message msg)
        {
            // get ip address from connection
            string address = GetConnectionInfo(msg.connectionId);

            // add player info
            InsightNetworkConnection conn = new InsightNetworkConnection();
            conn.Initialize(this, address, serverHostId, msg.connectionId);
            AddConnection(conn);

            OnConnected(conn);
        }

        void HandleDisconnect(Message msg)
        {
            InsightNetworkConnection conn;
            if (connections.TryGetValue(msg.connectionId, out conn))
            {
                conn.Disconnect();
                RemoveConnection(msg.connectionId);

                OnDisconnected(conn);
            }
        }

        void HandleData(int connectionId, byte[] data, byte error)
        {
            InsightNetworkConnection conn;
            if (connections.TryGetValue(connectionId, out conn))
            {
                //print("");
                OnData(conn, data);
                return;
            }
            else
            {
                Debug.LogError("HandleData Unknown connectionId:" + connectionId);
            }
        }

        void OnData(InsightNetworkConnection conn, byte[] data)
        {
            conn.TransportReceive(data);
        }

        public bool Send(int connectionId, byte[] data)
        {
            return server.Send(connectionId, data);
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

        public string GetConnectionInfo(int connectionId)
        {
            string address;
            server.GetConnectionInfo(connectionId, out address);
            return address;
        }

        public bool AddConnection(InsightNetworkConnection conn)
        {
            if (!connections.ContainsKey(conn.connectionId))
            {
                // connection cannot be null here or conn.connectionId
                // would throw NRE
                connections[conn.connectionId] = conn;
                conn.SetHandlers(messageHandlers);
                return true;
            }
            // already a connection with this id
            return false;
        }

        public bool RemoveConnection(int connectionId)
        {
            return connections.Remove(connectionId);
        }

        private void OnApplicationQuit()
        {
            StartCoroutine(ShutDown());
        }

        private IEnumerator ShutDown()
        {
            Debug.Log("Stopping Server");
            server.Stop();
            yield return new WaitForSeconds(1);
        }

        //----------virtual handlers--------------//

        public virtual void OnConnected(InsightNetworkConnection conn)
        {
            Debug.Log("OnConnected");
        }

        public virtual void OnDisconnected(InsightNetworkConnection conn)
        {
            Debug.Log("OnDisconnected");
        }

        public virtual void OnServerStart()
        {

        }

        public virtual void OnServerStop()
        {

        }
    }
}