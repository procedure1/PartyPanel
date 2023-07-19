using ProtoBuf;
using System;

namespace PartyPanelShared.Models
{
    [ProtoContract]
    public class Command
    {
        //BW added 5 new commands
        public enum CommandType
        {
            Heartbeat,
            ReturnToMenu,
            SoloMenu,
            OnlineMenu,
            PlayCampaign,
            PartyMenu,
            PlayTutorial
        }
        [ProtoMember(1)]
        public CommandType commandType;

        public Command(CommandType commandType)
        {
            this.commandType = commandType;
        }

        public Command()
        {
        }
    }
}
