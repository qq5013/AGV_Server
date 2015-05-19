using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ServiceModel;

namespace WcfDuplexMessageService
{
    [ServiceBehavior(            
        InstanceContextMode = InstanceContextMode.Single)]
    public class MessageService : IMessageService, IDisposable 
    {
        public static readonly Dictionary<int, string> PropertyToHex = new Dictionary<int, string>() 
        {
        {1,"txtAGVNum"},{2,"txtWL"},{3,"txtTrafficState"},{4,"txtStatus"},{5,"txtWorkLine"},{6,"txtLineNum"},
        {7,"txtMarkNum"},{8,"txtMarkFunction"},{9,"txtSpeed"},{10,"txtDockNum"},{11,"txtPower"},{12,"txtagvCharge"}
        };
        public static ObservableCollection<IClient> ClientCallbackList = new ObservableCollection<IClient>();

        public MessageService()
        {

        }

        public void RegisterClient()
        {
            var client = OperationContext.Current.GetCallbackChannel<IClient>();
            var id = OperationContext.Current.SessionId;
            Console.WriteLine("{0} registered.", id);
            OperationContext.Current.Channel.Closing += new EventHandler(Channel_Closing);
            ClientCallbackList.Add(client);
        }

        public void UnregisterClient()
        {
            var client = OperationContext.Current.GetCallbackChannel<IClient>();
            lock (ClientCallbackList)
            {
                ClientCallbackList.Remove(client);
            }
        }

        void Channel_Closing(object sender, EventArgs e)
        {
            lock (ClientCallbackList)
            {
                ClientCallbackList.Remove((IClient)sender);
            }
        }

        public void Dispose()
        {
            ClientCallbackList.Clear();
        }
    }
}
