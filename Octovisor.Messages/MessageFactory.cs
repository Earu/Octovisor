using System.Threading;

namespace Octovisor.Messages
{
    public class MessageFactory
    {
        private int CurrentMessageID;

        public MessageFactory()
        {
            this.CurrentMessageID = 0;
        }

        private void SetMessageErrorString(Message msg)
        {
            if (!msg.HasException) return;
            if (msg.HasException && !string.IsNullOrWhiteSpace(msg.Error)) return;

            switch (msg.Status)
            {
                case MessageStatus.NetworkError:
                    msg.Error = "There was a problem with the network";
                    break;
                case MessageStatus.MalformedMessageError:
                    msg.Error = "Could not read a message because it is malformed";
                    break;
                case MessageStatus.ProcessNotFound:
                    msg.Error = "Could not find the target process specified";
                    break;
                case MessageStatus.ServerError:
                    msg.Error = "There was a problem with your transmission";
                    break;
                case MessageStatus.TargetError:
                    msg.Error = "The target remote process encountered an error when processing your transmission";
                    break;
                default:
                    msg.Error = null;
                    break;
            }
        }

        public Message CreateClientRegisterMessage(string processName, string token)
        {
            Message msg = new Message
            {
                ID = -1,
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
                ID = -1,
                OriginName = processName,
                TargetName = MessageConstants.SERVER_PROCESS_NAME,
                Identifier = MessageConstants.TERMINATE_IDENTIFIER,
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
                ID = -1,
                OriginName = processName,
                TargetName = MessageConstants.SERVER_PROCESS_NAME,
                Identifier = MessageConstants.REQUEST_PROCESSES_INFO_IDENTIFIER,
                Data = null,
                Type = MessageType.Request,
                Status = MessageStatus.Unknown
            };

            return msg;
        }

        public Message CreateMessage(string identifier, string originName, string targetName, string payload, MessageType type, MessageStatus status)
        {
            Message msg = new Message
            {
                ID = this.CurrentMessageID,
                OriginName = originName,
                TargetName = targetName,
                Identifier = identifier,
                Data = payload,
                Type = type,
                Status = status,
            };

            Interlocked.Increment(ref this.CurrentMessageID);
            this.SetMessageErrorString(msg);

            return msg;
        }

        public Message CreateMessageRequest(string identifier, string originName, string targetName, string payload)
        {
            Message msg = new Message
            {
                ID = this.CurrentMessageID,
                OriginName = originName,
                TargetName = targetName,
                Identifier = identifier,
                Data = payload,
                Type = MessageType.Request,
                Status = MessageStatus.Unknown,
            };

            Interlocked.Increment(ref this.CurrentMessageID);

            return msg;
        }

        public Message CreateMessageResponse(Message msg, string payload, MessageStatus status)
        {
            Message newMsg = new Message
            {
                ID = msg.ID,
                OriginName = msg.TargetName,
                TargetName = msg.OriginName,
                Identifier = msg.Identifier,
                Data = payload,
                Type = MessageType.Response,
                Status = status,
            };

            this.SetMessageErrorString(newMsg);

            return newMsg;
        }
    }
}
