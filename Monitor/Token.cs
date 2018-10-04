using System.Collections.Generic;
using System.Dynamic;

namespace Monitor
{
    public class Token
    {
        public SortedDictionary<string,long> LastRequestNumberList { get; set; }
        public List<string> TokenWaitingQueueu { get; set; }
        public string ReceivedData { get; set; }

        public Token()
        {
            
        }

        public Token(RemoteServerGroup rsg)
        {
            LastRequestNumberList = new SortedDictionary<string, long>();
            LastRequestNumberList[rsg.NodeAddress] = 0;
            foreach (var remoteAddress in rsg.RemoteAddressesList)
            {
                LastRequestNumberList[remoteAddress] = 0;
            }
            TokenWaitingQueueu = new List<string>();
        }

 
    }
}    