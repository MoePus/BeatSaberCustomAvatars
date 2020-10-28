﻿//  Beat Saber Custom Avatars - Custom player models for body presence in Beat Saber.
//  Copyright © 2018-2020  Beat Saber Custom Avatars Contributors
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.

using CustomAvatar.Logging;
using System;
using System.Collections.Generic;
using Zenject;

namespace CustomAvatar.Tracking
{
    internal class DeviceManager : IInitializable, ITickable, IDisposable
    {
        public event Action devicesChanged;

        private string _head;
        private string _leftHand;
        private string _rightHand;
        private string _waist;
        private string _leftFoot;
        private string _rightFoot;

        private readonly ILogger<DeviceManager> _logger;
        private readonly IDeviceProvider _deviceProvider;

        private readonly Dictionary<string, TrackedDevice> _devices = new Dictionary<string, TrackedDevice>();

        public DeviceManager(ILoggerProvider loggerProvider, IDeviceProvider deviceProvider)
        {
            _logger = loggerProvider.CreateLogger<DeviceManager>();
            _deviceProvider = deviceProvider;
        }

        public bool TryGetDeviceState(DeviceUse use, out TrackedDevice device)
        {
            switch (use)
            {
                case DeviceUse.Head:
                    device = GetDevice(ref _head);
                    return true;

                case DeviceUse.LeftHand:
                    device = GetDevice(ref _leftHand);
                    return true;

                case DeviceUse.RightHand:
                    device = GetDevice(ref _rightHand);
                    return true;

                case DeviceUse.Waist:
                    device = GetDevice(ref _waist);
                    return true;

                case DeviceUse.LeftFoot:
                    device = GetDevice(ref _leftFoot);
                    return true;

                case DeviceUse.RightFoot:
                    device = GetDevice(ref _rightFoot);
                    return true;

                default:
                    device = default;
                    return false;
            }
        }

        public void Initialize()
        {
            _deviceProvider.devicesChanged += OnDevicesChanged;
        }

        public void Tick()
        {
            _deviceProvider.GetDevices(_devices);
        }

        public void Dispose()
        {
            _deviceProvider.devicesChanged -= OnDevicesChanged;
        }

        private TrackedDevice GetDevice(ref string id)
        {
            if (string.IsNullOrEmpty(id)) return default;
            if (!_devices.ContainsKey(id)) return default;

            return _devices[id];
        }

        private void OnDevicesChanged()
        {
            AssignDevices();
            devicesChanged?.Invoke();
        }

        private void AssignDevices()
        {
            string head = null;
            string leftHand = null;
            string rightHand = null;
            string waist = null;
            string leftFoot = null;
            string rightFoot = null;

            foreach (TrackedDevice device in _devices.Values)
            {
                switch (device.deviceUse)
                {
                    case DeviceUse.Head:
                        head = device.id;
                        break;

                    case DeviceUse.LeftHand:
                        leftHand = device.id;
                        break;

                    case DeviceUse.RightHand:
                        rightHand = device.id;
                        break;

                    case DeviceUse.Waist:
                        waist = device.id;
                        break;

                    case DeviceUse.LeftFoot:
                        leftFoot = device.id;
                        break;

                    case DeviceUse.RightFoot:
                        rightFoot = device.id;
                        break;
                }
            }

            AssignDevice(ref _head,      head,      DeviceUse.Head);
            AssignDevice(ref _leftHand,  leftHand,  DeviceUse.LeftHand);
            AssignDevice(ref _rightHand, rightHand, DeviceUse.RightHand);
            AssignDevice(ref _waist,     waist,     DeviceUse.Waist);
            AssignDevice(ref _leftFoot,  leftFoot,  DeviceUse.LeftFoot);
            AssignDevice(ref _rightFoot, rightFoot, DeviceUse.RightFoot);
        }

        private void AssignDevice(ref string current, string potential, DeviceUse use)
        {
            if (current == potential) return;

            if (string.IsNullOrEmpty(potential))
            {
                _logger.Info($"Lost device '{current}' that was used as {use}");

                current = null;
            }
            else
            {
                if (current != null)
                {
                    _logger.Info($"Replacing device '{current}' with '{potential}' as {use}");
                }
                else
                {
                    _logger.Info($"Using device '{potential}' as {use}");
                }

                current = potential;
            }

        }
    }
}
