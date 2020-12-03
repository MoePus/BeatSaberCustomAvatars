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

using CustomAvatar.Avatar;
using CustomAvatar.Logging;
using CustomAvatar.Utilities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace CustomAvatar.Lighting
{
    internal class DynamicLightingController : MonoBehaviour
    {
        private ILogger<DynamicLightingController> _logger;
        private LightWithIdManager _lightManager;
        private DiContainer _container;

        private List<DynamicTubeBloomPrePassLight>[] _lights;
        private List<(DirectionalLight, Light)> _directionalLights;
        
        #region Behaviour Lifecycle
        #pragma warning disable IDE0051

        [Inject]
        private void Inject(ILoggerProvider loggerProvider, LightWithIdManager lightManager, DiContainer container)
        {
            name = nameof(DynamicLightingController);

            _logger = loggerProvider.CreateLogger<DynamicLightingController>();
            _lightManager = lightManager;
            _container = container;
        }

        private void Start()
        {
            _lightManager.didSetColorForIdEvent += OnSetColorForId;
            _lightManager.didChangeSomeColorsThisFrameEvent += OnChangedSomeColorsThisFrame;

            CreateLights();
        }

        private void OnDestroy()
        {
            _lightManager.didSetColorForIdEvent -= OnSetColorForId;
            _lightManager.didChangeSomeColorsThisFrameEvent -= OnChangedSomeColorsThisFrame;
        }

        #pragma warning restore IDE0051
        #endregion

        private void CreateLights()
        {
            List<ILightWithId>[] lightsWithId = _lightManager.GetPrivateField<List<ILightWithId>[]>("_lights");
            int maxLightId = _lightManager.GetPrivateField<int>("kMaxLightId");

            _lights = new List<DynamicTubeBloomPrePassLight>[maxLightId + 1];
            
            for (int id = 0; id < lightsWithId.Length; id++)
            {
                if (lightsWithId[id] == null) continue;

                foreach (ILightWithId lightWithId in lightsWithId[id])
                {
                    if (lightWithId is TubeBloomPrePassLightWithId tubeLightWithId)
                    {
                        TubeBloomPrePassLight tubeLight = tubeLightWithId.GetPrivateField<TubeBloomPrePassLight>("_tubeBloomPrePassLight");

                        DynamicTubeBloomPrePassLight light = _container.InstantiateComponent<DynamicTubeBloomPrePassLight>(new GameObject($"DynamicTubeBloomPrePassLight({tubeLight.name})"), new[] { tubeLight });

                        if (_lights[id] == null)
                        {
                            _lights[id] = new List<DynamicTubeBloomPrePassLight>(10);
                        }

                        _lights[id].Add(light);
                    }
                }
            }

            _directionalLights = new List<(DirectionalLight, Light)>();

            foreach (var directionalLight in DirectionalLight.lights)
            {
                Light light = new GameObject($"DynamicDirectionalLight({directionalLight.name})").AddComponent<Light>();

                light.type = LightType.Directional;
                light.color = directionalLight.color;
                light.intensity = 1;
                light.cullingMask = AvatarLayers.kAllLayersMask;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 1;

                light.transform.parent = transform;
                light.transform.position = Vector3.zero;
                light.transform.rotation = directionalLight.transform.rotation;

                _directionalLights.Add((directionalLight, light));
            }

            _logger.Trace($"Created {_lights.Sum(l => l?.Count)} DynamicTubeBloomPrePassLights");
        }

        private void OnSetColorForId(int id, Color color)
        {
            if (_lights[id] != null)
            {
                foreach (DynamicTubeBloomPrePassLight light in _lights[id])
                {
                    light.color = color;
                }
            }
        }

        private void OnChangedSomeColorsThisFrame()
        {
            foreach ((DirectionalLight directionalLight, Light light) in _directionalLights)
            {
                light.color = directionalLight.color;
                light.intensity = directionalLight.intensity * 0.1f;
            }
        }
    }
}
