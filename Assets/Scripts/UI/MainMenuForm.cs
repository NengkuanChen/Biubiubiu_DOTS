using System;
using System.Linq;
using DefaultNamespace;
using Lobby;
using Player;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public class MainMenuForm: UIForm
    {
        [SerializeField]
        private TMP_InputField nicknameInputField;
        
        [SerializeField]
        private TMP_InputField ipInputField;

        [SerializeField]
        private Button joinServerButton;
        
        [SerializeField]
        private Button createHostButton;
        
        [SerializeField]
        private Button createServerButton;

        private String nickName;
        public String NickName => nickName;
        
        private ushort port = 7979;

        public override void OnInitialize()
        {
            base.OnInitialize();
            joinServerButton.onClick.AddListener(OnJoinServerButtonClicked);
            createHostButton.onClick.AddListener(OnCreateHostButtonClicked);
            createServerButton.onClick.AddListener(OnCreateServerButtonClicked);
            Debug.Log("Local IP: " + NetworkEndpoint.LoopbackIpv4.Address);
#if UNITY_SERVER
            ServerLaunch();
#endif
        }

        private void OnCreateServerButtonClicked()
        {
            ServerLaunch();
        }

        private void ServerLaunch()
        {
            var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            DestroyLocalSimulationWorld();
            SceneManager.LoadSceneAsync("Lobby", LoadSceneMode.Additive);
            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = server;
            NetworkEndpoint ep = NetworkEndpoint.AnyIpv4.WithPort(port);
            {
                using var drvQuery = server.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(ep);
            }
            UIManager.Singleton.ShowForm<LobbyForm>();
            CloseSelf();
        }

        private void OnCreateHostButtonClicked()
        {
            var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            DestroyLocalSimulationWorld();
            SceneManager.LoadSceneAsync("Lobby", LoadSceneMode.Additive);
            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = server;
            nickName = nicknameInputField.text;
            NetworkEndpoint ep = NetworkEndpoint.AnyIpv4.WithPort(port);
            {
                using var drvQuery = server.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(ep);
            }
            
            
            
            ep = NetworkEndpoint.LoopbackIpv4.WithPort(port);
            {
                using var drvQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, ep);
            }
            
            // LobbySystemClient.Singleton.SendPlayerIdentity();
            CloseSelf();
            UIManager.Singleton.ShowForm<LobbyForm>();
        }

        private void OnJoinServerButtonClicked()
        {
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            DestroyLocalSimulationWorld();
            SceneManager.LoadSceneAsync("Lobby", LoadSceneMode.Additive);
            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = client;
            nickName = nicknameInputField.text;
            var ep = NetworkEndpoint.Parse(ipInputField.text, port);
            {
                using var drvQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, ep);
            }
            // LobbySystemClient.Singleton.SendPlayerIdentity();
            UIManager.Singleton.ShowForm<LobbyForm>();
            CloseSelf();
        }
        
        private void DestroyLocalSimulationWorld()
        {
            WorldGetter.GetLocalWorld().Dispose();
        }
        
    }
}