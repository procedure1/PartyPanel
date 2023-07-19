using ProtoBuf;
using System;

namespace PartyPanelShared.Models
{
    [ProtoContract]
    public class Command
    {
		//BW added
        public enum CommandType
        {
            Heartbeat,
            ReturnToMenu,
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
