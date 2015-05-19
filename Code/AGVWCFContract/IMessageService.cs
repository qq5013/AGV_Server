using System.Net.Security;
using System.ServiceModel;

namespace WcfDuplexMessageService
{
    [ServiceContract(CallbackContract = typeof(IClient),ProtectionLevel= ProtectionLevel.None,SessionMode = SessionMode.Required)]
    public interface IMessageService
    {
        [OperationContract(IsOneWay = true)]
        void RegisterClient();

        [OperationContract(IsOneWay = true)]
        void UnregisterClient();
    }

    [ServiceContract(ProtectionLevel=ProtectionLevel.None,SessionMode = SessionMode.Required)]
    public interface IClient
    {
        //IsOneWay = true;调用者就不会等待回应了.当然,只有不需要返回值,才能使用这个设置。
        [OperationContract(IsOneWay = true)]
        void SendCarMessage(AGVCar_WCF message);

        [OperationContract(IsOneWay = true)]
        void SendPropertyChangedMessage(PropertyChangedMessage message);

        [OperationContract(IsOneWay = true)]
        void SendSystemMessage(string message);
    }

    public struct PropertyChangedMessage
    {
        public int AGVID;
        public string PropertyID;
        public object PropertyValue;
        public PropertyChangedMessage(int agvid, string id, object value)
        {
            this.AGVID = agvid;
            this.PropertyID = id;
            this.PropertyValue = value;
        }
    }
}
