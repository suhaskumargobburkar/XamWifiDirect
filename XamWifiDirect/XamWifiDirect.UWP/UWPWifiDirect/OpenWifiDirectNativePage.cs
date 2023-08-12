using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using XamWifiDirect.UWP.UWPWifiDirect;

[assembly: Dependency(typeof(OpenWifiDirectNativePage))]
namespace XamWifiDirect.UWP.UWPWifiDirect
{
    public class OpenWifiDirectNativePage : IOpenWifiDirectNativePage
    {
        Windows.UI.Xaml.Controls.Frame rootFrame = Windows.UI.Xaml.Window.Current.Content as Windows.UI.Xaml.Controls.Frame;
        public void CloseReceiverViewPage()
        {
            if (rootFrame != null)
            {
                if(rootFrame.CanGoBack)
                {
                    rootFrame.GoBack();
                }
            }
        }

        public void CloseSenderViewPage()
        {
            if (rootFrame != null)
            {
                if (rootFrame.CanGoBack)
                {
                    rootFrame.GoBack();
                }
            }
        }

        public void OpenReceiverViewPage()
        {
            if(rootFrame != null)
            {
                rootFrame.Navigate(typeof(DataExchange),false);
            }
        }

        public void OpenSenderViewPage()
        {
            if (rootFrame != null)
            {
                rootFrame.Navigate(typeof(DataExchange),true);
            }
        }
    }
}
