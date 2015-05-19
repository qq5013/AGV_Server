using System;
using System.Windows;
using COM;
using System.Configuration;
using AGV_WPF_Global;
using System.IO.Ports;
using System.Threading;
using System.ComponentModel;

namespace AGV_WPF
{
    /// <summary>
    /// CallAddressSet.xaml 的交互逻辑
    /// </summary>
    public partial class CallAddressSet : Window
    {
        /// <summary>
        /// 叫料系统发送信息缓冲区
        /// </summary>
        byte[] buf_callsend = { 0x10, 0x44, 0x00, 0x00, 0x00, 0x03 };
        byte[] buf_callrec = { 0x10, 0x44, 0x00, 0x00, 0x00, 0x03 };
        public COM.SerialPortWrapper SPComCall;

        /// <summary>
        /// 构造函数，初始化界面，串口初始化
        /// </summary>
        public CallAddressSet()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 串口初始化，打开了串口
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
                SPComCall.Open();
                btn_Set.IsEnabled = true;
            }
            catch (System.UnauthorizedAccessException ex)
            {
                if (MessageBoxResult.OK == MessageBox.Show(ex.Message + "请先暂停此系统或其他系统使用当前串口!", "错误", MessageBoxButton.OK, MessageBoxImage.Error))
                {
                    this.Close();
                }
            }
            catch (System.Exception ex)
            {
                if (MessageBoxResult.OK == MessageBox.Show(ex.Message + "请在<通信设置>中设置新的串口!", "错误", MessageBoxButton.OK, MessageBoxImage.Error))
                {
                    this.Close();
                }
            }
        }

        /// <summary>
        /// 串口类SerialPortWrapper的数据接收触发委托函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SPComCall_OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Thread.Sleep(10);
            Array.Clear(buf_callrec, 0, buf_callrec.Length);
            SPComCall.Read(buf_callrec, 0, buf_callrec.Length);
            ReceivedDataHandle(buf_callrec);
        }

        private void ReceivedDataHandle(byte[] buf)
        {
            byte[] databuffer = buf;
            if (MainWindow.CallDataCheck(databuffer, databuffer.Length - 2) == 0)
            {
                if (databuffer[1] == 0x43 && databuffer[2] == buf_callsend[3])//设置成功
                {
                    MessageBox.Show("设置地址成功");
                    return;
                }
            }
        }

        /// <summary>
        /// 地址设置按钮消息响应
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Set_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(tbOldAd.Text.Trim()) || String.IsNullOrEmpty(tbNewAd.Text.Trim()))
            {
                MessageBox.Show("请输入地址！");
                return;
            }
            try
            {
                buf_callsend[2] = Byte.Parse(tbOldAd.Text, System.Globalization.NumberStyles.HexNumber);
                buf_callsend[3] = Byte.Parse(tbNewAd.Text, System.Globalization.NumberStyles.HexNumber);
                buf_callsend[4] = COM.SerialPortWrapper.GetXORCheckCode(buf_callsend, buf_callsend.Length - 2);
                SPComCall.Write(buf_callsend, 0, buf_callsend.Length);
            }
            catch (FormatException)
            {
                MessageBox.Show("您输入的地址格式不正确，请重新输入！");
                return;
            }
        }

        #region 退出系统
        /// <summary>
        /// 退出系统
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Stop Com,Unregister SPComCall data received event
            if (null != SPComCall)
            {
                SPComCall.Close();
                SPComCall.OnDataReceived -= this.SPComCall_OnDataReceived;
            }
            if (null != buf_callsend)
            {
                buf_callsend = null;
            }
            if (null != buf_callrec)
            {
                buf_callrec = null;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CallCOMInit();
        }
    }
}
