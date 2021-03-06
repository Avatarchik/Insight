﻿using Insight;
using UnityEngine;

public class MasterInsightServer : InsightServer
{
    // Use this for initialization
    public override void Start ()
    {        
        base.Start();
        
        StartServer(networkPort);
        RegisterHandlers();

#if !UNITY_EDITOR
        Application.targetFrameRate = Mathf.RoundToInt(1f / Time.fixedDeltaTime);
        print("server tick rate set to: " + Application.targetFrameRate + " (1 / Edit->Project Settings->Time->Fixed Time Step)");
#endif
    }

    // Update is called once per frame
    public override void Update ()
    {
        base.Update();

        HandleNewMessages();
    }

    public override void OnConnected(InsightNetworkConnection conn)
    {
        print("OnConnected");
    }

    public override void OnDisconnected(InsightNetworkConnection conn)
    {
        print("OnDisconnected");
    }

    public override void OnServerStart()
    {
        print("OnServerStart");
    }

    public override void OnServerStop()
    {
        print("OnServerStop");
    }

    private void RegisterHandlers()
    {
        
    }

    private void OnApplicationQuit()
    {
        StopServer();
    }
}