using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
using Xamarin.Forms;

namespace XamWifiDirect.UWP.UWPWifiDirect
{
    public partial class MainPage : Page
    {
        public const string FEATURE_NAME = "Wi-Fi Direct";
        static MainPage mainPage;
        public static MainPage Current => mainPage??(mainPage =new MainPage());

        List<Scenario> scenarios = new List<Scenario>
        {
            new Scenario() { Title="Advertiser", ClassType=typeof(SenderUWPViewPage)},
            new Scenario() { Title="Connector", ClassType=typeof(RecevierUWPViewPage)}
        };

        public async void NotifyUserFromBackground(string strMessage, NotifyType type)
        {
            //await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            //{
            //    NotifyUser(strMessage, type);
            //});
        }
    }

    public class Scenario
    {
        public string Title { get; set; }
        public Type ClassType { get; set; }
    }
}
