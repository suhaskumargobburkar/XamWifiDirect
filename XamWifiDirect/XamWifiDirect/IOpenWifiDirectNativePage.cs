using System;
using System.Collections.Generic;
using System.Text;

namespace XamWifiDirect
{
    public interface IOpenWifiDirectNativePage
    {
        void OpenSenderViewPage();
        void CloseSenderViewPage();
        void OpenReceiverViewPage();
        void CloseReceiverViewPage();
    }
}
