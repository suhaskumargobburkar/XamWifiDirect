using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFiDirect;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.Sockets;
using Windows.Networking;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace XamWifiDirect.UWP.UWPWifiDirect
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class RecevierUWPViewPage : Page
    {
        private MainPage rootPage = MainPage.Current;
        DeviceWatcher _deviceWatcher;
        bool _fWatcherStarted = false;
        WiFiDirectAdvertisementPublisher _publisher = new WiFiDirectAdvertisementPublisher();

        public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = new ObservableCollection<DiscoveredDevice>();
        public ObservableCollection<ConnectedDevice> ConnectedDevices { get; } = new ObservableCollection<ConnectedDevice>();

        public RecevierUWPViewPage()
        {
            this.InitializeComponent();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                return;
            }

            // Navigate back if possible, and if the event has not 
            // already been handled .
            if (rootFrame.CanGoBack)
            {
                CloseConnect();
                rootFrame.GoBack();
            }
        }
        void CloseConnect()
        {
            var connectedDevice = (ConnectedDevice)lvConnectedDevices.SelectedItem;
            ConnectedDevices.Remove(connectedDevice);

            // Close socket and WiFiDirect object
            if (connectedDevice != null)
                connectedDevice.Dispose();
        }
        void PrintConsole(string message, NotifyType notify)
        {
            Console.WriteLine("Print - " + notify + " : " + message);
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_fWatcherStarted == false)
            {
                _publisher.Start();

                if (_publisher.Status != WiFiDirectAdvertisementPublisherStatus.Started)
                {
                    StatusTextBlock.Text = string.Format("Failed to start advertisement.", NotifyType.ErrorMessage);
                    return;
                }

                DiscoveredDevices.Clear();
                PrintConsole("Finding Devices...", NotifyType.StatusMessage);

                String deviceSelector = WiFiDirectDevice.GetDeviceSelector(
                   WiFiDirectDeviceSelectorType.AssociationEndpoint);

                _deviceWatcher = DeviceInformation.CreateWatcher(deviceSelector, new string[] { "System.Devices.WiFiDirect.InformationElements" });

                _deviceWatcher.Added += OnDeviceAdded;
                _deviceWatcher.Removed += OnDeviceRemoved;
                _deviceWatcher.Updated += OnDeviceUpdated;
                _deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;
                _deviceWatcher.Stopped += OnStopped;

                _deviceWatcher.Start();

                btnStart.Content = "Stop Watcher";
                _fWatcherStarted = true;
            }
            else
            {
                _publisher.Stop();

                btnStart.Content = "Start Watcher";
                _fWatcherStarted = false;

                _deviceWatcher.Added -= OnDeviceAdded;
                _deviceWatcher.Removed -= OnDeviceRemoved;
                _deviceWatcher.Updated -= OnDeviceUpdated;
                _deviceWatcher.EnumerationCompleted -= OnEnumerationCompleted;
                _deviceWatcher.Stopped -= OnStopped;

                _deviceWatcher.Stop();

                StatusTextBlock.Text = string.Format("Device watcher stopped.", NotifyType.StatusMessage);
            }
        }

        #region DeviceWatcherEvents
        private async void OnDeviceAdded(DeviceWatcher deviceWatcher, DeviceInformation deviceInfo)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                DiscoveredDevices.Add(new DiscoveredDevice(deviceInfo));
            });
        }

        private async void OnDeviceRemoved(DeviceWatcher deviceWatcher, DeviceInformationUpdate deviceInfoUpdate)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (DiscoveredDevice discoveredDevice in DiscoveredDevices)
                {
                    if (discoveredDevice.DeviceInfo.Id == deviceInfoUpdate.Id)
                    {
                        DiscoveredDevices.Remove(discoveredDevice);
                        break;
                    }
                }
            });
        }

        private async void OnDeviceUpdated(DeviceWatcher deviceWatcher, DeviceInformationUpdate deviceInfoUpdate)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                foreach (DiscoveredDevice discoveredDevice in DiscoveredDevices)
                {
                    if (discoveredDevice.DeviceInfo.Id == deviceInfoUpdate.Id)
                    {
                        discoveredDevice.UpdateDeviceInfo(deviceInfoUpdate);
                        break;
                    }
                }
            });
        }

        private void OnEnumerationCompleted(DeviceWatcher deviceWatcher, object o)
        {
            rootPage.NotifyUserFromBackground("DeviceWatcher enumeration completed", NotifyType.StatusMessage);
        }

        private void OnStopped(DeviceWatcher deviceWatcher, object o)
        {
            rootPage.NotifyUserFromBackground("DeviceWatcher stopped", NotifyType.StatusMessage);
        }
        #endregion

        private void OnConnectionStatusChanged(WiFiDirectDevice sender, object arg)
        {
            PrintConsole($"Connection status changed: {sender.ConnectionStatus}", NotifyType.StatusMessage);
        }
        private void btnStop_Click(object sender, RoutedEventArgs e)
        {

        }

        public async Task<bool> RequestPairDeviceAsync(DeviceInformationPairing pairing)
        {
            WiFiDirectConnectionParameters connectionParams = new WiFiDirectConnectionParameters();

            //short? groupOwnerIntent = Utils.GetSelectedItemTag<short?>(cmbGOIntent);
            //if (groupOwnerIntent.HasValue)
            //{
            //    connectionParams.GroupOwnerIntent = groupOwnerIntent.Value;
            //}

            DevicePairingKinds devicePairingKinds = DevicePairingKinds.None;
            //if (_supportedConfigMethods.Count > 0)
            //{
            //    // If specific configuration methods were added, then use them.
            //    foreach (var configMethod in _supportedConfigMethods)
            //    {
            //        connectionParams.PreferenceOrderedConfigurationMethods.Add(configMethod);
            //        devicePairingKinds |= WiFiDirectConnectionParameters.GetDevicePairingKinds(configMethod);
            //    }
            //}
            //else
            {
                // If specific configuration methods were not added, then we'll use these pairing kinds.
                devicePairingKinds = DevicePairingKinds.ConfirmOnly;// | DevicePairingKinds.DisplayPin | DevicePairingKinds.ProvidePin;
            }

            connectionParams.PreferredPairingProcedure = WiFiDirectPairingProcedure.GroupOwnerNegotiation;
            DeviceInformationCustomPairing customPairing = pairing.Custom;
            customPairing.PairingRequested += OnPairingRequested;

            DevicePairingResult result = await customPairing.PairAsync(devicePairingKinds, DevicePairingProtectionLevel.Default, connectionParams);
            if (result.Status != DevicePairingResultStatus.AlreadyPaired && result.Status!=DevicePairingResultStatus.Paired)
            {
                await Utils.ShowAlertMessage(Dispatcher, "PairAsync failed");
                PrintConsole($"PairAsync failed, Status: {result.Status}", NotifyType.ErrorMessage);
                return false;
            }
            btnSendData_Click(null, null);
            await Utils.ShowAlertMessage(Dispatcher, "PairAsync True");
            return true;
        }

        private void OnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            Utils.HandlePairing(Dispatcher, args);
        }
        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            var discoveredDevice = (DiscoveredDevice)lvDiscoveredDevices.SelectedItem;

            if (discoveredDevice == null)
            {
                await Utils.ShowAlertMessage(Dispatcher, "Device not selected");
                PrintConsole("No device selected, please select one.", NotifyType.ErrorMessage);
                return;
            }

            PrintConsole($"Connecting to {discoveredDevice.DeviceInfo.Name}...", NotifyType.StatusMessage);

            if (!discoveredDevice.DeviceInfo.Pairing.IsPaired)
            {
                if (!await RequestPairDeviceAsync(discoveredDevice.DeviceInfo.Pairing))
                {
                    await Utils.ShowAlertMessage(Dispatcher, "Connect return");
                    return;
                }
            }

            try
            {
                // IMPORTANT: FromIdAsync needs to be called from the UI thread
                var wfdDevice = await WiFiDirectDevice.FromIdAsync(discoveredDevice.DeviceInfo.Id);

                // Register for the ConnectionStatusChanged event handler
                wfdDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

                IReadOnlyList<EndpointPair> endpointPairs = wfdDevice.GetConnectionEndpointPairs();
                HostName remoteHostName = endpointPairs[0].RemoteHostName;

                PrintConsole($"Devices connected on L2 layer, connecting to IP Address: {remoteHostName} Port: {Globals.strServerPort}",
                    NotifyType.StatusMessage);

                // Wait for server to start listening on a socket
                await Task.Delay(2000);

                // Connect to Advertiser on L4 layer
                StreamSocket clientSocket = new StreamSocket();
                await clientSocket.ConnectAsync(remoteHostName, Globals.strServerPort);
                PrintConsole("Connected with remote side on L4 layer", NotifyType.StatusMessage);

                SocketReaderWriter socketRW = new SocketReaderWriter(clientSocket, rootPage);

                string sessionId = Path.GetRandomFileName();
                ConnectedDevice connectedDevice = new ConnectedDevice(sessionId, wfdDevice, socketRW);
                ConnectedDevices.Add(connectedDevice);

                // The first message sent over the socket is the name of the connection.
                //await socketRW.WriteMessageAsync(sessionId);

                while (await socketRW.ReadMessageAsync() != null)
                {
                    // Keep reading messages
                }
                await Utils.ShowAlertMessage(Dispatcher, "Added Device " + wfdDevice.DeviceId);
            }
            catch (TaskCanceledException)
            {
                await Utils.ShowAlertMessage(Dispatcher, "");
                PrintConsole("FromIdAsync was canceled by user", NotifyType.ErrorMessage);
            }
            catch (Exception ex)
            {
                await Utils.ShowAlertMessage(Dispatcher, "Error Connection operation");
                PrintConsole($"Connect operation threw an exception: {ex.Message}", NotifyType.ErrorMessage);
            }
        }

        private async void btnSendData_Click(object sender, RoutedEventArgs e)
        {
            var connectedDevice = (ConnectedDevice)lvConnectedDevices.SelectedItem;
            if (connectedDevice != null)
            {
                string message = string.IsNullOrEmpty(txtMessage.Text) ? "No Data entered.." : txtMessage.Text;
                await connectedDevice.SocketRW.WriteMessageAsync(message + " - " + DateTime.Now.ToString("dd/MMM/yyyy hh:mm ss tt"));
            }

        }

        private async void btnUnpair_Click(object sender, RoutedEventArgs e)
        {
            var discoveredDevice = (DiscoveredDevice)lvDiscoveredDevices.SelectedItem;

            if (discoveredDevice == null)
            {
                PrintConsole("No device selected, please select one.", NotifyType.ErrorMessage);
                return;
            }

            DeviceUnpairingResult result = await discoveredDevice.DeviceInfo.Pairing.UnpairAsync();
            PrintConsole($"Unpair result: {result.Status}", NotifyType.StatusMessage);
        }
    }
}
