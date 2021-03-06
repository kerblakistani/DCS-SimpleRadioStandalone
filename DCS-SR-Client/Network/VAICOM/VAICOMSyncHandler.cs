﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.LotATC.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.VAICOM.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.VAICOM
{
    public class VAICOMSyncHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly long UPDATE_SYNC_RATE = 5*1000 * 10000; //There are 10,000 ticks in a millisecond, or 10 million ticks in a second. Update every 5 seconds
        private UdpClient _vaicomUDPListener;
        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
        private volatile bool _stop = false;
        private readonly ClientStateSingleton _clientStateSingleton;

        public VAICOMSyncHandler()
        {
            _clientStateSingleton = ClientStateSingleton.Instance;
        }

        public void Start()
        {
            _vaicomUDPListener = new UdpClient();
            _vaicomUDPListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,
                true);
            _vaicomUDPListener.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.

            var localEp = new IPEndPoint(IPAddress.Any,
                _globalSettings.GetNetworkSetting(GlobalSettingsKeys.VAICOMIncomingUDP));
            _vaicomUDPListener.Client.Bind(localEp);

            Task.Factory.StartNew(() =>
            {
                using (_vaicomUDPListener)
                {
                    while (!_stop)
                    {
                        try
                        {
                            var groupEp = new IPEndPoint(IPAddress.Any,
                            _globalSettings.GetNetworkSetting(GlobalSettingsKeys.VAICOMIncomingUDP));
                            var bytes = _vaicomUDPListener.Receive(ref groupEp);

                            var vaicomMessageWrapper =
                                JsonConvert.DeserializeObject<VAICOMMessageWrapper>(Encoding.UTF8.GetString(
                                    bytes, 0, bytes.Length));

                            if (vaicomMessageWrapper != null )
                            {
                                if (vaicomMessageWrapper.MessageType == 1)
                                {
                                    vaicomMessageWrapper.LastReceivedAt = DateTime.Now.Ticks;
                                    _clientStateSingleton.InhibitTX = vaicomMessageWrapper;
                                }
                            }
                        }
                        catch (SocketException e)
                        {
                            if (!_stop)
                            {
                                Logger.Error(e, "SocketException Handling VAICOM UDP Message");
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Handling VAICOM UDP Message");
                        }
                    }

                    try
                    {
                        _vaicomUDPListener.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping VAICOM UDP listener");
                    }
                }
            });

          
        }
        public void Stop()
        {
            _stop = true;
            try
            {
                _vaicomUDPListener?.Close();
            }
            catch (Exception ex)
            {
            }
        }
    }
}
