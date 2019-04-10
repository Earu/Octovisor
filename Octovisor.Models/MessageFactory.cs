using System;
using System.Collections.Generic;
using System.Text;

namespace Octovisor.Models
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
                Status = MessageStatus.DataRequest,
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
                Status = MessageStatus.DataRequest,
            };

            return msg;
        }

        public Message CreateMessage(string identifier, string originame, string targetname, string payload)
        {
            Message msg = new Message
            {
                Data = payload,
                Status = MessageStatus.DataRequest,
                OriginName = originame,
                TargetName = targetname,
                Identifier = identifier,
            };

            return msg;
        }
    }
}
