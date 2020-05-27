using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NetCon.inter
{
    class NetConImpl : INetCon
    {

        //Ciało callbacku
        private FrameListener fl = (IntPtr arr, int size) => { return 1; };

        [DllImport(".\\..\\..\\..\\Debug\\NETCONLIB.dll",EntryPoint ="startCapture", CallingConvention = CallingConvention.Cdecl)]
        private static extern void _startCapture(
            int port,
            [MarshalAs(UnmanagedType.FunctionPtr)]
            FrameListener listener, 
            int bufSize);

        [DllImport(".\\..\\..\\..\\Debug\\NETCONLIB.dll", EntryPoint = "stopCapture")]
        private static extern int _stopCapture();

        [DllImport(".\\..\\..\\..\\Debug\\NETCONLIB.dll", EntryPoint = "send_and_receive_mdio", CallingConvention = CallingConvention.Cdecl)]
        private static extern void _sendAndReceiveMdio(int opCode, int phyAddr, int regAddr, int data);

        [DllImport(".\\..\\..\\..\\Debug\\NETCONLIB.dll", EntryPoint = "sendRequest", CallingConvention = CallingConvention.Cdecl)]
        private static extern void _sendRequest(int port, int function, bool state);
        //sendSettingsWrapper(int port, int minFrameLen[4], std::string vecStr[]);

        [DllImport(".\\..\\..\\..\\Debug\\NETCONLIB.dll", EntryPoint = "sendSettingsWrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern void _sendSettingsWrapper(int argc, string[] argv);

        [DllImport(".\\..\\..\\..\\Debug\\NETCONLIB.dll", EntryPoint = "setCaptureState", CallingConvention = CallingConvention.Cdecl)]
        private static extern void _setCaptureState(bool state);
        public void sendAndReceiveMdio()
        {
            for (int i = 1; i < 5; i++)
            {
                _sendAndReceiveMdio(1, i, 13, 7);
                _sendAndReceiveMdio(1, i, 14, 60);
                _sendAndReceiveMdio(1, i, 13, 16391);
                _sendAndReceiveMdio(1, i, 14, 0);
            }

            for (int i = 1; i < 5; i++)
            {
                _sendAndReceiveMdio(1, i, 13, 7);
                _sendAndReceiveMdio(1, i, 14, 60);
                _sendAndReceiveMdio(1, i, 13, 16391);
                _sendAndReceiveMdio(2, i, 14, 0);
            }

            for (int i = 1; i < 5; i++)
            {
                _sendAndReceiveMdio(1, i, 0, 37184);
            }

        }

        public void sendFilter()
        {

        }

        public void sendRequest(RequestCode code, int port, bool value)
        {
            _sendRequest((int)code, port, value);
        }

        public void sendSettings(string[] settings)
        {
            _sendSettingsWrapper(settings.Length,settings);
        }

        public void setOnFrameListener(FrameListener f)
        {
            fl = f;
        }

        public void startCapture(int port, int bufferSizeMegaBytes)
        {
            _startCapture(port, fl, 1024 * 1024 * bufferSizeMegaBytes);
        }

        public void stopCapture()
        {
            var ret = _stopCapture();
        }

        public void setCaptureState(bool state)
        {
            _setCaptureState(state);
        }
    }
}
