using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFiDirect;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
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
    public sealed partial class DataExchange : Page
    {
        bool IsSenderApp;
        private MainPage rootPage = MainPage.Current;
        DeviceWatcher _deviceWatcher;
        bool _fWatcherStarted = false;
        WiFiDirectAdvertisementPublisher _publisher = new WiFiDirectAdvertisementPublisher();
        public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = new ObservableCollection<DiscoveredDevice>();
        public ObservableCollection<ConnectedDevice> ConnectedDevices { get; } = new ObservableCollection<ConnectedDevice>();
        ObservableCollection<ConsoleMessage> consoleMessages = new ObservableCollection<ConsoleMessage>();

        WiFiDirectConnectionListener _listener;
        List<WiFiDirectInformationElement> _informationElements = new List<WiFiDirectInformationElement>();
        ConcurrentDictionary<StreamSocketListener, WiFiDirectDevice> _pendingConnections = new ConcurrentDictionary<StreamSocketListener, WiFiDirectDevice>();


        public DataExchange()
        {
            this.InitializeComponent();

        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            IsSenderApp = (bool)e.Parameter;
            btnService.IsEnabled = IsSenderApp;
            btnWatcher.IsEnabled = !IsSenderApp;
        }
        private void btnService_Click(object sender, RoutedEventArgs e)
        {
            consoleMessages = new ObservableCollection<ConsoleMessage>();
            if (btnService.Content.ToString() == "Start")
            {
                _publisher = new WiFiDirectAdvertisementPublisher();
                _publisher.StatusChanged += OnPubStatusChanged;

                _listener = new WiFiDirectConnectionListener();

                if (true)
                {
                    try
                    {
                        // This can raise an exception if the machine does not support WiFi. Sorry.
                        _listener.ConnectionRequested += OnListenerConnectionRequested;
                    }
                    catch (Exception ex)
                    {
                        WriteConsole("Error preparing Advertisement: {0}" + ex.Message);
                        return;
                    }
                }

                _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;
                _publisher.Advertisement.LegacySettings.IsEnabled = false;

                foreach (WiFiDirectInformationElement informationElement in _informationElements)
                {
                    _publisher.Advertisement.InformationElements.Add(informationElement);
                }

                _publisher.Start();

                if (_publisher.Status == WiFiDirectAdvertisementPublisherStatus.Started)
                {
                    btnService.Content = "Stop";
                    lblSenderStatus.Text = string.Format("Advertisement started");
                }
                else
                {
                    lblSenderStatus.Text = string.Format("Advertisement failed to start. Status is " + _publisher.Status);
                }
            }
            else
            {
                CloseConnect();
            }
        }

        private void btnWatcher_Click(object sender, RoutedEventArgs e)
        {
            consoleMessages = new ObservableCollection<ConsoleMessage>();
            if (btnWatcher.Content.ToString() == "Start")
            {
                _publisher.Start();

                if (_publisher.Status != WiFiDirectAdvertisementPublisherStatus.Started)
                {
                    lblWatcherStatus.Text = string.Format("Failed to start advertisement.", NotifyType.ErrorMessage);
                    return;
                }

                DiscoveredDevices.Clear();
                WriteConsole("Finding Devices...");

                String deviceSelector = WiFiDirectDevice.GetDeviceSelector(
                   WiFiDirectDeviceSelectorType.AssociationEndpoint);

                _deviceWatcher = DeviceInformation.CreateWatcher(deviceSelector, new string[] { "System.Devices.WiFiDirect.InformationElements" });

                _deviceWatcher.Added += OnDeviceAdded;
                _deviceWatcher.Removed += OnDeviceRemoved;
                _deviceWatcher.Updated += OnDeviceUpdated;
                _deviceWatcher.EnumerationCompleted += OnEnumerationCompleted;
                _deviceWatcher.Stopped += OnStopped;

                _deviceWatcher.Start();

                btnWatcher.Content = "Stop";
                _fWatcherStarted = true;
            }
            else
            {
                CloseConnect();
            }
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

        private void OnStopped(DeviceWatcher deviceWatcher, object o)
        {
           WriteConsole("DeviceWatcher stopped");
        }
        private void OnEnumerationCompleted(DeviceWatcher deviceWatcher, object o)
        {
            WriteConsole("DeviceWatcher enumeration completed");
        }

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
                    if (discoveredDevice.DeviceInfo.Pairing.IsPaired)
                    {

                    }

                    if (discoveredDevice.DeviceInfo.Id == deviceInfoUpdate.Id)
                    {
                        DiscoveredDevices.Remove(discoveredDevice);
                        break;
                    }
                }
            });
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
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

        private void CloseConnect()
        {
            if (IsSenderApp)
            {
                _publisher.Stop();
                _publisher.StatusChanged -= OnPubStatusChanged;

                _listener.ConnectionRequested -= OnListenerConnectionRequested;

                _informationElements.Clear();

                btnService.Content = "Start";
                lblSenderStatus.Text = string.Format("Advertisement Stopped");
            }
            else
            {
                var connectedDevice = (ConnectedDevice)lvConnectedDevices.SelectedItem;
                ConnectedDevices.Remove(connectedDevice);

                // Close socket and WiFiDirect object
                connectedDevice?.Dispose();
                lblWatcherStatus.Text = string.Format("Watcher Stopped");
                btnWatcher.Content = "Start";
            }
        }

        void WriteConsole(string message)
        {
            consoleMessages.Add(new ConsoleMessage { Message = DateTime.Now.ToString("hh:mm ss tt") + " -> " + message });
        }

        private async void OnListenerConnectionRequested(WiFiDirectConnectionListener sender, WiFiDirectConnectionRequestedEventArgs connectionEventArgs)
        {
            WiFiDirectConnectionRequest connectionRequest = connectionEventArgs.GetConnectionRequest();
            bool success = false;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                success = await HandleConnectionRequestAsync(connectionRequest);
            });

            if (!success)
            {
                // Decline the connection request
                WriteConsole($"Connection request from {connectionRequest.DeviceInformation.Name} was declined");
                connectionRequest.Dispose();
            }
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
                WriteConsole("DeviceInformation.CreateFromIdAsync threw an exception: " + ex.Message);
            }

            if (devInfo == null)
            {
                WriteConsole("Device Information is null");
                return false;
            }

            deviceSelector = $"System.Devices.Aep.DeviceAddress:=\"{devInfo.Properties["System.Devices.Aep.DeviceAddress"]}\"";
            DeviceInformationCollection pairedDeviceCollection = await DeviceInformation.FindAllAsync(deviceSelector, null, DeviceInformationKind.Device);
            return pairedDeviceCollection.Count > 0;
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

            WriteConsole("Connecting to " + deviceName + "...");

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
                WriteConsole("Exception in FromIdAsync: " + ex.Message);
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
                WriteConsole("Connect operation threw an exception: " + ex.Message);
                return false;
            }

            WriteConsole("Devices connected on L2, listening on IP Address: " + EndpointPairs[0].LocalHostName + " Port: " + Globals.strServerPort);


            return true;
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
                WriteConsole($"PairAsync failed, Status: {result.Status}");
                return false;
            }
            return true;
        }
        private void OnPairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            Utils.HandlePairing(Dispatcher, args);
        }

        private void OnConnectionStatusChanged(WiFiDirectDevice sender, object args)
        {
            WriteConsole("Connection Status: " + sender.ConnectionStatus);

        }
        private void OnSocketConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                WriteConsole("Connecting to remote side on L4 layer...");
                StreamSocket serverSocket = args.Socket;

                SocketReaderWriter socketRW = new SocketReaderWriter(serverSocket, rootPage);
                // The first message sent is the name of the connection.
                //string message = await socketRW.ReadMessageAsync();

                // Find the pending connection and add it to the list of active connections.
                WiFiDirectDevice wfdDevice;
                if (_pendingConnections.TryRemove(sender, out wfdDevice))
                {
                    ConnectedDevices.Add(new ConnectedDevice(wfdDevice.DeviceId, wfdDevice, socketRW));
                }


                await socketRW.ReadMessageAsync();

            });
        }

        private void OnPubStatusChanged(WiFiDirectAdvertisementPublisher sender, WiFiDirectAdvertisementPublisherStatusChangedEventArgs args)
        {
            WriteConsole($"Advertisement: Status: {args.Status}");
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            string message = string.IsNullOrEmpty(txtMessage.Text) ? "No Data entered.." : txtMessage.Text;
            var connectedDevice = (ConnectedDevice)lvConnectedDevices.SelectedItem;
            if (connectedDevice != null)
            {

                await connectedDevice.SocketRW.WriteMessageAsync(message + " - " + DateTime.Now.ToString("dd/MMM/yyyy hh:mm ss tt"));
            }
            else
            {
                if (ConnectedDevices.Count > 0)
                {
                    foreach (var dev in ConnectedDevices)
                        await dev.SocketRW.WriteMessageAsync(message + " - " + DateTime.Now.ToString("dd/MMM/yyyy hh:mm ss tt"));
                }
            }
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            var discoveredDevice = (DiscoveredDevice)lvDiscoveredDevices.SelectedItem;

            if (discoveredDevice == null)
            {
                await Utils.ShowAlertMessage(Dispatcher, "Device not selected");
                WriteConsole("No device selected, please select one.");
                return;
            }

            WriteConsole($"Connecting to {discoveredDevice.DeviceInfo.Name}...");

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

                WriteConsole($"Devices connected on L2 layer, connecting to IP Address: {remoteHostName} Port: {Globals.strServerPort}");

                // Wait for server to start listening on a socket
                await Task.Delay(2000);

                // Connect to Advertiser on L4 layer
                StreamSocket clientSocket = new StreamSocket();
                await clientSocket.ConnectAsync(remoteHostName, Globals.strServerPort);
                WriteConsole("Connected with remote side on L4 layer");

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
                WriteConsole("FromIdAsync was canceled by user");
            }
            catch (Exception ex)
            {
                await Utils.ShowAlertMessage(Dispatcher, "Error Connection operation");
                WriteConsole($"Connect operation threw an exception: {ex.Message}");
            }
        }
    }
}
