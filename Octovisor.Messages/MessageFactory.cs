namespace Octovisor.Messages
{
    public class MessageFactory
    {
        public Message CreateClientRegisterMessage(string processName, string token)
        {
            Message msg = new Message
            {
                OriginName = processName,
                TargetName = MessageConstants.SERVER_PROCESS_NAME,
                Identifier = MessageConstants.REGISTER_IDENTIFIER,
                Data = token,
                Type = MessageType.Request,
                Status = MessageStatus.Unknown,
            };

            return msg;
        }

        public Message CreateClientUnregisterMessage(string processName, string token)
        {
            Message msg = new Message
            {
                OriginName = processName,
                TargetName = MessageConstants.SERVER_PROCESS_NAME,
                Identifier = MessageConstants.END_IDENTIFIER,
                Data = token,
                Type = MessageType.Request,
                Status = MessageStatus.Unknown,
            };

            return msg;
        }

        public Message CreateClientRequestProcessesInfoMessage(string processName)
        {
            Message msg = new Message
            {
                OriginName = processName,
                TargetName = MessageConstants.SERVER_PROCESS_NAME,
                Identifier = MessageConstants.REQUEST_PROCESSES_INFO_IDENTIFIER,
                Data = null,
                Type = MessageType.Request,
                Status = MessageStatus.Unknown
            };

            return msg;
        }

        public Message CreateMessageRequest(string identifier, string originName, string targetName, string payload)
        {
            Message msg = new Message
            {
                OriginName = originName,
                TargetName = targetName,
                Identifier = identifier,
                Data = payload,
                Type = MessageType.Request,
                Status = MessageStatus.Unknown,
            };

            return msg;
        }

        public Message CreateMessage(string identifier, string originName, string targetName, string payload, MessageType type, MessageStatus status)
        {
            Message msg = new Message
            {
                OriginName = originName,
                TargetName = targetName,
                Identifier = identifier,
                Data = payload,
                Type = type,
                Status = status,
            };

            return msg;
        }

        public Message CreateMessageResponse(Message msg, string payload, MessageStatus status)
        {
            Message newMsg = new Message
            {
                OriginName = msg.TargetName,
                TargetName = msg.OriginName,
                Identifier = msg.Identifier,
                Data = payload,
                Type = MessageType.Response,
                Status = status,
            };

            return newMsg;
        }
    }
}
