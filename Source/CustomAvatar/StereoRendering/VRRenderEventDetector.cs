//========= Copyright 2016-2017, HTC Corporation. All rights reserved. ===========

using UnityEngine;
using Zenject;

namespace CustomAvatar.StereoRendering
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    internal class VRRenderEventDetector : MonoBehaviour
    {
        public Camera camera { get; private set; }
        public StereoRenderManager manager { get; private set; }

        [Inject]
        private void Inject(StereoRenderManager manager)
        {
            this.manager = manager;
        }

        public void Start()
        {
            camera = GetComponent<Camera>();
        }

        private void OnPreRender()
        {
            if (manager != null)
            {
                manager.InvokeStereoRenderers(this);
            }
            else
            {
                Destroy(this);
            }
        }
    }
}
