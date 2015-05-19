using System;
using System.Runtime.Serialization;

namespace AGV_WPF
{
    public class AGVTemp
    {
        
        public byte AGVNum { get; set; }

        
        public float TotalDistance { get; set; }

        
        public int BootCount { get; set; }

        
        public byte WorklineNum { get; set; }

        
        public Int16 LineNum { get; set; }

        
        public int MarkNum { get; set; }

        
        public byte MarkFunction { get; set; }

        
        public bool WlLink { get; set; }


        public byte WlLinkCount { get; set; }

        
        public byte AGVStatus { get; set; }

        
        public byte AGVSpeed { get; set; }

        
        public byte AGVPower { get; set; }

        
        public byte AGVCharge { get; set; }

        
        public int TrafficNum { get; set; }

        
        public bool TrafficStatus { get; set; }

        
        public int DockNum { get; set; }

        
        public int AGVTask { get; set; }


        public AGVTemp()
        {

        }
    }
}
