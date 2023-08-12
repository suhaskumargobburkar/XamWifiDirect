using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace XamWifiDirect
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private void btnRecevier_Clicked(object sender, EventArgs e)
        {
            DependencyService.Get<IOpenWifiDirectNativePage>().OpenReceiverViewPage();
        }

        private void btnSender_Clicked(object sender, EventArgs e)
        {
            DependencyService.Get<IOpenWifiDirectNativePage>().OpenSenderViewPage();
        }
    }
}
