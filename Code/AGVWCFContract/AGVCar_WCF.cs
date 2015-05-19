using System;
using System.Runtime.Serialization;

namespace WcfDuplexMessageService
{
    [DataContract]
    public class AGVCar_WCF
    {
        [DataMember]
        public byte AGVNum { get; set; }

        [DataMember]
        public float TotalDistance { get; set; }

        [DataMember]
        public int BootCount { get; set; }

        [DataMember]
        public byte WorklineNum { get; set; }

        [DataMember]
        public Int16 LineNum { get; set; }

        [DataMember]
        public int MarkNum { get; set; }

        [DataMember]
        public byte MarkFunction { get; set; }

        [DataMember]
        public bool WlLink { get; set; }

        [DataMember]
        public byte AGVStatus { get; set; }

        [DataMember]
        public byte AGVSpeed { get; set; }

        [DataMember]
        public byte AGVPower { get; set; }

        [DataMember]
        public byte AGVCharge { get; set; }

        [DataMember]
        public int TrafficNum { get; set; }

        [DataMember]
        public bool TrafficStatus { get; set; }

        [DataMember]
        public int DockNum { get; set; }

        [DataMember]
        public int AGVTask { get; set; }

        public AGVCar_WCF(byte agvnum,float td,int bootcount,byte wlnum,Int16 linenum,
            int marknum,byte markfun,bool wllink,byte status,byte speed,byte power,
            byte charge,int trafficnum, bool trafficStatus,int docknum,int task)
        {
            this.AGVNum = agvnum;
            this.TotalDistance = td;
            this.BootCount = bootcount;
            this.WorklineNum = wlnum;
            this.LineNum = linenum;
            this.MarkNum = marknum;
            this.MarkFunction = markfun;
            this.WlLink = wllink;
            this.AGVStatus = status;
            this.AGVSpeed = speed;
            this.AGVPower = power;
            this.AGVCharge = charge;
            this.TrafficNum = trafficnum;
            this.TrafficStatus = trafficStatus;
            this.DockNum = docknum;
            this.AGVTask = task;

        }
    }
}
