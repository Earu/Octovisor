using System.Collections.Generic;
using System.Text;

namespace Octovisor.Messages
{
    public class MessageReader
    {
        // Number of bytes possible before clearing up data (1GB)
        private const int Threshold = 1000000000; 

        private readonly char MessageFinalizer;
        private readonly StringBuilder Builder;

        public MessageReader(char messageFinalizer)
        {
            this.MessageFinalizer = messageFinalizer;
            this.Builder = new StringBuilder();
        }

        public int Size { get => this.Builder.Length; }

        public string Value { get => this.Builder.ToString(); }

        public List<Message> Read(string content)
        {
            string current;
            List<Message> msgs = new List<Message>();
            if (string.IsNullOrWhiteSpace(content))
                return msgs;

            foreach (char c in content)
            {
                this.Builder.Append(c);

                current = this.Value;
                if (current.Length >= 1 && current[current.Length - 1].Equals(this.MessageFinalizer))
                {
                    string smsg = current.Substring(0, current.Length - 1);
                    if (!string.IsNullOrWhiteSpace(smsg))
                        msgs.Add(Message.Deserialize(smsg));

                    this.Clear();
                }
            }

            if (this.Size >= Threshold)
                this.Clear();

            return msgs;
        }

        public void Clear()
            => this.Builder.Clear();
    }
}
