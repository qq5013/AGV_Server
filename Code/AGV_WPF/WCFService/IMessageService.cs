using AGV_WPF_DisMember;
using System.ServiceModel;

namespace WcfDuplexMessageService
{
    [ServiceContract(CallbackContract = typeof(IClient))]
    public interface IMessageService
    {
        [OperationContract(IsOneWay = true)]
        void RegisterClient();

        [OperationContract(IsOneWay = true)]
        void UnregisterClient();
    }

    public interface IClient
    {
        //IsOneWay = true;调用者就不会等待回应了.当然,只有不需要返回值,才能使用这个设置。
        [OperationContract(IsOneWay = true)]
        void SendMessage(AGV_DisMember message);

        [OperationContract(IsOneWay = true)]
        void SendProperty(PropertyChangedMessage message);
    }

    public struct PropertyChangedMessage
    {
        public int AGVID;
        public int PropertyID;
        public string PropertyValue;
        public PropertyChangedMessage(int agvid, int id, string value)
        {
            this.AGVID = agvid;
            this.PropertyID = id;
            this.PropertyValue = value;
        }
    }
}
