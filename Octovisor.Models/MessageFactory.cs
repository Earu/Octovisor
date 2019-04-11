namespace Octovisor.Messages
{
    public class MessageFactory
    {
        public Message CreateRegisterMessage(string procname, string token)
        {
            Message msg = new Message
            {
                OriginName = procname,
                TargetName = "SERVER",
                Identifier = "INTERNAL_OCTOVISOR_PROCESS_INIT",
                Data = token,
                Type = MessageType.Request,
                Status = MessageStatus.Unknown,
            };

            return msg;
        }

        public Message CreateUnregisterMessage(string procname, string token)
        {
            Message msg = new Message
            {
                OriginName = procname,
                TargetName = "SERVER",
                Identifier = "INTERNAL_OCTOVISOR_PROCESS_END",
                Data = token,
                Type = MessageType.Request,
                Status = MessageStatus.Unknown,
            };

            return msg;
        }

        public Message CreateMessage(string identifier, string originame, string targetname, string payload)
        {
            Message msg = new Message
            {
                OriginName = originame,
                TargetName = targetname,
                Identifier = identifier,
                Data = payload,
                Type = MessageType.Request,
                Status = MessageStatus.Unknown,
            };

            return msg;
        }

        public Message CreateMessageResponse(Message msg, string payload, MessageStatus status)
        {
            Message newmsg = new Message
            {
                OriginName = msg.TargetName,
                TargetName = msg.OriginName,
                Identifier = msg.Identifier,
                Data = payload,
                Type = MessageType.Response,
                Status = status,
            };

            return newmsg;
        }
    }
}
