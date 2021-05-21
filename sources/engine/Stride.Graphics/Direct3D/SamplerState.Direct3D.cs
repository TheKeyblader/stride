// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if STRIDE_GRAPHICS_API_DIRECT3D11
using System;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

using Stride.Core.Mathematics;

namespace Stride.Graphics
{
    /// <summary>
    /// Describes a sampler state used for texture sampling.
    /// </summary>
    public partial class SamplerState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SamplerState"/> class.
        /// </summary>
        /// <param name="device">The device.</param>
        /// <param name="name">The name.</param>
        /// <param name="samplerStateDescription">The sampler state description.</param>
        private SamplerState(GraphicsDevice device, SamplerStateDescription samplerStateDescription) : base(device)
        {
            Description = samplerStateDescription;

            CreateNativeDeviceChild();
        }

        /// <inheritdoc/>
        protected internal override bool OnRecreate()
        {
            base.OnRecreate();
            CreateNativeDeviceChild();
            return true;
        }

        private unsafe void CreateNativeDeviceChild()
        {
            SamplerDesc nativeDescription;

            nativeDescription.AddressU = (Silk.NET.Direct3D11.TextureAddressMode)Description.AddressU;
            nativeDescription.AddressV = (Silk.NET.Direct3D11.TextureAddressMode)Description.AddressV;
            nativeDescription.AddressW = (Silk.NET.Direct3D11.TextureAddressMode)Description.AddressW;
            //TODO nativeDescription.BorderColor = ColorHelper.Convert(Description.BorderColor);
            nativeDescription.ComparisonFunc = (ComparisonFunc)Description.CompareFunction;
            nativeDescription.Filter = (Filter)Description.Filter;
            nativeDescription.MaxAnisotropy = (uint)Description.MaxAnisotropy;
            nativeDescription.MaxLOD = Description.MaxMipLevel;
            nativeDescription.MinLOD = Description.MinMipLevel;
            nativeDescription.MipLODBias = Description.MipMapLevelOfDetailBias;

            // For 9.1, anisotropy cannot be larger then 2
            // mirror once is not supported either
            if (GraphicsDevice.Features.CurrentProfile == GraphicsProfile.Level_9_1)
            {
                // TODO: Min with user-value instead?
                nativeDescription.MaxAnisotropy = 2;

                if (nativeDescription.AddressU == Silk.NET.Direct3D11.TextureAddressMode.TextureAddressMirrorOnce)
                    nativeDescription.AddressU = Silk.NET.Direct3D11.TextureAddressMode.TextureAddressMirror;
                if (nativeDescription.AddressV == Silk.NET.Direct3D11.TextureAddressMode.TextureAddressMirrorOnce)
                    nativeDescription.AddressV = Silk.NET.Direct3D11.TextureAddressMode.TextureAddressMirror;
                if (nativeDescription.AddressW == Silk.NET.Direct3D11.TextureAddressMode.TextureAddressMirrorOnce)
                    nativeDescription.AddressW = Silk.NET.Direct3D11.TextureAddressMode.TextureAddressMirror;
            }

            SilkMarshal.ThrowHResult(NativeDevice->CreateSamplerState(ref nativeDescription, (ID3D11SamplerState**)NativeDeviceChild->LpVtbl));
        }
    }
}
#endif
