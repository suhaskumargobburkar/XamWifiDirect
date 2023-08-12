using System;
using System.Collections.Concurrent;
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
using Windows.UI.Core;
using Windows.UI.Popups;
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
    public sealed partial class SenderUWPViewPage : Page
    {
        private MainPage rootPage = MainPage.Current;
        private ObservableCollection<ConnectedDevice> ConnectedDevices = new ObservableCollection<ConnectedDevice>();
        WiFiDirectAdvertisementPublisher _publisher;
        WiFiDirectConnectionListener _listener;
        List<WiFiDirectInformationElement> _informationElements = new List<WiFiDirectInformationElement>();
        ConcurrentDictionary<StreamSocketListener, WiFiDirectDevice> _pendingConnections = new ConcurrentDictionary<StreamSocketListener, WiFiDirectDevice>();


        public SenderUWPViewPage()
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
                if (btnStop.IsEnabled)
                {
                    StopAdvertisement();
                }
                rootFrame.GoBack();
            }
        }

        private void StopAdvertisement()
        {
            _publisher.Stop();
            _publisher.StatusChanged -= OnStatusChanged;

            _listener.ConnectionRequested -= OnConnectionRequested;

            _informationElements.Clear();

            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            StatusTextBlock.Text = string.Format("Advertisement Stopped");
        }
        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            _publisher = new WiFiDirectAdvertisementPublisher();
            _publisher.StatusChanged += OnStatusChanged;

            _listener = new WiFiDirectConnectionListener();

            if (true)
            {
                try
                {
                    // This can raise an exception if the machine does not support WiFi. Sorry.
                    _listener.ConnectionRequested += OnConnectionRequested;
                }
                catch (Exception ex)
                {
                    PrintConsole("Error preparing Advertisement: {0}" + ex.Message, NotifyType.ErrorMessage);
                    return;
                }
            }

            //var discoverability = Utils.GetSelectedItemTag<WiFiDirectAdvertisementListenStateDiscoverability>("Normal");
            //_publisher.Advertisement.ListenStateDiscoverability = discoverability;

            _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;
            _publisher.Advertisement.LegacySettings.IsEnabled = false;
            // Legacy settings are meaningful only if IsAutonomousGroupOwnerEnabled is true.
            //if (_publisher.Advertisement.IsAutonomousGroupOwnerEnabled && chkLegacySetting.IsChecked.Value)
            //{
            //    _publisher.Advertisement.LegacySettings.IsEnabled = true;
            //    if (!String.IsNullOrEmpty(txtPassphrase.Text))
            //    {
            //        var creds = new Windows.Security.Credentials.PasswordCredential();
            //        creds.Password = txtPassphrase.Text;
            //        _publisher.Advertisement.LegacySettings.Passphrase = creds;
            //    }

            //    if (!String.IsNullOrEmpty(txtSsid.Text))
            //    {
            //        _publisher.Advertisement.LegacySettings.Ssid = txtSsid.Text;
            //    }
            //}

            // Add the information elements.
            foreach (WiFiDirectInformationElement informationElement in _informationElements)
            {
                _publisher.Advertisement.InformationElements.Add(informationElement);
            }

            _publisher.Start();

            if (_publisher.Status == WiFiDirectAdvertisementPublisherStatus.Started)
            {
                btnStart.IsEnabled = false;
                btnStop.IsEnabled = true;
                StatusTextBlock.Text = string.Format("Advertisement started");
            }
            else
            {
                StatusTextBlock.Text = string.Format("Advertisement failed to start. Status is "+_publisher.Status);
            }
        }

        private async Task<bool> HandleConnectionRequestAsync(WiFiDirectConnectionRequest connectionRequest)
        {
            string deviceName = connectionRequest.DeviceInformation.Name;

            bool isPaired = (connectionRequest.DeviceInformation.Pairing?.IsPaired == true) ||
                            (await IsAepPairedAsync(connectionRequest.DeviceInformation.Id));

            // Show the prompt only in case of WiFiDirect reconnection or Legacy client connection.
            if (isPaired || _publisher.Advertisement.LegacySettings.IsEnabled)
            {
                var messageDialog = new MessageDialog($"Connection request received from {deviceName}", "Connection Request");

                // Add two commands, distinguished by their tag.
                // The default command is "Decline", and if the user cancels, we treat it as "Decline".
                messageDialog.Commands.Add(new UICommand("Accept", null, true));
                messageDialog.Commands.Add(new UICommand("Decline", null, null));
                messageDialog.DefaultCommandIndex = 1;
                messageDialog.CancelCommandIndex = 1;

                // Show the message dialog
                var commandChosen = await messageDialog.ShowAsync();

                if (commandChosen.Id == null)
                {
                    return false;
                }
            }

            PrintConsole("Connecting to "+deviceName+"...", NotifyType.StatusMessage);

            // Pair device if not already paired and not using legacy settings
            if (!isPaired && !_publisher.Advertisement.LegacySettings.IsEnabled)
            {
                if (!await RequestPairDeviceAsync(connectionRequest.DeviceInformation.Pairing))
                {
                    return false;
                }
            }

            WiFiDirectDevice wfdDevice = null;
            try
            {
                // IMPORTANT: FromIdAsync needs to be called from the UI thread
                wfdDevice = await WiFiDirectDevice.FromIdAsync(connectionRequest.DeviceInformation.Id);
            }
            catch (Exception ex)
            {
                PrintConsole("Exception in FromIdAsync: "+ex.Message, NotifyType.ErrorMessage);
                return false;
            }

            // Register for the ConnectionStatusChanged event handler
            wfdDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

            var listenerSocket = new StreamSocketListener();

            // Save this (listenerSocket, wfdDevice) pair so we can hook it up when the socket connection is made.
            _pendingConnections[listenerSocket] = wfdDevice;

            var EndpointPairs = wfdDevice.GetConnectionEndpointPairs();

            listenerSocket.ConnectionReceived += this.OnSocketConnectionReceived;
            try
            {
                await listenerSocket.BindServiceNameAsync(Globals.strServerPort);
            }
            catch (Exception ex)
            {
                PrintConsole("Connect operation threw an exception: "+ ex.Message, NotifyType.ErrorMessage);
                return false;
            }

            PrintConsole("Devices connected on L2, listening on IP Address: "+EndpointPairs[0].LocalHostName + " Port: "+ Globals.strServerPort, NotifyType.StatusMessage);


            return true;
        }

        void PrintConsole(string message, NotifyType notify)
        {
            Console.WriteLine("Print - "+notify + " : "+message);
        }

        private void OnSocketConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                PrintConsole("Connecting to remote side on L4 layer...", NotifyType.StatusMessage);
                StreamSocket serverSocket = args.Socket;

                SocketReaderWriter socketRW = new SocketReaderWriter(serverSocket, rootPage);
                // The first message sent is the name of the connection.
                //string message = await socketRW.ReadMessageAsync();

                // Find the pending connection and add it to the list of active connections.
                WiFiDirectDevice wfdDevice;
                if (_pendingConnections.TryRemove(sender, out wfdDevice))
                {
                    ConnectedDevices.Add(new ConnectedDevice("test", wfdDevice, socketRW));
                }


                await socketRW.ReadMessageAsync();

            });
        }

        private void OnConnectionStatusChanged(WiFiDirectDevice sender, object args)
        {
            PrintConsole("Connection Status: " + sender.ConnectionStatus, notify: NotifyType.StatusMessage);
            if (sender.ConnectionStatus == WiFiDirectConnectionStatus.Disconnected)
            {
                // TODO: Should we remove this connection from the list?
                // (Yes, probably.)
            }
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
            if (result.Status != DevicePairingResultStatus.Paired)
            {
                PrintConsole($"PairAsync failed, Status: {result.Status}", NotifyType.ErrorMessage);
                return false;
            }
            return true;
        }

        private void OnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            Utils.HandlePairing(Dispatcher, args);
        }
        private async Task<bool> IsAepPairedAsync(string deviceId)
        {
            List<string> additionalProperties = new List<string>();
            additionalProperties.Add("System.Devices.Aep.DeviceAddress");
            String deviceSelector = $"System.Devices.Aep.AepId:=\"{deviceId}\"";
            DeviceInformation devInfo = null;

            try
            {
                devInfo = await DeviceInformation.CreateFromIdAsync(deviceId, additionalProperties);
            }
            catch (Exception ex)
            {
               StatusTextBlock.Text = string.Format("DeviceInformation.CreateFromIdAsync threw an exception: " + ex.Message, NotifyType.ErrorMessage);
            }

            if (devInfo == null)
            {
                StatusTextBlock.Text = string.Format("Device Information is null", NotifyType.ErrorMessage);
                return false;
            }

            deviceSelector = $"System.Devices.Aep.DeviceAddress:=\"{devInfo.Properties["System.Devices.Aep.DeviceAddress"]}\"";
            DeviceInformationCollection pairedDeviceCollection = await DeviceInformation.FindAllAsync(deviceSelector, null, DeviceInformationKind.Device);
            return pairedDeviceCollection.Count > 0;
        }
        private async void OnConnectionRequested(WiFiDirectConnectionListener sender, WiFiDirectConnectionRequestedEventArgs connectionEventArgs)
        {
            WiFiDirectConnectionRequest connectionRequest = connectionEventArgs.GetConnectionRequest();
            bool success = false;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,async () =>
            {
                success = await HandleConnectionRequestAsync(connectionRequest);
            });

            if (!success)
            {
                // Decline the connection request
                rootPage.NotifyUserFromBackground($"Connection request from {connectionRequest.DeviceInformation.Name} was declined", NotifyType.ErrorMessage);
                connectionRequest.Dispose();
            }
        }

        private async void OnStatusChanged(WiFiDirectAdvertisementPublisher sender, WiFiDirectAdvertisementPublisherStatusChangedEventArgs statusEventArgs)
        {
            if (statusEventArgs.Status == WiFiDirectAdvertisementPublisherStatus.Started)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (sender.Advertisement.LegacySettings.IsEnabled)
                    {
                        // Show the autogenerated passphrase and SSID.
                        //if (String.IsNullOrEmpty(txtPassphrase.Text))
                        //{
                        //    txtPassphrase.Text = _publisher.Advertisement.LegacySettings.Passphrase.Password;
                        //}

                        //if (String.IsNullOrEmpty(txtSsid.Text))
                        //{
                        //    txtSsid.Text = _publisher.Advertisement.LegacySettings.Ssid;
                        //}
                    }
                });
            }

            PrintConsole($"Advertisement: Status: {statusEventArgs.Status} Error: {statusEventArgs.Error}", NotifyType.StatusMessage);
            return;
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            StopAdvertisement();
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
    }
}
