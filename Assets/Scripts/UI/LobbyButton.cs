using DefaultNamespace;
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

        private void OnPlayerChangeTeamClicked()
        {
            if (Name != "Empty")
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
                em.AddComponentData<SendRpcCommandRequestComponent>(e, new SendRpcCommandRequestComponent
                {
                    TargetConnection = default
                });
            }
        }
    }
}