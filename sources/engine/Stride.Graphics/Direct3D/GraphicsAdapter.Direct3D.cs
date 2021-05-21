// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if STRIDE_GRAPHICS_API_DIRECT3D
// Copyright (c) 2010-2014 SharpDX - Alexandre Mutel
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
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.DXGI;
using Stride.Core;
using ComponentBase = Stride.Core.ComponentBase;
using Utilities = Stride.Core.Utilities;

namespace Stride.Graphics
{
    /// <summary>
    /// Provides methods to retrieve and manipulate graphics adapters. This is the equivalent to <see cref="Adapter1"/>.
    /// </summary>
    /// <msdn-id>ff471329</msdn-id>
    /// <unmanaged>IDXGIAdapter1</unmanaged>
    /// <unmanaged-short>IDXGIAdapter1</unmanaged-short>
    public partial class GraphicsAdapter
    {
        private unsafe readonly ComPtr<IDXGIAdapter1> adapter;
        private readonly int adapterOrdinal;
        private unsafe readonly AdapterDesc1 description;

        private GraphicsProfile minimumUnsupportedProfile = (GraphicsProfile)int.MaxValue;
        private GraphicsProfile maximumSupportedProfile;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphicsAdapter" /> class.
        /// </summary>
        /// <param name="defaultFactory">The default factory.</param>
        /// <param name="adapterOrdinal">The adapter ordinal.</param>
        internal unsafe GraphicsAdapter(IDXGIFactory1* defaultFactory, int adapterOrdinal)
        {
            this.adapterOrdinal = adapterOrdinal;

            var handle = (IDXGIAdapter*)adapter.Handle;
            SilkMarshal.ThrowHResult
            (
                defaultFactory->CreateSoftwareAdapter
                (
                    adapterOrdinal,
                    ref handle
                )
            );
            adapter.DisposeBy(this);

            SilkMarshal.ThrowHResult
            (
                adapter.Handle->GetDesc1(ref description)
            );

            //description.Description = description.Description.TrimEnd('\0'); // for some reason sharpDX returns an adaptater name of fixed size filled with trailing '\0'
            //var nativeOutputs = adapter.Outputs;

            uint i = 0;
            var _outputs = new List<GraphicsOutput>();
            IDXGIOutput* output;
            while (adapter.Handle->EnumOutputs(i, &output) != unchecked((int)DXGIError.NotFound))
            {
                _outputs.Add(new GraphicsOutput(this, (int)i, output));
                ++i;
            }
            outputs = _outputs.ToArray();

            AdapterUid = description.AdapterLuid.Item1.ToString();
        }

        /// <summary>
        /// Gets the description of this adapter.
        /// </summary>
        /// <value>The description.</value>
        public string Description
        {
            get
            {
                unsafe
                {
                    fixed (char* str = description.Description)
                    {
                        return new(str);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the vendor identifier.
        /// </summary>
        /// <value>
        /// The vendor identifier.
        /// </value>
        public int VendorId
        {
            get { return (int)description.VendorId; }
        }

        /// <summary>
        /// Determines if this instance of GraphicsAdapter is the default adapter.
        /// </summary>
        public bool IsDefaultAdapter
        {
            get
            {
                return adapterOrdinal == 0;
            }
        }

        internal unsafe IDXGIAdapter1* NativeAdapter
        {
            get
            {
                return adapter;
            }
        }

        /// <summary>
        /// Tests to see if the adapter supports the requested profile.
        /// </summary>
        /// <param name="graphicsProfile">The graphics profile.</param>
        /// <returns>true if the profile is supported</returns>
        public bool IsProfileSupported(GraphicsProfile graphicsProfile)
        {
#if STRIDE_GRAPHICS_API_DIRECT3D12
            return true;
#else
            // Did we check fo this or a higher profile, and it was supported?
            if (maximumSupportedProfile >= graphicsProfile)
                return true;

            // Did we check for this or a lower profile and it was unsupported?
            if (minimumUnsupportedProfile <= graphicsProfile)
                return false;

            // Check and min/max cached values
            unsafe
            {
                if (SilkHelper.IsSupportedFeatureLevel((IDXGIAdapter*)NativeAdapter, (D3DFeatureLevel)graphicsProfile))
                {
                    maximumSupportedProfile = graphicsProfile;
                    return true;
                }
                else
                {
                    minimumUnsupportedProfile = graphicsProfile;
                    return false;
                }
            }
#endif
        }
    }
}
#endif
