using System.Collections.Generic;
using System.Text;

namespace Octovisor.Messages
{
    public class MessageReader
    {
        // Number of bytes possible before clearing up data (1GB)
        private const int _Threshold = 1000000000; 

        private readonly string _MessageFinalizer;
        private readonly StringBuilder _Builder;

        public MessageReader(string messagefinalizer)
        {
            this._MessageFinalizer = messagefinalizer;
            this._Builder = new StringBuilder(_Threshold);
        }

        public int Size { get => this._Builder.Length; }

        private List<Message> JsonToMessageList(List<string> jmsgs)
        {
            List<Message> msgs = new List<Message>();
            foreach (string jmsg in jmsgs)
                msgs.Add(Message.Deserialize(jmsg));

            return msgs;
        }

        public List<Message> Read(string content)
        {
            List<string> msgdata = new List<string>();
            foreach (char c in content)
            {
                this._Builder.Append(c);

                string current = this._Builder.ToString();
                int endlen = this._MessageFinalizer.Length;
                if (current.Length >= endlen && current.Substring(current.Length - endlen, endlen).Equals(this._MessageFinalizer))
                {
                    string smsg = current.Substring(0, current.Length - endlen);
                    if (!string.IsNullOrWhiteSpace(smsg))
                        msgdata.Add(smsg);

                    this._Builder.Clear();
                }
            }

            if (this._Builder.Length >= _Threshold)
                this._Builder.Clear();

            return this.JsonToMessageList(msgdata);
        }

        public void Clear()
            => this._Builder.Clear();
    }
}
