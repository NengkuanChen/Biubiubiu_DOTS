using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    public class LobbyForm: UIForm
    {
        
        private int teamASize;
        private int teamBSize;
        
        private int currentTeamID;
        private int currentPositionID;

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
        }

        public int OnPlayerJoin(string playerName)
        {
            if (teamASize == 3 && teamBSize == 3)
            {
                return -1;
            }
            if (teamASize > teamBSize)
            {
                teamBSize++;
                foreach (var button in teamBButtons)
                {
                    if (button.Name != "Empty")
                    {
                        continue;
                    }
                    button.Name = playerName;
                    break;
                }
                currentTeamID = 1;
                return 1;
            }
            foreach (var button in teamAButtons)
            {
                if (button.Name != "Empty")
                {
                    continue;
                }
                button.Name = playerName;
                break;
            }
            teamASize++;
            currentTeamID = 0;
            return 0;
        }
        
        public bool OnPlayerPositionChange(int teamID, int positionID, int oTeamID, int oPositionID, string playerName)
        {
            if (teamID == 0)
            {
                if (teamAButtons[positionID].Name != "Empty")
                {
                    return false;
                }
                teamAButtons[positionID].Name = playerName;
            }
            else
            {
                if (teamBButtons[positionID].Name != "Empty")
                {
                    return false;
                }
                teamBButtons[positionID].Name = playerName;
            }
            if (oTeamID == 0)
            {
                teamAButtons[oPositionID].Name = "Empty";
            }
            else
            {
                teamBButtons[oPositionID].Name = "Empty";
            }
            return true;
        }
    }
}