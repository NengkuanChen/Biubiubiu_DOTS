using System.Collections.Generic;
using DefaultNamespace;
using Lobby;
using Player;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LobbyForm: UIForm
    {
        
        private int teamASize;
        private int teamBSize;
        
        [SerializeField]
        private Button readyButton;

        private List<LobbyButton> teamAButtons = new List<LobbyButton>() {null, null, null};
        private List<LobbyButton> teamBButtons = new List<LobbyButton>(){null, null, null};

        public override void OnInitialize()
        {
            base.OnInitialize();
            foreach (var element in Elements)
            {
                if (element is LobbyButton button)
                {
                    if (button.TeamID == 0)
                    {
                        teamAButtons[button.PositionID] = button;
                    }
                    else
                    {
                        teamBButtons[button.PositionID] = button;
                    }
                }
            }
            readyButton.onClick.AddListener(OnReadyButtonClicked);
        }

        private void OnReadyButtonClicked()
        {
            if (WorldGetter.GetClientWorld() != null)
            {
                var newEntity = WorldGetter.GetClientWorld().EntityManager.CreateEntity();
                WorldGetter.GetClientWorld().EntityManager.AddComponentData(newEntity, new PlayerReadyRequest());
                WorldGetter.GetClientWorld().EntityManager
                    .AddComponentData(newEntity, new SendRpcCommandRequestComponent(){ TargetConnection = default});
                
            }
        }
        
        public void OnPlayerReady(int playerID, bool isReady)
        {
            foreach (var element in Elements)
            {
                if (element is LobbyButton button)
                {
                    if (button.PlayerInGameID == playerID)
                    {
                        button.OnPlayerReady(isReady);
                    }
                }
            }
        }

        public void GetPlayerInfo(int PlayerID, out int teamID, out int positionID)
        {
            teamID = -1;
            positionID = -1;
            foreach (var element in Elements)
            {
                if (element is LobbyButton button)
                {
                    if (button.PlayerInGameID == PlayerID)
                    {
                        teamID = button.TeamID;
                        positionID = button.PositionID;
                        return;
                    }
                }
            }
        }
        
        public bool SetButtonToPlayer(int playerID, int teamID, int positionID, string playerName)
        {
            
            if (teamID == 0)
            {
                if (teamAButtons[positionID].PlayerInGameID != -1)
                {
                    return false;
                }
                teamAButtons[positionID].SetPlayer(playerID, playerName);
                teamASize++;
            }
            else
            {
                if (teamBButtons[positionID].PlayerInGameID != -1)
                {
                    return false;
                }
                teamBButtons[positionID].SetPlayer(playerID, playerName);
                teamBSize++;
            }
            return true;
        }
        
        
        public void ClearButton(int teamID, int positionID)
        {
            if (teamID == 0)
            {
                if (teamAButtons[positionID].PlayerInGameID == -1)
                {
                    return;
                }
                teamAButtons[positionID].SetPlayer(-1, "Empty");
                teamAButtons[positionID].OnPlayerReady(false);
                teamASize--;
            }
            else
            {
                if (teamBButtons[positionID].PlayerInGameID == -1)
                {
                    return;
                }
                teamBButtons[positionID].SetPlayer(-1, "Empty");
                teamBButtons[positionID].OnPlayerReady(false);
                teamBSize--;
            }
        }

        public bool OnPlayerJoin(string playerName, int playerInGameID, out int teamID, out int positionID)
        {
            teamID = -1;
            positionID = -1;
            if (teamASize > teamBSize)
            {
                teamBSize++;
                for (int i = 0; i < teamBButtons.Count; i++)
                {
                    if (teamBButtons[i].PlayerInGameID == -1)
                    {
                        SetButtonToPlayer(playerInGameID, 1, i, playerName);
                        teamID = 1;
                        positionID = i;
                        return true;
                    }
                }
                return false;
            }
            teamASize++;
            for (int i = 0; i < teamAButtons.Count; i++)
            {
                if (teamAButtons[i].PlayerInGameID == -1)
                {
                    SetButtonToPlayer(playerInGameID, 0, i, playerName);
                    teamID = 0;
                    positionID = i;
                    return true;
                }
            }
            return false;
        }
        
        
        
        public bool OnPlayerPositionChange(int playerID, int teamID, int positionID, int oTeamID, int oPositionID, string playerName)
        {
            if (teamID == 0)
            {
                if (teamAButtons[positionID].PlayerInGameID != -1)
                {
                    return false;
                }
            }
            else
            {
                if (teamBButtons[positionID].PlayerInGameID != -1)
                {
                    return false;
                }
            }
            SetButtonToPlayer(playerID, teamID, positionID, playerName);
            ClearButton(oTeamID, oPositionID);
            return true;
        }
    }
}