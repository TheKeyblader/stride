// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if STRIDE_GRAPHICS_API_DIRECT3D11
// Copyright (c) 2010-2012 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Feature = Silk.NET.Direct3D11.Feature;

namespace Stride.Graphics
{
    /// <summary>
    /// Features supported by a <see cref="GraphicsDevice"/>.
    /// </summary>
    /// <remarks>
    /// This class gives also features for a particular format, using the operator this[dxgiFormat] on this structure.
    /// </remarks>
    public partial struct GraphicsDeviceFeatures
    {
        private static readonly List<Format> ObsoleteFormatToExcludes = new List<Format>() { Format.FormatR1Unorm, Format.FormatB5G6R5Unorm, Format.FormatB5G5R5A1Unorm };

        internal unsafe GraphicsDeviceFeatures(GraphicsDevice deviceRoot)
        {
            var nativeDevice = deviceRoot.NativeDevice;

            HasSRgb = true;

            mapFeaturesPerFormat = new FeaturesPerFormat[256];

            // Set back the real GraphicsProfile that is used
            RequestedProfile = deviceRoot.RequestedProfile;
            CurrentProfile = GraphicsProfileHelper.FromFeatureLevel(nativeDevice->GetFeatureLevel());

            HasResourceRenaming = true;

            FeatureDataFormatSupport2* computeShaders = null;
            SilkMarshal.ThrowHResult(nativeDevice->CheckFeatureSupport(Feature.FeatureFormatSupport2, computeShaders, (uint)sizeof(FeatureDataFormatSupport2)));
            HasComputeShaders = computeShaders != null;

            FeatureDataDoubles* doublePrecision = null;
            SilkMarshal.ThrowHResult(nativeDevice->CheckFeatureSupport(Feature.FeatureDoubles, doublePrecision, (uint)sizeof(FeatureDataDoubles)));
            HasDoublePrecision = doublePrecision != null;

            FeatureDataThreading* threading = null;
            SilkMarshal.ThrowHResult(nativeDevice->CheckFeatureSupport(Feature.FeatureThreading, threading, (uint)sizeof(FeatureDataThreading)));
            HasMultiThreadingConcurrentResources = threading->DriverConcurrentCreates == 1;
            HasDriverCommandLists = threading->DriverCommandLists == 1;

            HasDepthAsSRV = (CurrentProfile >= GraphicsProfile.Level_10_0);
            HasDepthAsReadOnlyRT = CurrentProfile >= GraphicsProfile.Level_11_0;
            HasMultisampleDepthAsSRV = CurrentProfile >= GraphicsProfile.Level_11_0;

            // Check features for each DXGI.Format
            foreach (var format in Enum.GetValues(typeof(Format)))
            {
                var dxgiFormat = (Format)format;
                var maximumMultisampleCount = MultisampleCount.None;
                uint formatSupport = 0;

                if (!ObsoleteFormatToExcludes.Contains(dxgiFormat))
                {
                    maximumMultisampleCount = GetMaximumMultisampleCount(nativeDevice, dxgiFormat);

                    SilkMarshal.ThrowHResult(nativeDevice->CheckFormatSupport(dxgiFormat, ref formatSupport));
                }

                mapFeaturesPerFormat[(int)dxgiFormat] = new FeaturesPerFormat((PixelFormat)dxgiFormat, maximumMultisampleCount, (FormatSupport)formatSupport);
            }
        }

        /// <summary>
        /// Gets the maximum multisample count for a particular <see cref="PixelFormat" />.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="pixelFormat">The pixelFormat.</param>
        /// <returns>The maximum multisample count for this pixel pixelFormat</returns>
        private static unsafe MultisampleCount GetMaximumMultisampleCount(ID3D11Device* device, Format pixelFormat)
        {
            int maxCount = 1;
            for (int i = 1; i <= 8; i *= 2)
            {
                uint result = 0;
                SilkMarshal.ThrowHResult(device->CheckMultisampleQualityLevels(pixelFormat, (uint)i, ref result));
                if (result != 0) maxCount = i;
            }
            return (MultisampleCount)maxCount;
        }
    }
}
#endif
