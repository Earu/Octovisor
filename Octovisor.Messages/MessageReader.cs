using System.Collections.Generic;
using System.Text;

namespace Octovisor.Messages
{
    public class MessageReader
    {
        // Number of bytes possible before clearing up data (1GB)
        private const int Treshold = 1000000000; 

        private readonly string MessageFinalizer;
        private readonly StringBuilder Builder;

        public MessageReader(string messageFinalizer)
        {
            this.MessageFinalizer = messageFinalizer;
            this.Builder = new StringBuilder(Treshold);
        }

        public int Size { get => this.Builder.Length; }

        public string Value { get => this.Builder.ToString(); }

        public List<Message> Read(string content)
        {
            string current;
            List<Message> msgs = new List<Message>();
            int finalizerLen = this.MessageFinalizer.Length;
            foreach (char c in content)
            {
                this.Builder.Append(c);

                current = this.Value;
                if (current.Length >= finalizerLen && current.EndsWith(this.MessageFinalizer))
                {
                    string smsg = current.Substring(0, current.Length - finalizerLen);
                    if (!string.IsNullOrWhiteSpace(smsg))
                        msgs.Add(Message.Deserialize(smsg));

                    this.Clear();
                }
            }

            if (this.Size >= Treshold)
                this.Clear();

            return msgs;
        }

        public void Clear()
            => this.Builder.Clear();
    }
}
