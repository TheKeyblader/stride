// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if STRIDE_GRAPHICS_API_DIRECT3D11
using System;

using Silk.NET.Direct3D11;

namespace Stride.Graphics
{
    /// <summary>
    /// GraphicsResource class
    /// </summary>
    public abstract partial class GraphicsResource
    {
        private unsafe ID3D11ShaderResourceView* shaderResourceView;
        private unsafe ID3D11UnorderedAccessView* unorderedAccessView;
        internal bool DiscardNextMap; // Used to internally force a WriteDiscard (to force a rename) with the GraphicsResourceAllocator

        protected bool IsDebugMode
        {
            get
            {
                return GraphicsDevice != null && GraphicsDevice.IsDebugMode;
            }
        }

        protected unsafe override void OnNameChanged()
        {
            base.OnNameChanged();
            if (IsDebugMode)
            {
                if (this.shaderResourceView != null)
                {
                    ((ID3D11DeviceChild*)shaderResourceView)->SetDebugName(Name == null ? null : $"{Name} SRV");
                }

                if (this.unorderedAccessView != null)
                {
                    ((ID3D11DeviceChild*)unorderedAccessView)->SetDebugName(Name == null ? null : $"{Name} UAV");
                }
            }
        }

        /// <summary>
        /// Gets or sets the ShaderResourceView attached to this GraphicsResource.
        /// Note that only Texture, Texture3D, RenderTarget2D, RenderTarget3D, DepthStencil are using this ShaderResourceView
        /// </summary>
        /// <value>The device child.</value>
        protected internal unsafe ID3D11ShaderResourceView* NativeShaderResourceView
        {
            get
            {
                return shaderResourceView;
            }
            set
            {
                shaderResourceView = value;

                if (IsDebugMode && shaderResourceView != null)
                {
                    ((ID3D11DeviceChild*)shaderResourceView)->SetDebugName(Name == null ? null : $"{Name} SRV");
                }
            }
        }

        /// <summary>
        /// Gets or sets the UnorderedAccessView attached to this GraphicsResource.
        /// </summary>
        /// <value>The device child.</value>
        protected internal unsafe ID3D11UnorderedAccessView* NativeUnorderedAccessView
        {
            get
            {
                return unorderedAccessView;
            }
            set
            {
                unorderedAccessView = value;

                if (IsDebugMode && unorderedAccessView != null)
                {
                    ((ID3D11DeviceChild*)unorderedAccessView)->SetDebugName(Name == null ? null : $"{Name} UAV");
                }
            }
        }

        protected internal unsafe override void OnDestroyed()
        {
            shaderResourceView->Release();
            unorderedAccessView->Release();

            base.OnDestroyed();
        }
    }
}
 
#endif
