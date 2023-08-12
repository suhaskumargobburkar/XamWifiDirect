using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFiDirect;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Storage;

namespace XamWifiDirect.UWP.UWPWifiDirect
{
    public class SocketReaderWriter : IDisposable
    {
        DataReader _dataReader;
        DataWriter _dataWriter;
        StreamSocket _streamSocket;
        private MainPage _rootPage;

        public SocketReaderWriter(StreamSocket socket, MainPage mainPage)
        {
            _dataReader = new DataReader(socket.InputStream);
            _dataReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            _dataReader.ByteOrder = ByteOrder.LittleEndian;

            _dataWriter = new DataWriter(socket.OutputStream);
            _dataWriter.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            _dataWriter.ByteOrder = ByteOrder.LittleEndian;

            _streamSocket = socket;
            _rootPage = mainPage;
        }

        public void Dispose()
        {
            _dataReader.Dispose();
            _dataWriter.Dispose();
            _streamSocket.Dispose();
        }

        public async Task WriteMessageAsync(string message)
        {
            try
            {
                _dataWriter.WriteUInt32(_dataWriter.MeasureString(message));
                _dataWriter.WriteString(message);
                await _dataWriter.StoreAsync();
                _rootPage.NotifyUserFromBackground("Sent message: " + message, NotifyType.StatusMessage);
            }
            catch (Exception ex)
            {
                _rootPage.NotifyUserFromBackground("WriteMessage threw exception: " + ex.Message, NotifyType.StatusMessage);
            }
        }

        public async Task<string> ReadMessageAsync()
        {
            try
            {
                uint capacity = 10000000;
                Windows.Storage.Streams.Buffer buffer = new Windows.Storage.Streams.Buffer(capacity);

                /* UInt32 bytesRead = await _dataReader.LoadAsync(sizeof(UInt32));
                 if (bytesRead > 0)
                 {
                     // Determine how long the string is.
                     UInt32 messageLength = _dataReader.ReadUInt32();
                     bytesRead = await _dataReader.LoadAsync(messageLength);
                     if (bytesRead > 0)
                     {
                         // Decode the string.
                         string message = _dataReader.ReadString(messageLength);
                         _rootPage.NotifyUserFromBackground("Got message: " + message, NotifyType.StatusMessage);
                         return message;
                     }
                 }*/

                //Read image from stream
                StorageFile file = await DownloadsFolder.CreateFileAsync("firsttest.jpg", CreationCollisionOption.GenerateUniqueName);

                while (true)
                {

                    await _streamSocket.InputStream.ReadAsync(buffer, capacity, InputStreamOptions.None);

                    if (buffer.Length > 0)
                    {
                        await FileIO.WriteBufferAsync(file, buffer);
                    }
                    else
                    {
                        break;
                    }
                }

                return "done";

            }
            catch (Exception)
            {
                _rootPage.NotifyUserFromBackground("Socket was closed!", NotifyType.StatusMessage);
            }
            return null;
        }
    }

    public class DiscoveredDevice : INotifyPropertyChanged
    {
        public DeviceInformation DeviceInfo { get; private set; }

        public DiscoveredDevice(DeviceInformation deviceInfo)
        {
            DeviceInfo = deviceInfo;
        }

        public string DisplayName => DeviceInfo.Name + " - " + (DeviceInfo.Pairing.IsPaired ? "Paired" : "Unpaired");
        public override string ToString() => DisplayName;

        public void UpdateDeviceInfo(DeviceInformationUpdate update)
        {
            DeviceInfo.Update(update);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DisplayName"));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class ConnectedDevice : IDisposable
    {
        public SocketReaderWriter SocketRW { get; }
        public WiFiDirectDevice WfdDevice { get; }
        public string DisplayName { get; }

        public ConnectedDevice(string displayName, WiFiDirectDevice wfdDevice, SocketReaderWriter socketRW)
        {
            DisplayName = displayName;
            WfdDevice = wfdDevice;
            SocketRW = socketRW;
        }

        public override string ToString() => DisplayName;

        public void Dispose()
        {
            // Close socket
            SocketRW.Dispose();

            // Close WiFiDirectDevice object
            WfdDevice.Dispose();
        }
    }
}
