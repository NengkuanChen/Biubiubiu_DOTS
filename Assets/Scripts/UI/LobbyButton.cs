using Game;
using Lobby;
using TMPro;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LobbyButton: UIElement
    {
        [SerializeField]
        private Button button;

        [SerializeField] 
        private TextMeshProUGUI nameShow;

        [SerializeField] 
        private int teamID;
        public int TeamID => teamID;
        
        
        private int playerInGameID = -1;
        public int PlayerInGameID => playerInGameID;
        
        public string Name
        {
            get => nameShow.text;
            set => nameShow.text = value;
        }
        
        [SerializeField]
        private int positionID;
        public int PositionID => positionID;

        public override void OnInitialize()
        {
            base.OnInitialize();
            button.onClick.AddListener(()=>OnPlayerChangeTeamClicked());
        }
        
        public void SetPlayer(int playerID, string playerName)
        {
            playerInGameID = playerID;
            Name = playerName;
        }



        private void OnPlayerChangeTeamClicked()
        {
            if (playerInGameID != -1)
            {
                return;
            }
            else
            {
                var em = WorldGetter.GetClientWorld().EntityManager;
                var e = em.CreateEntity();
                em.AddComponentData<PositionChangeRequestComponent>(e, new PositionChangeRequestComponent
                {
                    targetTeamID = teamID,
                    targetPositionID = positionID
                });
                em.AddComponentData<SendRpcCommandRequest>(e, new SendRpcCommandRequest
                {
                    TargetConnection = default
                });
            }
        }
        
        public void OnPlayerReady(bool isReady)
        {
            button.image.color = isReady ? Color.green : Color.white;
        }
    }
}