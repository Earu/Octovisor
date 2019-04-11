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

        public List<Message> Read(string content)
        {
            List<Message> msgs = new List<Message>();
            foreach (char c in content)
            {
                this.Builder.Append(c);

                string current = this.Builder.ToString();
                int finalizerLen = this.MessageFinalizer.Length;
                if (current.Length >= finalizerLen && current.EndsWith(this.MessageFinalizer))
                {
                    string smsg = current.Substring(0, current.Length - finalizerLen);
                    if (!string.IsNullOrWhiteSpace(smsg))
                        msgs.Add(Message.Deserialize(smsg));

                    this.Builder.Clear();
                }
            }

            if (this.Builder.Length >= Treshold)
                this.Builder.Clear();

            return msgs;
        }

        public void Clear()
            => this.Builder.Clear();
    }
}
