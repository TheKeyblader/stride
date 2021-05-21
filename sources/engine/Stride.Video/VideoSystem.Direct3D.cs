// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#if STRIDE_GRAPHICS_API_DIRECT3D11

using System;
using SharpDX.Direct3D;
using SharpDX.MediaFoundation;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Stride.Games;

namespace Stride.Video
{
    public partial class VideoSystem
    {
        public DXGIDeviceManager DxgiDeviceManager;

        public unsafe override void Initialize()
        {
            base.Initialize();

            var graphicsDevice = Services.GetService<IGame>().GraphicsDevice;

            DxgiDeviceManager = new DXGIDeviceManager();
            DxgiDeviceManager.ResetDevice(new SharpDX.ComObject(new IntPtr(graphicsDevice.NativeDevice)));

            //Add multi thread protection on device
            ID3D11Multithread* mt = null;
            SilkMarshal.ThrowHResult(graphicsDevice.NativeDevice->QueryInterface(ref SilkMarshal.GuidOf<ID3D11Multithread>(), (void**)&mt));
            mt->SetMultithreadProtected(1);

            MediaManager.Startup();
        }
    }
}

#endif
