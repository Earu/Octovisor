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
    }
}
