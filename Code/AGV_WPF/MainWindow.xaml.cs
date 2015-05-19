using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.IO.Ports;
using System.Data;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Configuration;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using DAL;
using COM;
using AGV_WPF_Global;
using AGV_WPF.DLL.AGV;
using AGV_WPF.DLL;
using System.IO;
using System.ServiceModel;
using System.Windows.Controls.Primitives;
using WcfDuplexMessageService;

namespace AGV_WPF
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>

    #region AGV结构体的定义
    public struct AGVDockAreaStr
    {
        public WorkMarkStr DockStartStop;   //停靠区起点
        public WorkMarkStr DockEndStop;     //停靠区终点 
        public int DockNum;         //停靠区路线
    }

    public struct WorkMarkStr
    {
        public int Line;    //AGV工作路线，流水线号
        public int Num; 	//对应流水线号管制区地标号
        public WorkMarkStr(int line, int num)
        {
            this.Line = line;
            this.Num = num;
        }
    };

    public struct SpeedStr
    {
        public int CmdNum;
        public double Speed;
        public string SpeedGrade;
        public SpeedStr(int num,double speed,string grade)
        {
            this.CmdNum = num;
            this.Speed = speed;
            this.SpeedGrade = grade;
        }
    }


    #endregion

    public partial class MainWindow : Window
    {
        #region 类变量
        #region AGV常变量的定义

        //常变量的定义

        //AGV运行控制
        public const bool AGVMODERUN = true;
        public const bool AGVMODESTOP = false;

        //AGV无线连接状态
        public const bool AGVWLLINK_OK = true;
        public const bool AGVWLLINK_ERROR = false;

        //实际的AGV数量
        public byte AGVNUM_MAX = 10;// = Convert.ToByte(ConfigurationManager.AppSettings["AGVNUM_MAX"]);

        //AGV地标功能
        public const byte AGVMARKFUN_OVER = 15;

        //帧校验结果
        public const byte VERIFY_NOERROR = 0;
        public const byte VERIFY_HEADERROR = 1;
        public const byte VERIFY_ENDERROR = 2;
        public const byte VERIFY_FUNERROR = 3;
        public const byte VERIFY_BCCERROR = 4;

        #endregion

        #region AGV信息定义
        public static string[] MarkFuncOpt;
        public static string[] StatusOpt;
        public static SortedList<int, SpeedStr> SpeedOpt = null;
        public Brush[] ColorOpt = { Brushes.Yellow, Brushes.Tomato, Brushes.Purple, Brushes.Pink, Brushes.SkyBlue, Brushes.LightBlue, Brushes.GreenYellow, Brushes.Goldenrod };
        AGVTrafficList agvTrafficList = null;
        AGVDock agvDock = null;
        AGVCharge agvCharge = null;
        AGVCall agvCall = null;

        #endregion

        #region 串口信息定义

        #region AGV监控串口
        /// <summary>
        /// AGV监控串口
        /// </summary>
        private COM.SerialPortWrapper SPComControl;

        /// <summary>
        /// 读取AGV车辆信息buffer，默认分配1/4页1K内存，并始终限制不允许超过 
        /// </summary>
        private List<byte> buffer_readcontrol = new List<byte>(1024);

        /// <summary>
        /// 缓冲区中可读的字节数
        /// </summary>
        private int readcount_control = 0;

        /// <summary>
        /// AGV交通管制命令
        /// </summary>
        byte[] buf_trafficctl = { 0x10, 0x61, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03 };

        /// <summary>
        /// 交通管制返回AGV状态信息
        /// </summary>
        byte[] buf_rettraffic = { 0x10, 0x62, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03 };

        /// <summary>
        /// AGV运行控制命令缓冲区，动态命令
        /// </summary>
        List<byte> buf_runctl = new List<byte>();

        /// <summary>
        /// AGV发送控制命令重复的次数
        /// </summary>
        List<byte> ctrlWaitNum = new List<byte>();

        /// <summary>
        /// 虚拟交通管制命令(用于判断是否断线，未接收到数据时虚拟发送数据，使agv状态断线)
        /// </summary>
        byte[] buf_virtualtrafficctl = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        /// <summary>
        /// 接收数据计数器，用于断线后自动更新数据
        /// </summary>
        byte[] data_total;

        /// <summary>
        /// 初试状态运行
        /// </summary>
        private Int64 SendD1D2 = 0L;

        //返回AGV运行控制
        //byte[] buf_retrun = { 0x10, 0x72, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03 };
        //public int iReceivedNum = 0;//接收数据的次数
        //public int iLastReceivedNum = 0;
        //public int IsSendCtrl = 0;
        //public int CtrlWaitNum = 0;

        #endregion

        #region AGV叫料串口

        /// <summary>
        /// AGV叫料串口
        /// </summary>
        private COM.SerialPortWrapper SPComCall;

        /// <summary>
        /// 叫料系统缓冲区中可读的字节数
        /// </summary>
        private int readcount_call = 0;

        /// <summary>
        /// 叫料系统信息buffer，默认分配1/8页0.5K内存，并始终限制不允许超过 
        /// </summary>
        private List<byte> buffer_readcall = new List<byte>(512);

        /// <summary>
        /// 叫料系统接收数据缓冲区
        /// </summary>
        private byte[] buf_callctl = { 0x10, 0x00, 0x00, 0x00, 0x00, 0x03 };

        /// <summary>
        /// 叫料系统发送数据缓冲区
        /// </summary>
        private byte[] buf_callret = { 0x10, 0x00, 0x00, 0x00, 0x00, 0x03 };
        #endregion

        #endregion

        #region 数据库定义W
        #endregion

        #region 动画定义
        DispatcherTimer pageShift = new DispatcherTimer();
        DispatcherTimer DataTimer = new DispatcherTimer();
        ObservableCollection<AGVCar> AGVStatus = new ObservableCollection<AGVCar>();
        #endregion

        #region 系统定义
        private object syncRoot = new object();
        private ServiceHost _host = null;
        private bool IsOpenSystem = false;
        private bool disposed;
        #endregion
        #endregion

        #region 窗体创建与销毁

        public MainWindow()
        {
            NameScope.SetNameScope(this, new NameScope());
            InitializeComponent();
            try
            {
                ReadConfigFile();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            data_total = new byte[AGVNUM_MAX];
        }

        private void ReadConfigFile()
        {
            try
            {
                AGVNUM_MAX = Convert.ToByte(ConfigurationManager.AppSettings["AGVNUM_MAX"]);
                GlobalPara.Gcontrolcomname = ConfigurationManager.AppSettings["ControlCOMName"];
                GlobalPara.Gcallcomname = ConfigurationManager.AppSettings["CallCOMName"];
                GlobalPara.IsTrafficFun = Convert.ToBoolean(ConfigurationManager.AppSettings["TRAFFICFUN"]);
                GlobalPara.IsDockFun = Convert.ToBoolean(ConfigurationManager.AppSettings["DOCKFUN"]);
                GlobalPara.IsChargeFun = Convert.ToBoolean(ConfigurationManager.AppSettings["CHARGEFUN"]);
                GlobalPara.IsCallFun = Convert.ToBoolean(ConfigurationManager.AppSettings["CALLFUN"]);
                GlobalPara.IsClientFun = Convert.ToBoolean(ConfigurationManager.AppSettings["CLIENTFUN"]);
                GlobalPara.PageShiftInterval = Convert.ToByte(ConfigurationManager.AppSettings["PAGESHIFTINTERVAL"]);
                GlobalPara.MapWidth = Convert.ToInt32(ConfigurationManager.AppSettings["MAPWIDTH"]);
                GlobalPara.MapHeight = Convert.ToInt32(ConfigurationManager.AppSettings["MAPHEIGHT"]);
            }
            catch
            {
                throw new Exception("读取配置文件失败！！！");
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //RegisterDeviceNotification();//注册加密锁事件插拨通知
                MenuBarInit();//菜单栏初始化
                StatusBarInit();//状态栏初始化
                MapInit();//地图初始化
                AGVGridInit(AGVNUM_MAX);//界面表格初始化
                ControlAreaInit();//控制面板初始化
                CustomInit();//自定义参数获取初始化
                DrvWlConInit();//交通管制区初始化
                TimerInit();//定时器初始化
                MessageService.ClientCallbackList.CollectionChanged += ClientCallbackList_CollectionChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "请配置好再启动系统！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void ClientCallbackList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                lblclientnum.Content = MessageService.ClientCallbackList.Count;
                foreach (IClient obj in e.NewItems)
                {
                    foreach (var item in AGVStatus)
                    {
                        obj.SendCarMessage(item.Convert2WCF());
                    }
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                IClient obj = e.OldItems[0] as IClient;
                App.Current.Dispatcher.Invoke(new Action(delegate
                {
                    lblclientnum.Content = MessageService.ClientCallbackList.Count;
                }));
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            IsOpenSystem = true;
            btn_CloseSystem_Click(null, null);
            IsOpenSystem = false;
            if (null != this.pageShift)
            {
                this.pageShift.Stop();
                this.pageShift.Tick -= this.PageShift_Tick;
            }
            // Stop timer
            if (null != this.DataTimer)
            {
                this.DataTimer.Stop();
                this.DataTimer.Tick -= this.SendDataTimerTick;
            }

            // Stop Com,Unregister SPComControl data received event
            if (null != SPComControl)
            {
                this.SPComControl.Close();
                this.SPComControl.OnDataReceived -= this.SPComControl_OnDataReceived;
            }

            if (_host != null)
            {
                _host.Close();
                IDisposable host = _host as IDisposable;
                host.Dispose();
            }
            Configuration cfa = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            cfa.AppSettings.Settings["PAGESHIFTINTERVAL"].Value = GlobalPara.PageShiftInterval.ToString();
            cfa.Save(ConfigurationSaveMode.Modified);
        }

        /// <summary>
        /// Finalizes an instance of the MainWindow class.
        /// This destructor will run only if the Dispose method does not get called.
        /// </summary>
        ~MainWindow()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Frees all memory associated with the FusionImageFrame.
        /// </summary>
        /// <param name="disposing">Whether the function was called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (null != this.SPComControl)
                    {
                        this.SPComControl.Dispose();
                    }
                }
            }

            this.disposed = true;
        }
        #endregion

        #region 初始化
        /// <summary>
        /// 状态栏初始化设置
        /// </summary>
        private void StatusBarInit()
        {
            LaunchTimer();//状态栏本地系统时间
            lblusername.Content = GlobalPara.strName;//状态栏用户名
        }

        /// <summary>
        /// 菜单栏权限设置初始化
        /// </summary>
        private void MenuBarInit()
        {
            if (!GlobalPara.IsManager)
            {
                MenuAGVManager.Visibility = Visibility.Collapsed;
                MenuCallManage.Visibility = Visibility.Collapsed;
                MenuSettings.Visibility = Visibility.Collapsed;
                MenuSystemManage.Visibility = Visibility.Collapsed;
            }
            if (!GlobalPara.IsCallFun)
            {
                MenuCallManage.Visibility = Visibility.Collapsed;
                MCallCOM.Visibility = Visibility.Collapsed;
            }
            if (!GlobalPara.IsDockFun)
            {
                DockArea.Visibility = Visibility.Collapsed;
            }
            if (!GlobalPara.IsChargeFun)
            {
                ChargeArea.Visibility = Visibility.Collapsed;
            }
            if (!GlobalPara.IsTrafficFun)
            {
                Traffic.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 定时器初始化
        /// </summary>
        private void TimerInit()
        {
            pageShift.Interval = TimeSpan.FromSeconds(GlobalPara.PageShiftInterval);
            pageShift.Tick += new EventHandler(PageShift_Tick);
            //PC与串口扩展板每次通信周期650+其他ms
            DataTimer.Interval = TimeSpan.FromMilliseconds(650);//以前550ms
            DataTimer.Tick += new EventHandler(this.SendDataTimerTick);
        }

        /// <summary>
        /// 自定义初始化
        /// </summary>
        private void CustomInit()
        {
            DAL.ZSql sql1 = new DAL.ZSql();
            try
            {
                //地标功能定义
                sql1.Open("select CmdFunction from T_Custom where CustomType=1 order by CmdNum");
                if (sql1.rowcount > 0)
                {
                    MarkFuncOpt = new string[sql1.rowcount];
                    for (int i = 0; i < sql1.rowcount; i++)
                    {
                        MarkFuncOpt[i] = sql1.Rows[i]["CmdFunction"].ToString();
                    }
                }
                else
                {
                    MessageBox.Show("警告：请在“自定义设置”中设置地标功能！");
                }
                //运行状态定义
                sql1.Open("select CmdFunction from T_Custom where CustomType=2 order by CmdNum");
                if (sql1.rowcount > 0)
                {
                    StatusOpt = new string[sql1.rowcount];
                    for (int i = 0; i < sql1.rowcount; i++)
                    {
                        StatusOpt[i] = sql1.Rows[i]["CmdFunction"].ToString();
                    }
                }
                else
                {
                    MessageBox.Show("警告：请在“自定义设置”中设置AGV运行状态！");
                }
                //速度定义
                sql1.Open("select SpeedGrade,Speed,CmdNum from T_Speed order by CmdNum");
                if (sql1.rowcount > 0)
                {
                    SpeedOpt = new SortedList<int, SpeedStr>();
                    for (int i = 0; i < sql1.rowcount; i++)
                    {
                        string speedgrade = sql1.Rows[i]["SpeedGrade"].ToString();
                        int cmdnum;
                        Int32.TryParse(sql1.Rows[i]["CmdNum"].ToString(),out cmdnum);
                        double speed;
                        Double.TryParse(sql1.Rows[i]["Speed"].ToString(),out speed);
                        SpeedOpt.Add(cmdnum,new SpeedStr(cmdnum,speed,speedgrade));
                    }
                }
                else
                {
                    MessageBox.Show("请在“速度设置”中设置AGV速度信息！","错误",MessageBoxButton.OK,MessageBoxImage.Error);
                }

            }
            catch (Exception ex)
            {              
                throw new Exception("初始化定义读取数据库失败！"+ ex.Message);
            }
            finally 
            {
                sql1.Close();
                sql1 = null;
            }
        }

        /// <summary>
        /// 控制面板中数据初始化
        /// </summary>
        private void ControlAreaInit()
        {
            //小车编号加载初始化
            for (int i = 0; i < AGVNUM_MAX; i++)
            {
                cb_AgvNum.Items.Add("AGV" + (i + 1).ToString());
            }
            //线路加载初始化
            DAL.ZSql sql1 = new DAL.ZSql();
            sql1.Open("select DISTINCT LineNum from T_Line order by LineNum");
            cb_LineNum.ItemsSource = sql1.m_table.DefaultView;
            cb_LineNum.DisplayMemberPath = "LineNum";
            cb_LineNum.SelectedValuePath = "LineNum";
            cb_LineNum.SelectedValue = "0";
            sql1.Close();
        }

        /// <summary>
        /// 状态栏定时器
        /// </summary>
        private void LaunchTimer()
        {
            DispatcherTimer innerTimer = new DispatcherTimer(TimeSpan.FromSeconds(1.0),
                    DispatcherPriority.Loaded, new EventHandler(this.TimerTick), this.Dispatcher);
            innerTimer.Start();
        }

        /// <summary>
        ///  状态栏定时器触发函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimerTick(object sender, EventArgs e)
        {
            lblTime.Content = DateTime.Now.ToString();
        }

        /// <summary>
        /// AGV监控串口初始化
        /// </summary>
        private void ControlCOMInit()
        {
            int controlcombaudrate = int.Parse(ConfigurationManager.AppSettings["ControlCOMBaudrate"]);
            int controlcomdatabits = int.Parse(ConfigurationManager.AppSettings["ControlCOMDataBits"]);
            string controlcomstopbits = ConfigurationManager.AppSettings["ControlCOMStopBits"];
            string controlcomparity = ConfigurationManager.AppSettings["ControlCOMParity"];
            try
            {
                SPComControl = new COM.SerialPortWrapper(GlobalPara.Gcontrolcomname, controlcombaudrate, controlcomparity, controlcomdatabits, controlcomstopbits);
                SPComControl.OnDataReceived += new SerialDataReceivedEventHandler(SPComControl_OnDataReceived);
                SPComControl.ReceivedBytesThreshold = buf_rettraffic.Length;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
        }

        /// <summary>
        /// AGV叫料串口初始化
        /// </summary>
        private void CallCOMInit()
        {
            int callcombaudrate = int.Parse(ConfigurationManager.AppSettings["CallCOMBaudrate"]);
            int callcomdatabits = int.Parse(ConfigurationManager.AppSettings["CallCOMDataBits"]);
            string callcomstopbits = ConfigurationManager.AppSettings["CallCOMStopBits"];
            string callcomparity = ConfigurationManager.AppSettings["CallCOMParity"];
            try
            {
                SPComCall = new COM.SerialPortWrapper(GlobalPara.Gcallcomname, callcombaudrate, callcomparity, callcomdatabits, callcomstopbits);
                SPComCall.OnDataReceived += new SerialDataReceivedEventHandler(SPComCall_OnDataReceived);
                SPComCall.ReceivedBytesThreshold = buf_callret.Length;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
        }

        /// <summary>
        /// 加载电子地图背景图片
        /// </summary>
        public void MapInit()
        {
            ImageBrush imageBrush = new ImageBrush();
            imageBrush.ImageSource = GlobalPara.mapImage;
            imageBrush.Stretch = Stretch.Uniform;
            imageBrush.AlignmentX = AlignmentX.Left;
            imageBrush.AlignmentY = AlignmentY.Top;
            canvas.Background = imageBrush;
            canvas.Height = GlobalPara.MapHeight;
            canvas.Width = GlobalPara.MapWidth;
            BitmapImage image = AGVUtils.GetImageFromFile(@"Image\mapbackground.png");
            if (image != null)
            {
                background.Source = image;
            }
            Canvas.SetLeft(marquee, canvas.ActualWidth / 2 - marquee.ActualWidth / 2);
        }



        /// <summary>
        /// agv表格绑定数据初始化
        /// </summary>
        /// <param name="agvnum"></param>
        private void AGVGridInit(int agvnum)
        {
            AGVAnimation.CarFElement = this;
            AGVAnimation.CarCanvas = canvas;
            AGVStatus.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(AGVStatus_CollectionChanged);
            dataGrid.DataContext = AGVStatus;
            tbpageinterval.Text = GlobalPara.PageShiftInterval.ToString();
        }

        private void AGVStatus_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                foreach (AGVCar obj in e.NewItems)
                {
                    obj.PropertyChanged += Dis_PropertyChange;
                }
            }
        }

        #endregion

        #region 事件委托触发更新
        /// <summary>
        /// 串口类SerialPortWrapper交通管制的数据接收触发委托函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void SPComControl_OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            readcount_control = SPComControl.BytesToRead;
            byte[] buf = new byte[readcount_control];
            SPComControl.Read(buf, 0, readcount_control);
            buffer_readcontrol.AddRange(buf);
            while (buffer_readcontrol.Count >= 11)
            {
                if (buffer_readcontrol[0] == 0x10 && buffer_readcontrol[1] == 0x62 && buffer_readcontrol[10] == 0x03)
                {
                    buffer_readcontrol.CopyTo(0, buf_rettraffic, 0, 11);
                    buffer_readcontrol.RemoveRange(0, 11);
                    ControlData_Processing(buf_rettraffic);
                }
                else
                {
                    buffer_readcontrol.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// AGV车辆返回信息处理过程
        /// </summary>
        /// <param name="buf">AGV车辆返回数据buffer</param>
        private void ControlData_Processing(byte[] buf)
        {
            AGVTemp agvCar = DrvWlCon_AgvStatus(buf);
            if (agvCar != null)
            {
                if (agvCar.WlLink == AGVWLLINK_OK)
                {
                    if (buf[1] == 0x62)
                    {
                        data_total[buf[2] - 1] = buf[2];
                        if (agvDock != null)
                        {
                            agvCar.DockNum = agvDock.UpdateDock(agvCar.WlLink,agvCar.DockNum,agvCar.WorklineNum,agvCar.MarkNum);
                        }
                        if (agvTrafficList != null)
                        {
                            TrafficRetParam retparam = agvTrafficList.UpdateTraffic(agvCar.WlLink,agvCar.AGVNum,agvCar.TrafficNum,agvCar.WorklineNum,agvCar.MarkNum);
                            agvCar.TrafficNum = retparam.trafficNum;
                            agvCar.TrafficStatus = retparam.trafficResult;
                            if (retparam.trafficResult)
                            {
                                SendD1D2 |= Convert.ToInt64(1 << (buf[2] - 1));
                            }
                            else
                            {
                                SendD1D2 &= Convert.ToInt64(~(1 << (buf[2] - 1)));
                            }
                        }
                    }

                    //判断运行控制是否回信息
                    if (buf_runctl.Count > 0)
                    {
                        if (buf[1] == 0x72)
                        {
                            for (int i = 0; i < ctrlWaitNum.Count; i++)
                            {
                                if (buf[2] == buf_runctl[i * buf_runctl.Count / ctrlWaitNum.Count + 2])
                                {
                                    buf_runctl.RemoveRange(buf_runctl.Count / ctrlWaitNum.Count * i, buf_runctl.Count / ctrlWaitNum.Count);//移除11个字节
                                    ctrlWaitNum.RemoveAt(i);
                                }
                            }
                        }
                    }
                }
                //数据有效
                App.Current.Dispatcher.Invoke(new UpdateAGVStatusDelegate(UpdateAGVStatus),agvCar);
            }


        }

        private delegate void UpdateAGVStatusDelegate(AGVTemp agvCar);
        private void UpdateAGVStatus(AGVTemp agvCar)
        {
            IEnumerable<AGVCar> member = AGVStatus.Where<AGVCar>(p => p.AGVNum == agvCar.AGVNum);
            int listindex;
            if (member.Count() == 0)
            {
                //WorklineNum避免异步，Add的时间过长，进入一个掉线的数据，界面出现new AGVCar()错误数据
                if (agvCar.AGVNum > 0 && agvCar.WorklineNum > 0)
                {
                    AGVStatus.Add(new AGVCar());
                    listindex = AGVStatus.Count - 1;
                }
                else
                {
                    return;
                }
            }
            else
            {
                AGVCar member1 = member.First<AGVCar>();
                listindex = AGVStatus.IndexOf(member1);
            }
            if (agvCar.WlLinkCount > 0)
            {
                AGVStatus[listindex].WlLinkCount += agvCar.WlLinkCount;//+1
                if (AGVStatus[listindex].WlLinkCount > 5)
                {
                    //第一次断线时，连接失败，更新交通管制参数，如果在交通管制区则出交通管制区
                    AGVStatus[listindex].MarkNum = 0;
                    AGVStatus[listindex].DockNum = 0;
                    AGVStatus[listindex].LineNum = 0;
                    if (agvTrafficList != null)
                    {
                        TrafficRetParam retparam = agvTrafficList.UpdateTraffic(AGVStatus[listindex].WlLink, AGVStatus[listindex].AGVNum,
                            AGVStatus[listindex].TrafficNum, AGVStatus[listindex].WorklineNum, AGVStatus[listindex].MarkNum);
                        AGVStatus[listindex].TrafficNum = retparam.trafficNum;
                        AGVStatus[listindex].TrafficStatus = retparam.trafficResult;
                    }
                    data_total[agvCar.AGVNum - 1] = 0x00;
                    AGVStatus[listindex].WlLink = AGVWLLINK_ERROR;//一定放在最后，先更新了管制区，然后再删除
                }
                return;
            }
            AGVStatus[listindex].WorklineNum = agvCar.WorklineNum;
            AGVStatus[listindex].DockNum = agvCar.DockNum;
            AGVStatus[listindex].AGVNum = agvCar.AGVNum;
            AGVStatus[listindex].TrafficNum = agvCar.TrafficNum;
            AGVStatus[listindex].TrafficStatus = agvCar.TrafficStatus;
            AGVStatus[listindex].LineNum = agvCar.LineNum;
            AGVStatus[listindex].MarkFunction = agvCar.MarkFunction;
            AGVStatus[listindex].AGVSpeed = agvCar.AGVSpeed;
            AGVStatus[listindex].MarkNum = agvCar.MarkNum;
            AGVStatus[listindex].AGVPower = agvCar.AGVPower;
            AGVStatus[listindex].AGVCharge = agvCar.AGVCharge;
            AGVStatus[listindex].WlLinkCount = agvCar.WlLinkCount;
            AGVStatus[listindex].WlLink = agvCar.WlLink;
            AGVStatus[listindex].AGVStatus = agvCar.AGVStatus;
        }

        /// <summary>
        /// 串口类SerialPortWrapper叫料系统的数据接收触发委托函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SPComCall_OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            readcount_call = SPComCall.BytesToRead;
            byte[] buf = new byte[readcount_call];
            SPComCall.Read(buf, 0, readcount_call);
            buffer_readcall.AddRange(buf);
            while (buffer_readcall.Count >= 6)
            {
                if (buffer_readcall[0] == 0x10 && buffer_readcall[5] == 0x03)
                {
                    buffer_readcall.CopyTo(0, buf_callctl, 0, 6);
                    buffer_readcall.RemoveRange(0, 6);
                    CallData_Processing(buf_callctl);
                }
                else
                {
                    buffer_readcall.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// 叫料信息处理过程
        /// </summary>
        /// <param name="buf">叫料信息数据buffer</param>
        private void CallData_Processing(byte[] buf)
        {
            if (CallDataCheck(buf, buf.Length - 2) == VERIFY_NOERROR)
            {
                if (buf[1] == 0x41)//叫料
                {
                    if (agvCall.Add(buf[2], buf[3]))
                    {
                        buf_callret[0] = 0x10;
                        buf_callret[1] = 0x43;
                        buf_callret[2] = buf[2];
                        buf_callret[3] = buf[3];
                        buf_callret[4] = COM.SerialPortWrapper.GetXORCheckCode(buf_callret, buf_callctl.Length - 2);
                        buf_callret[5] = 0x03;
                        SPComCall.Write(buf_callret, 0, buf_callret.Length);
                        if (agvDock != null)
                        {
                            if (agvDock.dockingCount > 0)
                            {
                                byte agvnum = agvDock.GetDockCarNum();
                                if (agvnum > 0 && agvCall.lineNum.Count > 0)
                                {
                                    if (agvCall.lineNum[0] > 0)
                                    {
                                        agvDock.outDockLine[agvnum - 1] = agvCall.lineNum[0];
                                        AGVControlCommand(agvnum, 3, 0, 0);
                                    }
                                }
                            }
                        }
                        else
                        {
                            //agvCall.memberData.
                            //先判断是否到达起点位置
                            //到达后再启动AGV
                            //没有达到时需要叫料排队，等到到达再启动
                        }
                    }
                }
                else if (buf[1] == 0x42)//取消叫料
                {
                    if (agvCall.Delete(buf[2], buf[3]))
                    {
                        buf_callret[0] = 0x10;
                        buf_callret[1] = 0x45;
                        buf_callret[2] = buf[2];
                        buf_callret[3] = buf[3];
                        buf_callret[4] = COM.SerialPortWrapper.GetXORCheckCode(buf_callret, buf_callctl.Length - 2);
                        buf_callret[5] = 0x03;
                        SPComCall.Write(buf_callret, 0, buf_callret.Length);
                    }
                }
            }
        }

        public void QueryCall(byte stationmum, byte materialnum)
        {

        }

        /// <summary>
        /// 数据格式效验
        ///  0x10 功能码 工位号 异或效验 0x03
        ///  功能码（0x41 叫料 0x42 取消叫料 0x43 叫料成功 0x44 设置工位号成功 0x45 取消叫料成功））
        /// </summary>
        /// <param name="buf">接收帧数据</param>
        /// <returns>返回效验结果</returns>
        public static int CallDataCheck(byte[] buf, int length)
        {
            if (buf[0] != 0x10)
            {
                return VERIFY_HEADERROR;//头错误
            }
            else if ((buf[buf.Length - 1]) != 0x03)
            {
                return VERIFY_ENDERROR;//尾错误
            }
            else if (buf[1] != 0x41 && buf[1] != 0x42 && buf[1] != 0x43 && buf[1] != 0x44 && buf[1] != 0x45)
            {
                return VERIFY_FUNERROR;
            }
            else if (COM.SerialPortWrapper.GetXORCheckCode(buf, length) != buf[buf.Length - 2])
            {
                return VERIFY_BCCERROR;// 校验错误 
            }
            else
            {
                return VERIFY_NOERROR;
            }
        }

        /// <summary>
        /// 发送数据定时器
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendDataTimerTick(object sender, EventArgs e)
        {
            if (ctrlWaitNum.Count > 0)
            {
                for (int i = 0; i < ctrlWaitNum.Count; i++)
                {
                    if (ctrlWaitNum[i]++ < 3)
                    {
                        SPComControl.Write(buf_runctl.ToArray(), buf_runctl.Count / ctrlWaitNum.Count * i, buf_runctl.Count / ctrlWaitNum.Count);
                    }
                    else
                    {
                        buf_runctl.RemoveRange(buf_runctl.Count / ctrlWaitNum.Count * i, buf_runctl.Count / ctrlWaitNum.Count);//移除11个字节
                        ctrlWaitNum.RemoveAt(i);
                    }
                    Thread.Sleep(20);
                }
                //线程挂起100ms，等待接收数据中断（挂起时，其他线程运行）
                Thread.Sleep(80);
            }
            for (int i = 0; i < data_total.Length; i++)
            {
                if (data_total[i] == 0xFF)
                {
                    Array.Clear(buf_virtualtrafficctl, 0, buf_virtualtrafficctl.Length);
                    buf_virtualtrafficctl[2] = Convert.ToByte(i + 1);
                    ControlData_Processing(buf_virtualtrafficctl);
                }
                else if (data_total[i] > 0 && data_total[i] < AGVNUM_MAX)
                {
                    data_total[i] = 0xFF;
                }
            }
            {
                buf_trafficctl[0] = 0x10;
                buf_trafficctl[1] = 0x61;
                buf_trafficctl[2] = Convert.ToByte(SendD1D2 & 0x00000000000000ff);
                buf_trafficctl[3] = Convert.ToByte((SendD1D2 & 0x000000000000ffff) >> 8);
                buf_trafficctl[4] = Convert.ToByte((SendD1D2 & 0x0000000000ffffff) >> 16);
                buf_trafficctl[5] = Convert.ToByte((SendD1D2 & 0x00000000ffffffff) >> 24);
                buf_trafficctl[6] = Convert.ToByte((SendD1D2 & 0x000000ffffffffff) >> 32);
                buf_trafficctl[7] = Convert.ToByte((SendD1D2 & 0x0000ffffffffffff) >> 40);
                buf_trafficctl[8] = Convert.ToByte((SendD1D2 & 0x00ffffffffffffff) >> 48);
                buf_trafficctl[9] = COM.SerialPortWrapper.GetXORCheckCode(buf_trafficctl, buf_trafficctl.Length - 2);
                buf_trafficctl[10] = 0x03;
                SPComControl.Write(buf_trafficctl, 0, buf_trafficctl.Length);
                Thread.Sleep(5);
            }
        }

        public void PageShift_Tick(object sender, EventArgs e)
        {
            if (Panel.GetZIndex(canvas) > 0)
            {
                Panel.SetZIndex(canvas, 0);
                Panel.SetZIndex(dataGrid, 3);
            }
            else
            {
                Panel.SetZIndex(canvas, 3);
                Panel.SetZIndex(dataGrid, 0);
            }

        }

        /// <summary>
        /// 界面显示类AGV_DisMember属性更改委托函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Dis_PropertyChange(object sender, PropertyChangedEventArgs e)
        {
            AGVCar agvmember = (AGVCar)sender;
            bool sendmember = false;
            if (e.PropertyName.Equals("MarkNum"))
            {
                MarkChangeAction(agvmember);
            }
            else if (e.PropertyName.Equals("AGVStatus"))
            {
                StatusChangeAction(agvmember);
            }
            else if (e.PropertyName.Equals("WlLink"))
            {
                WLChangeAction(agvmember);
                //当无线连接成功时，发送AGV的属性信息到客户端
                //当其他属性更改时，只发送变化的属性到客户端
                if (agvmember.WlLink)
                {
                    sendmember = true;
                }
            }
            else if (e.PropertyName == "AGVPower")
            {
                PowerChangeAction((AGVCar)sender);
            }
            var list = WcfDuplexMessageService.MessageService.ClientCallbackList;
            if (list == null || list.Count == 0 || typeof(AGVCar_WCF).GetProperty(e.PropertyName) == null)
                return;
            object value = typeof(AGVCar).GetProperty(e.PropertyName).GetValue(agvmember,null);
            PropertyChangedMessage message = new PropertyChangedMessage(agvmember.AGVNum, e.PropertyName, value);
            lock (list)
            {
                foreach (var client in list)
                {
                    // Broadcast
                    if (sendmember)
                    {
                        client.SendCarMessage(agvmember.Convert2WCF());
                    }
                    else
                    {
                        client.SendPropertyChangedMessage(message);
                    }
                }
            }
        }

        /// <summary>
        /// 地标属性更改时触发的事件
        /// 移除上次动画，显示下一动画
        /// </summary>
        /// <param name="temp1">AGV_DisMember类</param>
        private void MarkChangeAction(AGVCar temp1)
        {
            IEnumerable<AGVCar> member = AGVStatus.Where<AGVCar>(p => p.AGVNum == temp1.AGVNum);
            AGVCar member1 = member.First<AGVCar>();
            int listindex = AGVStatus.IndexOf(member1);
            Point pStart = new Point(), pVirtual = new Point();

            //待测试
            //if (agvDock != null && agvCharge != null && agvCall != null)
            //{
            //    if (Convert.ToInt32(temp1.txtDockNum) == 0)
            //    {
            //        if (agvDock.dockStartStop.Equals(new WorkMarkStr(Convert.ToInt32(temp1.txtWorkLine), Convert.ToInt32(temp1.txtMarkNum))))
            //        {
            //            //充电返回后，清除状态
            //            if (temp1.txtagvCharge == 4) { AGVStatus[iagvnum - 1].agvCharge = 0; }
            //            CarLine carline = agvDock.Add(iagvnum);
            //            if (carline != null)
            //            {
            //                temp1.txtDockNum = carline.dockNum.ToString();
            //                temp1.txtLineNum = carline.lineNum.ToString();
            //                AGVStatus[iagvnum - 1].dockNum = carline.dockNum;
            //                //启动车辆，发送变更为待装停靠区路线
            //                //进入待装区时，起始地标的功能为暂停，结束地标不能为停止或暂停，一定为agv运行的地标功能，也不能在交通管制点内
            //                AGVControlCommand(iagvnum, 3, 0, Convert.ToByte(carline.lineNum));
            //            }
            //            else
            //            {
            //                //这是在排队队列中，此时需要车辆停止（发送停止命令）
            //                AGVControlCommand(iagvnum, 2, 0, 0);
            //            }
            //        }
            //    }
            //    else
            //    {
            //        if (agvDock.dockEndStop.Equals(new WorkMarkStr(Convert.ToInt32(temp1.txtWorkLine), Convert.ToInt32(temp1.txtMarkNum))))
            //        {
            //            //移除待装区排队
            //            CarLine carline = agvDock.Delete(iagvnum);
            //            if (carline != null)
            //            {
            //                //只有停靠区和管制区在这边需要软件更新，其他的更新可以发送控制命令，下位机更改AGV状态，上位机接收数据驱动更新
            //                AGVStatus[carline.agvNum - 1].dockNum = carline.dockNum;
            //                //如果在等待排队中有车辆，启动等待的车辆
            //                AGVControlCommand(carline.agvNum, 3, 0, Convert.ToByte(carline.lineNum));
            //            }
            //            temp1.txtDockNum = "0";
            //            AGVStatus[iagvnum - 1].dockNum = 0;
            //            //此时需要变换路线agvDock.outDockLine[iagvnum-1],发出控制命令，变更路线
            //            //1.当监测到temp1.txtagvCharge ==1时，outDockLine[iagvnum-1]为充电路线
            //            //2.当temp1.txtagvCharge ==0时，为执行任务的路线
            //            if (temp1.txtagvCharge == 1)
            //            {
            //                AGVStatus[iagvnum - 1].agvCharge = 2;
            //            }
            //            AGVControlCommand(iagvnum, 0, 0, Convert.ToByte(agvDock.outDockLine[iagvnum - 1]));
            //            agvDock.outDockLine[iagvnum - 1] = 0;
            //        }
            //    }

            //    if (temp1.txtagvCharge == 2)
            //    {
            //        if (agvCharge.dockStartStop.Equals(new WorkMarkStr(Convert.ToInt32(temp1.txtWorkLine), Convert.ToInt32(temp1.txtMarkNum))))
            //        {
            //            CarLine carline = agvCharge.Add(iagvnum);
            //            if (carline != null)
            //            {
            //                temp1.txtagvCharge = 3;//充电状态
            //                AGVStatus[iagvnum - 1].agvCharge = 3;//充电状态
            //                temp1.txtLineNum = carline.lineNum.ToString();
            //                //启动车辆，发送变更路线
            //                AGVControlCommand(iagvnum, 3, 0, Convert.ToByte(carline.lineNum));
            //            }
            //            else
            //            {
            //                //这是在排队队列中，此时需要车辆停止（发送停止命令）
            //                AGVControlCommand(iagvnum, 2, 0, 0);
            //            }
            //        }
            //    }//注意：充电完成，车辆不会自行启动，需要人工拔掉充电线，然后启动车辆
            //    else if (temp1.txtagvCharge == 3)
            //    {
            //        if (agvCharge.dockEndStop.Equals(new WorkMarkStr(Convert.ToInt32(temp1.txtWorkLine), Convert.ToInt32(temp1.txtMarkNum))))
            //        {
            //            //移除待装区排队
            //            CarLine carline = agvCharge.Delete(iagvnum);
            //            if (carline != null)
            //            {
            //                //只有停靠区和管制区在这边需要软件更新，其他的更新可以发送控制命令，下位机更改AGV状态，上位机接收数据驱动更新
            //                AGVStatus[carline.agvNum - 1].dockNum = carline.dockNum;
            //                //如果在等待排队(此时AGV车辆在待装区中排队，不是在充电区的起始地标排队)中有车辆，启动等待的车辆
            //                AGVControlCommand(carline.agvNum, 3, 0, 0);
            //                agvDock.outDockLine[iagvnum - 1] = agvCharge.chargeLine;//在出待装区的时候改变路线使用
            //            }
            //            temp1.txtagvCharge = 4;
            //            AGVStatus[iagvnum - 1].agvCharge = 4;
            //        }
            //    }
            //}

            #region 停靠区
            if (agvDock != null)
            {
                // 判断是否进入停靠区
                if (temp1.DockNum == 0)
                {
                    if (agvDock.dockStartStop.Equals(new WorkMarkStr(temp1.WorklineNum, temp1.MarkNum)))
                    {
                        //已经进入停靠区：1.更新AGV的停靠区参数 2.更新AGV的路线

                        //车辆进入停靠区
                        CarLine carline = agvDock.Add(temp1.AGVNum);
                        if (carline != null)
                        {
                            temp1.DockNum = carline.dockNum;
                            temp1.LineNum = carline.lineNum;
                            //更新停靠区参数
                            AGVStatus[listindex].DockNum = carline.dockNum;
                            //启动车辆，发送变更为待装停靠区路线
                            //进入待装区时，起始地标的功能为暂停，结束地标不能为停止或暂停，一定为agv运行的地标功能，也不能在交通管制点内
                            AGVControlCommand(temp1.AGVNum, 3, 0, carline.lineNum);
                        }
                        else
                        {
                            //这是在排队队列中，此时需要车辆停止（发送停止命令）
                            AGVControlCommand(temp1.AGVNum, 2, 0, 0);
                        }
                        //启用充电区
                        if (agvCharge != null)
                        {
                            //充电返回后，清除状态
                            if (temp1.AGVCharge == 4) { AGVStatus[listindex].AGVCharge = 0; }
                        }
                        else//不启用充电区
                        {
                        }
                    }
                    else
                    {
                        //未进入停靠区，一直处于停靠区外面；暂不做处理
                    }
                }
                else//判断是否出停靠区
                {
                    if (agvDock.dockEndStop.Equals(new WorkMarkStr(temp1.WorklineNum, temp1.MarkNum)))
                    {
                        //离开停靠区：1.更新AGV的停靠区参数 2.更新AGV的路线

                        //移除待装区排队
                        CarLine carline = agvDock.Delete(temp1.AGVNum);
                        //更新AGV停靠区参数
                        temp1.DockNum = 0;
                        AGVStatus[listindex].DockNum = 0;
                        //更新AGV路线
                        //有车辆等待排队
                        if (carline != null)
                        {
                            //只有停靠区和管制区在这边需要软件更新，其他的更新可以发送控制命令，下位机更改AGV状态，上位机接收数据驱动更新
                            IEnumerable<AGVCar> dockmember = AGVStatus.Where<AGVCar>(p => p.AGVNum == carline.agvNum);
                            int indexfind = AGVStatus.IndexOf(dockmember.First());
                            AGVStatus[indexfind].DockNum = carline.dockNum;
                            //如果在等待排队中有车辆，启动等待的车辆
                            AGVControlCommand(carline.agvNum, 3, 0, carline.lineNum);
                        }
                        //启用充电区
                        if (agvCharge != null)
                        {
                            if (temp1.AGVCharge == 1)
                            {
                                byte linenum = agvCharge.GetChargeLine(temp1.LineNum);
                                //充电路线，当linenum=0,说明用户没有定义
                                agvDock.outDockLine[temp1.AGVNum - 1] = linenum;//充电路线
                                if (linenum > 0)
                                {
                                    AGVStatus[listindex].AGVCharge = 2;
                                }
                            }
                        }
                        else
                        {
                            //不启动充电区
                        }

                        //车辆驶出停靠区后变更路线
                        //此时需要变换路线agvDock.outDockLine[iagvnum-1],发出控制命令，变更路线
                        //1.当监测到temp1.txtagvCharge ==1时，outDockLine[iagvnum-1]为充电路线
                        //2.当temp1.txtagvCharge ==0时，为执行任务的路线
                        if (agvDock.outDockLine[temp1.AGVNum - 1] > 0)
                        {
                            AGVControlCommand(temp1.AGVNum, 0, 0, agvDock.outDockLine[temp1.AGVNum - 1]);
                            agvDock.outDockLine[temp1.AGVNum - 1] = 0;
                        }

                    }
                    else
                    {
                        //已经进入停靠区，但一直在停靠区内；暂不做处理
                    }
                }
            }
            #endregion

            #region 充电区
            if (agvCharge != null)
            {
                if (temp1.AGVCharge == 2)
                {
                    if (agvCharge.dockStartStop.Equals(new WorkMarkStr(temp1.WorklineNum, temp1.MarkNum)))
                    {
                        CarLine carline = agvCharge.Add(temp1.AGVNum, temp1.LineNum);
                        if (carline != null)
                        {
                            temp1.AGVCharge = 3;//充电状态
                            AGVStatus[listindex].AGVCharge = 3;//充电状态
                            temp1.LineNum = carline.lineNum;
                            //启动车辆，发送变更路线
                            AGVControlCommand(temp1.AGVNum, 3, 0, carline.lineNum);
                        }
                        else
                        {
                            //这是在排队队列中，此时需要车辆停止（发送停止命令）
                            AGVControlCommand(temp1.AGVNum, 2, 0, 0);
                        }
                    }
                }//注意：充电完成，车辆不会自行启动，需要人工拔掉充电线，然后启动车辆
                else if (temp1.AGVCharge == 3)
                {
                    if (agvCharge.dockEndStop.Equals(new WorkMarkStr(temp1.WorklineNum, temp1.MarkNum)))
                    {
                        //移除待装区排队
                        CarLine carline = agvCharge.Delete(temp1.AGVNum);
                        if (carline != null)
                        {
                            //只有停靠区和管制区在这边需要软件更新，其他的更新可以发送控制命令，下位机更改AGV状态，上位机接收数据驱动更新
                            IEnumerable<AGVCar> dockmember = AGVStatus.Where<AGVCar>(p => p.AGVNum == carline.agvNum);
                            int indexfind = AGVStatus.IndexOf(dockmember.First());
                            AGVStatus[indexfind].DockNum = carline.dockNum;
                            //如果在等待排队(此时AGV车辆在待装区中排队，不是在充电区的起始地标排队)中有车辆，启动等待的车辆
                            temp1.LineNum = carline.lineNum;
                            AGVControlCommand(carline.agvNum, 3, 0, carline.lineNum);
                        }
                        temp1.AGVCharge = 4;
                        AGVStatus[listindex].AGVCharge = 4;
                        AGVControlCommand(temp1.AGVNum, 0, 0, agvCharge.beforeEnterLine[temp1.DockNum]);
                    }
                }
            }
            #endregion


            DAL.ZSql TrafficPara = new DAL.ZSql();

            TrafficPara.Open("select T_Line.MarkOrder, T_Mark.XPos, T_Mark.YPos FROM T_Line LEFT OUTER JOIN T_Mark ON T_Mark.ID = T_Line.MarkID where WorkLine=" + temp1.WorklineNum.ToString() + " and Mark=" + temp1.MarkNum.ToString() + " and LineNum=" + temp1.LineNum.ToString());
            if (TrafficPara.Rows.Count > 0)
            {
                pStart.X = Convert.ToDouble(TrafficPara.Rows[0]["XPos"]);
                pStart.Y = Convert.ToDouble(TrafficPara.Rows[0]["YPos"]);
                List<Point> pointcollection1 = new List<Point>();
                pointcollection1.Add(pStart);
                int currentOrder = Convert.ToInt16(TrafficPara.Rows[0]["MarkOrder"]) + 1;
                //线路起点处检测电量
                if (currentOrder == 2)
                {
                    //AGV到达起点位置，空闲状态；当检测到AGV状态为运行时，为执行任务
                    AGVStatus[listindex].AGVTask = 0;
                    //启用充电区
                    if (agvCharge != null)
                    {
                        if (temp1.AGVCharge == 1)
                        {
                            byte linenum = agvCharge.GetChargeLine(temp1.LineNum);
                            //当linenum=0时，说明管理员没有设置充电路线，不会启动自动充电
                            if (linenum > 0)
                            {
                                AGVControlCommand(temp1.AGVNum, 1, 0, linenum);
                                AGVStatus[listindex].AGVCharge = 2;
                            }
                        }
                    }
                }
                bool isvirtualpoint = true;
                double dMarkdistance = 0;
                do
                {
                    TrafficPara.Open("select XPos,YPos,T_Line.Distance,VirtualMark from T_Mark Left join T_Line on T_Mark.ID = T_Line.MarkID where T_Line.MarkOrder=" + currentOrder.ToString() +
                    "and LineNum=" + temp1.LineNum.ToString());
                    if (TrafficPara.Rows.Count > 0)
                    {
                        pVirtual.X = Convert.ToDouble(TrafficPara.Rows[0]["XPos"]);
                        pVirtual.Y = Convert.ToDouble(TrafficPara.Rows[0]["YPos"]);
                        pointcollection1.Add(pVirtual);
                        dMarkdistance += Convert.ToDouble(TrafficPara.Rows[0]["Distance"]);
                        isvirtualpoint = Convert.ToBoolean(TrafficPara.Rows[0]["VirtualMark"]);
                        currentOrder++;
                    }
                    else
                    {
                        isvirtualpoint = false;
                        break;
                    }
                }
                while (isvirtualpoint);
                if (pointcollection1.Count >= 2)
                {
                    double dAgvspeed = SpeedOpt[temp1.AGVSpeed].Speed / 60.0;
                    if (dAgvspeed == 0)
                    {
                        dAgvspeed = dMarkdistance;
                    }
                    AGVStatus[listindex].agvAnimation.DrawCarLine(pointcollection1, ColorOpt[temp1.AGVNum % ColorOpt.Length], dMarkdistance / dAgvspeed);
                }
            }
            TrafficPara.Close();
        }

        /// <summary>
        /// 运行状态属性更改时触发的事件
        /// </summary>
        /// <param name="temp1">AGV_DisMember类</param>
        private void StatusChangeAction(AGVCar temp1)
        {
            IEnumerable<AGVCar> member = AGVStatus.Where<AGVCar>(p => p.AGVNum == temp1.AGVNum);
            AGVCar member1 = member.First<AGVCar>();
            int listindex = AGVStatus.IndexOf(member1);
            AGVStatus[listindex].agvAnimation.StatusChangeAnimation(temp1.AGVStatus, temp1.AGVNum, temp1.DockNum);
            switch (temp1.AGVStatus)
            {
                case 0x40://"运行":
                    AGVStatus[listindex].AGVTask = 1;
                    break;
                case 0x41://"暂停":
                    break;
                case 0x42://"结束地标停止":
                    //if (agvCall.lineNum.Count > 0)//有叫料信息，需要车辆运送
                    //{
                    //    if (agvCall.lineNum[0] > 0)
                    //    {
                    //        agvDock.outDockLine[iagvnum - 1] = agvCall.lineNum[0];
                    //        AGVControlCommand(iagvnum, 3, 0, 0);
                    //    }
                    //}
                    break;
                default:
                    {
                        //将报警记录写入到数据库
                        string txttimer = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        DAL.ZSql TrafficPara = new DAL.ZSql();
                        TrafficPara.Open("Insert into T_Ex (CarID,ExTimer,ExType,ExRouteNum,ExMarkNum,ExWorkLine) VALUES (" + temp1.AGVNum.ToString() + ",'" + txttimer + "','" + StatusOpt[temp1.AGVStatus] + "'," + temp1.LineNum.ToString() + "," + temp1.MarkNum.ToString() + "," + temp1.WorklineNum.ToString() + ")");
                        TrafficPara.Close();
                        break;
                    }
            }
        }

        /// <summary>
        /// 无线连接属性更改时触发的事件
        /// </summary>
        /// <param name="temp1">AGV_DisMember类</param>
        private void WLChangeAction(AGVCar temp1)
        {
            IEnumerable<AGVCar> member = AGVStatus.Where<AGVCar>(p => p.AGVNum == temp1.AGVNum);
            AGVCar member1 = member.First<AGVCar>();
            int listindex = AGVStatus.IndexOf(member1);
            //此处不能用异步，需要等待动画后返回。因为后续有AGVStatus删除元素的操作
            AGVStatus[listindex].agvAnimation.WLChangeAnimation(temp1.WlLink);
            if (temp1.WlLink)
            {
                MarkChangeAction(temp1);
            }
            else
            {
                AGVStatus[listindex].agvAnimation.ClearAllElements();
                AGVStatus[listindex].agvAnimation = null;
                AGVStatus.Remove(temp1);
                if (agvCharge != null)
                {
                    agvCharge.Delete(temp1.AGVNum);
                }
                if (agvDock != null)
                {
                    agvDock.Delete(temp1.AGVNum);
                }
            }
        }

        /// <summary>
        /// 电量属性更改时触发的事件
        /// 电量低于20%时进行充电
        /// </summary>
        /// <param name="temp1">AGV_DisMember类</param>
        private void PowerChangeAction(AGVCar temp1)
        {
            IEnumerable<AGVCar> member = AGVStatus.Where<AGVCar>(p => p.AGVNum == temp1.AGVNum);
            AGVCar member1 = member.First<AGVCar>();
            int listindex = AGVStatus.IndexOf(member1);
            //20140312电量低于20%时进行充电，线路切换到充电线路
            if (temp1.AGVPower < 20)//电量低于20%时进行充电
            {
                //设置充电标志位
                AGVStatus[listindex].AGVCharge = 1;
            }

        }

        #endregion

        #region AGV控制区

        void DrvWlConInit()
        {
            if (GlobalPara.IsTrafficFun)
            {
                agvTrafficList = new AGVTrafficList();
                agvTrafficList.Init();//交通管制路口初始化
            }
            if (GlobalPara.IsDockFun)
            {
                agvDock = new AGVDock();
                agvDock.Init();
            }
            if (GlobalPara.IsChargeFun)
            {
                agvCharge = new AGVCharge();
                agvCharge.Init();
            }
            if (GlobalPara.IsCallFun)
            {
                agvCall = new AGVCall();
            }
        }

        /// <summary>
        /// 生产区初始化
        /// </summary>
        private byte DrvWlConWorkLineInit(byte agvid)
        {
            DAL.ZSql TrafficPara = new DAL.ZSql();
            try
            {
                Object workline = TrafficPara.GetScalar("select WorkLine from T_WorkLine Where CarID = " + agvid.ToString());
                if (workline == null)
                {
                    MessageBox.Show("AGV" + agvid.ToString() + "车辆未设置生产区号，启动系统前请设置后再重启软件！");
                    return 0;
                }
                return Convert.ToByte(workline);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("数据库连接异常,请检查后再重启软件！");
                return 0;
            }
            finally
            {
                TrafficPara.Close();
            }
        }

        byte DrvWlConCheck(byte[] buf, int length)   //判断一帧数据正确性
        {
            if ((buf[0]) != 0x10)
            {
                return VERIFY_HEADERROR;  //头错误
            }
            else if ((buf[buf.Length - 1]) != 0x03)
            {
                return VERIFY_ENDERROR;    //尾错误
            }
            else if ((buf[1]) != 0x62 && (buf[1]) != 0x72)  //功能码错误
            {
                return VERIFY_FUNERROR;
            }
            else if (COM.SerialPortWrapper.GetXORCheckCode(buf, length) != buf[buf.Length - 2])
            {
                return VERIFY_BCCERROR;  // 校验错误 
            }
            else
            {
                return VERIFY_NOERROR;
            }
        }

        private AGVTemp DrvWlCon_AgvStatus(byte[] buf)  //更新AGV运行参数
        {
            lock (syncRoot)
            {
                AGVTemp agvCar = new AGVTemp();
                byte SpeedGrade = 0;
                int bMarkNum = 0;
                byte AgvNum = buf[2];  //得到AGV编号，编号从0x01开始
                if (AgvNum > AGVNUM_MAX || AgvNum < 1)
                {
                    return null;//AGV编号错误
                }
                if (DrvWlConCheck(buf, buf.Length - 2) == VERIFY_NOERROR)  //校验无错
                {
                    agvCar.WorklineNum = DrvWlConWorkLineInit(AgvNum);
                    agvCar.WlLinkCount = 0;
                    //由于生产区在运行过程中不会改变，故将此放置在AGV的初始化中，不需要每次更新
                    agvCar.AGVNum = AgvNum;
                    //修改说明：地标号扩展两位，速度等级的bit8,bit7作为地标号的bit9，bit10
                    bMarkNum = buf[3] | ((buf[7] & 0xC0) << 2);
                    if (bMarkNum > 0)
                    {
                        //当前AGV运行路线
                        agvCar.LineNum = buf[6];
                        //当前地标功能
                        if (buf[4] > 0 && buf[4] < MarkFuncOpt.Length)
                        {
                            agvCar.MarkFunction = buf[4];
                        }
                        //当前AGV运行速度等级，不会接收到保持现状的0
                        //修改时间：2013-11-28
                        //修改说明：屏蔽速度等级的bit8,bit7
                        SpeedGrade = Convert.ToByte(buf[7] & 0x3F);
                        if (SpeedGrade > 0 && SpeedGrade < SpeedOpt.Count)
                        {
                            agvCar.AGVSpeed = SpeedGrade;
                        }
                        agvCar.MarkNum = bMarkNum;
                        //AGV当前剩余电量
                        if (buf[8] > 0 && buf[8] <= 100)
                        {
                            agvCar.AGVPower = buf[8];
                        }
                    }
                    //无线连接正常
                    agvCar.WlLink = AGVWLLINK_OK;
                    //当前AGV状态，由于接收到数据不是按照顺序，中间有间隔，在使用时才验证数据正确性 
                    agvCar.AGVStatus = buf[5];
                }
                else
                {
                    agvCar.AGVNum = AgvNum;
                    agvCar.WlLinkCount = 1;
                }
                return agvCar;
            }
        }
        #endregion

        #region 界面消息响应
        /// <summary>
        /// 系统打开
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_OpenSystem_Click(object sender, RoutedEventArgs e)
        {
            if (IsOpenSystem)
            {
                MessageBox.Show("系统已启动!");
                return;
            }
            try
            {
                //交通管制串口
                if (SPComControl == null)
                {
                    ControlCOMInit();
                }
                if (!SPComControl.IsOpen)
                {
                    SPComControl.portname = GlobalPara.Gcontrolcomname;
                    SPComControl.Open();
                    SPComControl.DiscardInBuffer();
                    SPComControl.Write(buf_trafficctl, 0, buf_trafficctl.Length);
                }

                //叫料系统串口
                if (GlobalPara.IsCallFun)
                {
                    if (SPComCall == null)
                    {
                        CallCOMInit();
                    }
                    if (!SPComCall.IsOpen)
                    {
                        SPComCall.portname = GlobalPara.Gcallcomname;
                        SPComCall.Open();
                        SPComCall.DiscardInBuffer();
                    }
                }
            }
            catch (System.Exception ex)
            {
                if (SPComControl != null)
                {
                    SPComControl.Close();
                }
                if (SPComCall != null)
                {
                    SPComCall.Close();
                }
                MessageBox.Show(ex.Message);
                return;
            }

            Array.Clear(buf_virtualtrafficctl, 0, buf_virtualtrafficctl.Length);
            if (!pageShift.IsEnabled && cbautoshift.IsChecked.Value)
            {
                pageShift.Start();//开始计时，开始循环
            }
            if (!DataTimer.IsEnabled)
            {
                SPComControl.DiscardInBuffer();
                DataTimer.Start();//数据发送Timers
            }
            if (SPComControl.IsOpen)
            {
                imgCOM.Source = new BitmapImage(new Uri("pack://application:,,,/Image/Light_Open_24.png"));
                lblcomstate.Content = "打开";
                lblcomstate.Foreground = Brushes.Green;
            }
            else
            {
                return;
            }
            IsOpenSystem = true;
            imgSystem.Source = new BitmapImage(new Uri("pack://application:,,,/Image/Light_Open_24.png"));
            lblsystemstate.Content = "打开";
            lblsystemstate.Foreground = Brushes.Green;
            btnControl.IsEnabled = true;
            btn_OpenSystem.IsEnabled = false;
            btn_CloseSystem.IsEnabled = true;
            try
            {
                if (GlobalPara.IsClientFun)
                {
                    if (_host == null)
                    {
                        _host = new ServiceHost(typeof(WcfDuplexMessageService.MessageService));
                        IAsyncResult result = _host.BeginOpen(new AsyncCallback(HostOpenAsync), _host);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + " and "+ex.InnerException);
            }
        }

        private void HostOpenAsync(IAsyncResult result)
        {
            _host.EndOpen(result);
            if (result.IsCompleted)
            {
                App.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    imgService.Source = new BitmapImage(new Uri("pack://application:,,,/Image/Light_Open_24.png"));
                    lblservicestate.Content = "打开";
                    lblservicestate.Foreground = Brushes.Green;
                }));
            }
        }

        /// <summary>
        /// 系统关闭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_CloseSystem_Click(object sender, RoutedEventArgs e)
        {
            if (!IsOpenSystem)
            {
                MessageBox.Show("系统已关闭!");
                return;
            }
            SPComControl.Close();
            if (pageShift.IsEnabled)
            {
                pageShift.Stop();
            }
            if (DataTimer.IsEnabled)
            {
                DataTimer.Stop();
            }
            if (!SPComControl.IsOpen)
            {
                imgCOM.Source = new BitmapImage(new Uri("pack://application:,,,/Image/Light_Close_24.png"));
                lblcomstate.Content = "关闭";
                lblcomstate.Foreground = Brushes.Red;
            }
            //删除所有动态数据集中AGV车辆，Count为了避免出现while死循环
            int count = AGVStatus.Count;
            while (AGVStatus.Count > 0 && count > 0)
            {
                AGVStatus[0].WlLink = true;
                AGVStatus[0].WlLink = false;
                count--;
            }
            IsOpenSystem = false;
            imgSystem.Source = new BitmapImage(new Uri("pack://application:,,,/Image/Light_Close_24.png"));
            lblsystemstate.Content = "关闭";
            lblsystemstate.Foreground = Brushes.Red;
            btnControl.IsEnabled = false;
            btn_OpenSystem.IsEnabled = true;
            btn_CloseSystem.IsEnabled = false;
            try
            {
                if (_host != null)
                {
                    while (WcfDuplexMessageService.MessageService.ClientCallbackList.Count > 0)
                    {
                        WcfDuplexMessageService.MessageService.ClientCallbackList[0].SendSystemMessage("服务器关闭");
                        WcfDuplexMessageService.MessageService.ClientCallbackList.RemoveAt(0);
                    }
                    IAsyncResult result = _host.BeginClose(new AsyncCallback(HostCloseAsync), null);
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void HostCloseAsync(IAsyncResult result)
        {
            _host.EndClose(result);
            if (result.IsCompleted)
            {
                _host = null;
                App.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    imgService.Source = new BitmapImage(new Uri("pack://application:,,,/Image/Light_Close_24.png"));
                    lblservicestate.Content = "关闭";
                    lblservicestate.Foreground = Brushes.Red;
                }));
            }
        }

        /// <summary>
        /// 小车控制
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnControl_Click(object sender, RoutedEventArgs e)
        {
            int iAgvnum = this.cb_AgvNum.SelectedIndex;
            int iOperation = this.cb_Operation.SelectedIndex;
            int iSpeed = this.cb_Speed.SelectedIndex;
            AGVControlCommand(Convert.ToByte(iAgvnum + 1), Convert.ToByte(iOperation), Convert.ToByte(iSpeed), Convert.ToInt16(this.cb_LineNum.Text));
        }

        /// <summary>
        /// AGV控制命令
        /// </summary>
        /// <param name="agvnum">AGV车辆编号</param>
        /// <param name="operation">AGV操作选项</param>
        /// <param name="speed">AGV速度等级</param>
        /// <param name="line">AGV线路</param>
        public void AGVControlCommand(byte agvnum, byte operation, byte speed, int line)
        {
            try
            {
                byte bline = Convert.ToByte(line);
                byte Operation = 0;
                if (operation > 0 && operation < 8)
                {
                    Operation = Convert.ToByte(0x01 << (operation - 1));
                }
                else
                {
                    Operation = 0x00;
                }
                byte[] temp = { 0x10, 0x71, agvnum, Operation, speed, bline, 0x00, 0x00, 0x00, 0x00, 0x03 };
                temp[temp.Length - 2] = COM.SerialPortWrapper.GetXORCheckCode(temp, temp.Length - 2);
                buf_runctl.AddRange(temp);
                ctrlWaitNum.Add(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show("发送控制命令失败！原因：" + ex.Message);
            }
        }

        /// <summary>
        /// AGV编号ComboBox选择更改触发事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cb_AgvNum_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //修改日期：2014-01-02
            cb_Speed.SelectedIndex = 0;
            cb_Operation.SelectedIndex = 0;
            cb_LineNum.SelectedIndex = 0;
        }

        /// <summary>
        /// 界面自动切换CheckBox勾选触发事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbautoshift_Checked(object sender, RoutedEventArgs e)
        {
            if (tbpageinterval != null)
            {
                tbpageinterval.IsEnabled = true;
                if (IsOpenSystem)
                {
                    pageShift.Start();
                }
            }
        }

        /// <summary>
        /// 界面自动切换CheckBox不选触发事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbautoshift_Unchecked(object sender, RoutedEventArgs e)
        {
            if (tbpageinterval != null)
            {
                tbpageinterval.IsEnabled = false;
                pageShift.Stop();
            }
        }

        /// <summary>
        /// 界面切换时间间隔文本框粘帖触发事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbpageinterval_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (!AGVUtils.IsNumberic(text))
                { e.CancelCommand(); }
            }
            else { e.CancelCommand(); }
        }

        /// <summary>
        /// 界面切换时间间隔文本框按键触发事件
        /// 在文本框里按下一个键后会先确发PreviewKeyDown再确发KeyPress事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbpageinterval_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                e.Handled = true;
        }

        /// <summary>
        /// 界面切换时间间隔文本框输入触发事件
        /// 以与设备无关的方式侦听文本输入，如果输入的是英文字符，那么执行e.Handled = true之后，TextBox中确实没有字符出现。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbpageinterval_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!AGVUtils.IsNumberic(e.Text))
            {
                e.Handled = true;
            }
            else
            {
                e.Handled = false;
            }
        }

        /// <summary>
        /// 界面切换时间间隔文本框失去焦点时触发事件；
        /// 失去焦点后文本框内容作为修改的时间间隔，并立即执行。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbpageinterval_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                int temp;
                temp = Int32.Parse(tbpageinterval.Text);
                if (GlobalPara.PageShiftInterval != temp)
                {
                    GlobalPara.PageShiftInterval = temp;
                    pageShift.Interval = TimeSpan.FromSeconds(GlobalPara.PageShiftInterval);
                    if (IsOpenSystem)
                    {
                        pageShift.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void expanderMain_MouseEnter(object sender, MouseEventArgs e)
        {
            expanderMain.IsExpanded = true;
        }

        private void expanderMain_MouseLeave(object sender, MouseEventArgs e)
        {
            expanderMain.IsExpanded = false;
        }

        #endregion

        #region 菜单栏响应消息

        /// <summary>
        /// 菜单栏获得焦点
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menu_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            menu.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 菜单栏失去焦点
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menu_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            menu.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 由于还能通过键盘组合键的方式来打开菜单，所以还要响应ContextMenuOpening事件，
        /// 不论Menu由于什么原因Opening了，菜单栏都需要为显示状态。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            menu.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 响应键盘Alt键，然后显示出Menu。这里需要用到WPF中的隧道事件(PreviewXXX)，
        /// 从根元素开始响应，这样不论焦点在哪个控件上，都能得到KeyDown事件。例子中是在Window根节点添加
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt)
            {
                if (menu.Visibility != Visibility.Visible) menu.Visibility = Visibility.Visible;
            }
        }

        private void AGVPara_Click(object sender, RoutedEventArgs e)
        {
            AGVParaSetting apsdialog = new AGVParaSetting();
            apsdialog.Show();
        }

        private void ControlCOM_Click(object sender, RoutedEventArgs e)
        {
            ControlCOMSetting ccsdialog = new ControlCOMSetting();
            ccsdialog.Show();
        }

        private void CallCOM_Click(object sender, RoutedEventArgs e)
        {
            CallCOMSetting csdialog = new CallCOMSetting();
            csdialog.Show();
        }

        private void PassWord_Click(object sender, RoutedEventArgs e)
        {
            PassWordSetting psdialog = new PassWordSetting();
            psdialog.Show();
        }

        private void UserManage_Click(object sender, RoutedEventArgs e)
        {
            UserManage umdialog = new UserManage();
            umdialog.Show();
        }

        private void Mark_Click(object sender, RoutedEventArgs e)
        {
            AGVManage amdialog = new AGVManage("Mark");
            amdialog.Show();
        }

        private void Route_Click(object sender, RoutedEventArgs e)
        {
            AGVManage amdialog = new AGVManage("Route");
            amdialog.Show();
        }

        private void Traffic_Click(object sender, RoutedEventArgs e)
        {
            AGVManage amdialog = new AGVManage("Traffic");
            amdialog.Show();
        }

        private void WorkLine_Click(object sender, RoutedEventArgs e)
        {
            WorkLineManage wmdialog = new WorkLineManage();
            wmdialog.Show();
        }

        private void DockArea_Click(object sender, RoutedEventArgs e)
        {
            DockManage dmdialog = new DockManage();
            dmdialog.Show();
        }

        private void ChargeArea_Click(object sender, RoutedEventArgs e)
        {
            ChargeManage cmdialog = new ChargeManage();
            cmdialog.Show();
        }

        private void Speed_Click(object sender, RoutedEventArgs e)
        {
            SpeedManage smdialog = new SpeedManage();
            smdialog.Show();
        }

        private void CallManage_Click(object sender, RoutedEventArgs e)
        {
            MaterialsSettings msdialog = new MaterialsSettings();
            msdialog.Show();
        }

        private void CallAddressSet_Click(object sender, RoutedEventArgs e)
        {
            CallAddressSet csdialog = new CallAddressSet();
            csdialog.Show();
        }

        private void Custom_Click(object sender, RoutedEventArgs e)
        {
            CustomManage cmdialog = new CustomManage();
            cmdialog.Show();
        }

        private void Exception_Click(object sender, RoutedEventArgs e)
        {
            ExceptionManage emdialog = new ExceptionManage();
            emdialog.Show();
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            string filename = System.Environment.CurrentDirectory + "\\ReadMe.pdf";
            if (File.Exists(filename))
            {
                System.Diagnostics.Process.Start(filename);
            }
            else
            {
                MessageBox.Show("请联系管理员！","提示",MessageBoxButton.OK,MessageBoxImage.Information);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            Help hdialog = new Help();
            hdialog.Show();
        }
        
        #endregion

        #region 在画布中的鼠标处理

        bool MouseLeftPress = false;
        Point lastpoint = new Point(0, 0);
        bool FullScreen = false;
        Point OffsetPoint = new Point(0, 0);
        private void canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MouseLeftPress = true;
            lastpoint.X = e.GetPosition(null).X + OffsetPoint.X;
            lastpoint.Y = e.GetPosition(null).Y + OffsetPoint.Y;
            this.Cursor = Cursors.Hand;
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (MouseLeftPress)
            {
                Point newpoint = e.GetPosition(null);
                canvas.Offset = new Point(lastpoint.X - newpoint.X, lastpoint.Y - newpoint.Y);
            }
        }

        private void canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            MouseLeftPress = false;
            this.Cursor = Cursors.Arrow;
            OffsetPoint = canvas.Offset;
        }

        private void canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            MouseLeftPress = false;
        }

        private void canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            FullScreen = !FullScreen;
            if (FullScreen)
            {
                Panel.SetZIndex(canvas, 3);
            }
            else
            {
                //Panel.SetZIndex(canvas, 0);
                canvas.Scale = 1;
                canvas.Offset = new Point(0, 0);
            }
        }

        private void mCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point zoomCenter = e.GetPosition(this.canvas);
            canvas.Scale += e.Delta / 1000.0;
            //最大放大十倍
            if (canvas.Scale > 10)
            {
                canvas.Scale = 10;
            }
            //缩小可以小到3倍
            if (canvas.Scale < 0.3)
            {
                canvas.Scale = 0.3;
            }
        }
        #endregion
    }
}
