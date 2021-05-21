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
using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Stride.Core;

namespace Stride.Graphics
{
    public partial class Texture
    {
        private unsafe ID3D11RenderTargetView* renderTargetView;
        private unsafe ID3D11DepthStencilView* depthStencilView;
        internal bool HasStencil;

        private int TexturePixelSize => Format.SizeInBytes();
        private const int TextureRowPitchAlignment = 1;
        private const int TextureSubresourceAlignment = 1;

        internal unsafe ID3D11DepthStencilView* NativeDepthStencilView
        {
            get
            {
                return depthStencilView;
            }
            private set
            {
                depthStencilView = value;
                if (IsDebugMode && depthStencilView != null)
                {
                    ((ID3D11DeviceChild*)depthStencilView)->SetDebugName(string.Format("{0} DSV", Name));
                }
            }
        }

        /// <summary>
        /// Gets the RenderTargetView attached to this GraphicsResource.
        /// Note that only Texture, Texture3D, RenderTarget2D, RenderTarget3D, DepthStencil are using this ShaderResourceView
        /// </summary>
        /// <value>The device child.</value>
        internal unsafe ID3D11RenderTargetView* NativeRenderTargetView
        {
            get
            {
                return renderTargetView;
            }
            private set
            {
                renderTargetView = value;
                if (IsDebugMode && renderTargetView != null)
                {
                    ((ID3D11DeviceChild*)renderTargetView)->SetDebugName(string.Format("{0} RTV", Name));
                }
            }
        }

        public void Recreate(DataBox[] dataBoxes = null)
        {
            InitializeFromImpl(dataBoxes);
        }

        public static bool IsDepthStencilReadOnlySupported(GraphicsDevice device)
        {
            return device.Features.CurrentProfile >= GraphicsProfile.Level_11_0;
        }

        /// <summary>
        /// Initializes from a native SharpDX.Texture
        /// </summary>
        /// <param name="texture">The texture.</param>
        internal unsafe Texture InitializeFromImpl(ID3D11Texture2D* texture, bool isSrgb)
        {
            NativeDeviceChild = (ID3D11Resource*)texture;

            Texture2DDesc desc = default;
            texture->GetDesc(ref desc);
            var newTextureDescription = ConvertFromNativeDescription(desc);

            // We might have created the swapchain as a non-srgb format (esp on Win10&RT) but we want it to behave like it is (esp. for the view and render target)
            if (isSrgb)
                newTextureDescription.Format = newTextureDescription.Format.ToSRgb();

            return InitializeFrom(newTextureDescription);
        }

        internal unsafe Texture InitializeFromImpl(ID3D11ShaderResourceView* srv)
        {
            ShaderResourceViewDesc* desc = null;
            srv->GetDesc(desc);
            if (desc->ViewDimension == D3DSrvDimension.D3D11SrvDimensionTexture2D)
            {
                if (srv != null) srv->AddRef();
                NativeShaderResourceView = srv;
                ID3D11Resource* dxTexture2D = null;
                srv->GetResource(ref dxTexture2D);
                NativeDeviceChild = dxTexture2D;

                Texture2DDesc texDesc = default;
                ((ID3D11Texture2D*)dxTexture2D)->GetDesc(ref texDesc);
                var newTextureDescription = ConvertFromNativeDescription(texDesc);
                var newTextureViewDescription = new TextureViewDescription();
                newTextureViewDescription.Format = (PixelFormat)desc->Format;
                newTextureViewDescription.Flags = newTextureDescription.Flags;

                return InitializeFrom(null, newTextureDescription, newTextureViewDescription, null);
            }
            else
            {
                throw new NotImplementedException("Creating a texture from a SRV with dimension " + desc->ViewDimension + " is not implemented");
            }
        }

        internal unsafe void SwapInternal(Texture other)
        {
            var deviceChild = NativeDeviceChild;
            NativeDeviceChild = other.NativeDeviceChild;
            other.NativeDeviceChild = deviceChild;

            var srv = NativeShaderResourceView;
            NativeShaderResourceView = other.NativeShaderResourceView;
            other.NativeShaderResourceView = srv;

            var uav = NativeUnorderedAccessView;
            NativeUnorderedAccessView = other.NativeUnorderedAccessView;
            other.NativeUnorderedAccessView = uav;

            Utilities.Swap(ref *renderTargetView, ref *other.renderTargetView);
            Utilities.Swap(ref *depthStencilView, ref *other.depthStencilView);
            Utilities.Swap(ref HasStencil, ref other.HasStencil);
        }

        private unsafe void InitializeFromImpl(DataBox[] dataBoxes = null)
        {
            if (ParentTexture != null)
            {
                NativeDeviceChild = ParentTexture.NativeDeviceChild;
            }

            if (NativeDeviceChild == null)
            {
                switch (Dimension)
                {
                    case TextureDimension.Texture1D:
                        var desc1D = ConvertToNativeDescription1D();
                        SilkMarshal.ThrowHResult(GraphicsDevice.NativeDevice->CreateTexture1D(
                            ref desc1D, ConvertDataBoxes(dataBoxes), (ID3D11Texture1D**)NativeDeviceChild->LpVtbl));
                        break;
                    case TextureDimension.Texture2D:
                    case TextureDimension.TextureCube:
                        var desc2D = ConvertToNativeDescription2D();
                        SilkMarshal.ThrowHResult(GraphicsDevice.NativeDevice->CreateTexture2D(
                            ref desc2D, ConvertDataBoxes(dataBoxes), (ID3D11Texture2D**)NativeDeviceChild->LpVtbl));
                        break;
                    case TextureDimension.Texture3D:
                        var desc3D = ConvertToNativeDescription3D();
                        SilkMarshal.ThrowHResult(GraphicsDevice.NativeDevice->CreateTexture3D(
                            ref desc3D, ConvertDataBoxes(dataBoxes), (ID3D11Texture3D**)NativeDeviceChild->LpVtbl));
                        break;
                }

                GraphicsDevice.RegisterTextureMemoryUsage(SizeInBytes);
            }

            if (NativeShaderResourceView == null)
                NativeShaderResourceView = GetShaderResourceView(ViewType, ArraySlice, MipLevel);
            NativeUnorderedAccessView = GetUnorderedAccessView(ViewType, ArraySlice, MipLevel);
            NativeRenderTargetView = GetRenderTargetView(ViewType, ArraySlice, MipLevel);
            NativeDepthStencilView = GetDepthStencilView(out HasStencil);

            switch (textureDescription.Options)
            {
                case TextureOptions.None:
                    SharedHandle = IntPtr.Zero;
                    break;
                case TextureOptions.Shared:
                    void* ptr = null;
                    NativeDeviceChild->QueryInterface(ref SilkMarshal.GuidOf<IDXGIResource>(), ref ptr);
                    SharedHandle = new IntPtr(ptr);
                    break;
#if STRIDE_GRAPHICS_API_DIRECT3D11
                case TextureOptions.SharedNthandle | TextureOptions.SharedKeyedmutex:
                    void* ptr2 = null;
                    NativeDeviceChild->QueryInterface(ref SilkMarshal.GuidOf<IDXGIResource>(), ref ptr2);
                    var uniqueName = "Stride:" + Guid.NewGuid().ToString();
                    fixed (char* _uniqueName = uniqueName)
                    {
                        void* ptr3 = null;
                        SilkMarshal.ThrowHResult(((IDXGIResource1*)ptr2)->CreateSharedHandle(null, 1, _uniqueName, ref ptr3));
                        SharedHandle = new IntPtr(ptr3);
                    }
                    SharedNtHandleName = uniqueName;
                    break;
#endif
                default:
                    throw new ArgumentOutOfRangeException("textureDescription.Options");
            }
        }

        protected internal unsafe override void OnDestroyed()
        {
            // If it was a View, do not release reference
            if (ParentTexture != null)
            {
                NativeDeviceChild = null;
            }
            else if (GraphicsDevice != null)
            {
                GraphicsDevice.RegisterTextureMemoryUsage(-SizeInBytes);
            }

            renderTargetView->Release();
            depthStencilView->Release();

            base.OnDestroyed();
        }

        private void OnRecreateImpl()
        {
            // Dependency: wait for underlying texture to be recreated
            if (ParentTexture != null && ParentTexture.LifetimeState != GraphicsResourceLifetimeState.Active)
                return;

            // Render Target / Depth Stencil are considered as "dynamic"
            if ((Usage == GraphicsResourceUsage.Immutable
                    || Usage == GraphicsResourceUsage.Default)
                && !IsRenderTarget && !IsDepthStencil)
                return;

            if (ParentTexture == null && GraphicsDevice != null)
            {
                GraphicsDevice.RegisterTextureMemoryUsage(-SizeInBytes);
            }

            InitializeFromImpl();
        }

        /// <summary>
        /// Gets a specific <see cref="ShaderResourceView" /> from this texture.
        /// </summary>
        /// <param name="viewType">Type of the view slice.</param>
        /// <param name="arrayOrDepthSlice">The texture array slice index.</param>
        /// <param name="mipIndex">The mip map slice index.</param>
        /// <returns>An <see cref="ShaderResourceView" /></returns>
        private unsafe ID3D11ShaderResourceView* GetShaderResourceView(ViewType viewType, int arrayOrDepthSlice, int mipIndex)
        {
            if (!IsShaderResource)
                return null;

            int arrayCount;
            int mipCount;
            GetViewSliceBounds(viewType, ref arrayOrDepthSlice, ref mipIndex, out arrayCount, out mipCount);

            // Create the view
            var srvDescription = new ShaderResourceViewDesc() { Format = ComputeShaderResourceViewFormat() };

            // Initialize for texture arrays or texture cube
            if (this.ArraySize > 1)
            {
                // If texture cube
                if (this.ViewDimension == TextureDimension.TextureCube)
                {
                    srvDescription.ViewDimension = D3DSrvDimension.D3DSrvDimensionTexturecube;
                    srvDescription.TextureCube = new TexcubeSrv((uint)mipIndex, (uint)mipCount);
                }
                else
                {
                    // Else regular Texture array
                    // Multisample?
                    if (IsMultisample)
                    {
                        if (Dimension != TextureDimension.Texture2D)
                        {
                            throw new NotSupportedException("Multisample is only supported for 2D Textures");
                        }

                        srvDescription.ViewDimension = D3DSrvDimension.D3DSrvDimensionTexture2Dmsarray;
                        srvDescription.Texture2DMSArray = new Tex2DmsArraySrv((uint)arrayOrDepthSlice, (uint)arrayCount);
                    }
                    else
                    {
                        srvDescription.ViewDimension = ViewDimension == TextureDimension.Texture2D ? D3DSrvDimension.D3DSrvDimensionTexture2Darray : D3DSrvDimension.D3DSrvDimensionTexture1Darray;
                        srvDescription.Texture2DArray = new Tex2DArraySrv((uint)mipIndex, (uint)mipCount, (uint)arrayOrDepthSlice, (uint)arrayCount);
                    }
                }
            }
            else
            {
                if (IsMultisample)
                {
                    if (ViewDimension != TextureDimension.Texture2D)
                    {
                        throw new NotSupportedException("Multisample is only supported for 2D Textures");
                    }

                    srvDescription.ViewDimension = D3DSrvDimension.D3DSrvDimensionTexture2Dms;
                }
                else
                {
                    switch (ViewDimension)
                    {
                        case TextureDimension.Texture1D:
                            srvDescription.ViewDimension = D3DSrvDimension.D3DSrvDimensionTexture1D;
                            break;
                        case TextureDimension.Texture2D:
                            srvDescription.ViewDimension = D3DSrvDimension.D3DSrvDimensionTexture2D;
                            break;
                        case TextureDimension.Texture3D:
                            srvDescription.ViewDimension = D3DSrvDimension.D3DSrvDimensionTexture3D;
                            break;
                        case TextureDimension.TextureCube:
                            throw new NotSupportedException("TextureCube dimension is expecting an arraysize > 1");
                    }
                    // Use srvDescription.Texture as it matches also Texture and Texture3D memory layout
                    srvDescription.Texture1D = new Tex1DSrv((uint)mipIndex, (uint)mipCount);
                }
            }

            ID3D11ShaderResourceView* ptr = null;
            SilkMarshal.ThrowHResult(GraphicsDevice.NativeDevice->CreateShaderResourceView(
                NativeResource, ref srvDescription, ref ptr));

            // Default ShaderResourceView
            return ptr;
        }

        /// <summary>
        /// Gets a specific <see cref="RenderTargetView" /> from this texture.
        /// </summary>
        /// <param name="viewType">Type of the view slice.</param>
        /// <param name="arrayOrDepthSlice">The texture array slice index.</param>
        /// <param name="mipIndex">Index of the mip.</param>
        /// <returns>An <see cref="RenderTargetView" /></returns>
        /// <exception cref="System.NotSupportedException">ViewSlice.MipBand is not supported for render targets</exception>
        private unsafe ID3D11RenderTargetView* GetRenderTargetView(ViewType viewType, int arrayOrDepthSlice, int mipIndex)
        {
            if (!IsRenderTarget)
                return null;

            if (viewType == ViewType.MipBand)
                throw new NotSupportedException("ViewSlice.MipBand is not supported for render targets");

            int arrayCount;
            int mipCount;
            GetViewSliceBounds(viewType, ref arrayOrDepthSlice, ref mipIndex, out arrayCount, out mipCount);

            // Create the render target view
            var rtvDescription = new RenderTargetViewDesc() { Format = (Format)ViewFormat };

            if (this.ArraySize > 1)
            {
                if (this.MultisampleCount > MultisampleCount.None)
                {
                    if (ViewDimension != TextureDimension.Texture2D)
                    {
                        throw new NotSupportedException("Multisample is only supported for 2D Textures");
                    }

                    rtvDescription.ViewDimension = RtvDimension.RtvDimensionTexture2Dmsarray;
                    rtvDescription.Texture2DMSArray = new Tex2DmsArrayRtv((uint)arrayOrDepthSlice, (uint)arrayCount);
                }
                else
                {
                    if (ViewDimension == TextureDimension.Texture3D)
                    {
                        throw new NotSupportedException("Texture Array is not supported for Texture3D");
                    }

                    rtvDescription.ViewDimension = Dimension == TextureDimension.Texture2D || Dimension == TextureDimension.TextureCube ? RtvDimension.RtvDimensionTexture2Darray : RtvDimension.RtvDimensionTexture1Darray;

                    // Use rtvDescription.Texture1DArray as it matches also Texture memory layout
                    rtvDescription.Texture1DArray = new Tex1DArrayRtv((uint)mipIndex, (uint)arrayOrDepthSlice, (uint)arrayCount);
                }
            }
            else
            {
                if (IsMultisample)
                {
                    if (ViewDimension != TextureDimension.Texture2D)
                    {
                        throw new NotSupportedException("Multisample is only supported for 2D RenderTarget Textures");
                    }

                    rtvDescription.ViewDimension = RtvDimension.RtvDimensionTexture2Dms;
                }
                else
                {
                    switch (ViewDimension)
                    {
                        case TextureDimension.Texture1D:
                            rtvDescription.ViewDimension = RtvDimension.RtvDimensionTexture1D;
                            rtvDescription.Texture1D = new Tex1DRtv((uint)mipIndex);
                            break;
                        case TextureDimension.Texture2D:
                            rtvDescription.ViewDimension = RtvDimension.RtvDimensionTexture2D;
                            rtvDescription.Texture2D = new Tex2DRtv((uint)mipIndex);
                            break;
                        case TextureDimension.Texture3D:
                            rtvDescription.ViewDimension = RtvDimension.RtvDimensionTexture3D;
                            rtvDescription.Texture3D = new Tex3DRtv((uint)mipIndex, (uint)arrayOrDepthSlice, (uint)arrayCount);
                            break;
                        case TextureDimension.TextureCube:
                            throw new NotSupportedException("TextureCube dimension is expecting an arraysize > 1");
                    }
                }
            }

            ID3D11RenderTargetView* ptr = null;
            SilkMarshal.ThrowHResult(GraphicsDevice.NativeDevice->CreateRenderTargetView(
                NativeResource, ref rtvDescription, ref ptr));


            return ptr;
        }

        /// <summary>
        /// Gets a specific <see cref="UnorderedAccessView" /> from this texture.
        /// </summary>
        /// <param name="viewType">The desired view type on the unordered resource</param>
        /// <param name="arrayOrDepthSlice">The texture array slice index.</param>
        /// <param name="mipIndex">Index of the mip.</param>
        /// <returns>An <see cref="UnorderedAccessView" /></returns>
        private unsafe ID3D11UnorderedAccessView* GetUnorderedAccessView(ViewType viewType, int arrayOrDepthSlice, int mipIndex)
        {
            if (!IsUnorderedAccess)
                return null;

            if (IsMultisample)
                throw new NotSupportedException("Multisampling is not supported for unordered access views");

            int arrayCount;
            int mipCount;
            GetViewSliceBounds(viewType, ref arrayOrDepthSlice, ref mipIndex, out arrayCount, out mipCount);

            var uavDescription = new UnorderedAccessViewDesc
            {
                Format = (Format)ViewFormat,
            };

            if (ArraySize > 1)
            {
                switch (ViewDimension)
                {
                    case TextureDimension.Texture1D:
                        uavDescription.ViewDimension = UavDimension.UavDimensionTexture1Darray;
                        break;
                    case TextureDimension.TextureCube:
                    case TextureDimension.Texture2D:
                        uavDescription.ViewDimension = UavDimension.UavDimensionTexture2Darray;
                        break;
                    case TextureDimension.Texture3D:
                        throw new NotSupportedException("Texture 3D is not supported for Texture Arrays");
                }

                uavDescription.Texture1DArray = new Tex1DArrayUav((uint)mipIndex, (uint)arrayOrDepthSlice, (uint)arrayCount);
            }
            else
            {
                switch (ViewDimension)
                {
                    case TextureDimension.Texture1D:
                        uavDescription.ViewDimension = UavDimension.UavDimensionTexture1D;
                        uavDescription.Texture1D = new Tex1DUav((uint)mipIndex);
                        break;
                    case TextureDimension.Texture2D:
                        uavDescription.ViewDimension = UavDimension.UavDimensionTexture2D;
                        uavDescription.Texture2D = new Tex2DUav((uint)mipIndex);
                        break;
                    case TextureDimension.Texture3D:
                        uavDescription.ViewDimension = UavDimension.UavDimensionTexture3D;
                        uavDescription.Texture3D = new Tex3DUav((uint)mipIndex, (uint)arrayOrDepthSlice, (uint)arrayCount);
                        break;
                    case TextureDimension.TextureCube:
                        throw new NotSupportedException("TextureCube dimension is expecting an array size > 1");
                }
            }

            ID3D11UnorderedAccessView* ptr = null;
            SilkMarshal.ThrowHResult(GraphicsDevice.NativeDevice->CreateUnorderedAccessView(
                NativeResource,
                ref uavDescription,
                ref ptr));

            return ptr;
        }

        private unsafe ID3D11DepthStencilView* GetDepthStencilView(out bool hasStencil)
        {
            hasStencil = false;
            if (!IsDepthStencil)
                return null;

            // Check that the format is supported
            if (ComputeShaderResourceFormatFromDepthFormat(ViewFormat) == PixelFormat.None)
                throw new NotSupportedException("Depth stencil format [{0}] not supported".ToFormat(ViewFormat));

            // Setup the HasStencil flag
            hasStencil = IsStencilFormat(ViewFormat);

            // Create a Depth stencil view on this texture2D
            var depthStencilViewDescription = new DepthStencilViewDesc
            {
                Format = ComputeDepthViewFormatFromTextureFormat(ViewFormat),
            };

            if (ArraySize > 1)
            {
                depthStencilViewDescription.ViewDimension = DsvDimension.DsvDimensionTexture2Darray;
                depthStencilViewDescription.Texture2DArray = new Tex2DArrayDsv(arraySize: (uint)ArraySize);
            }
            else
            {
                depthStencilViewDescription.ViewDimension = DsvDimension.DsvDimensionTexture2D;
                depthStencilViewDescription.Texture2D = new Tex2DDsv(0);
            }

            if (MultisampleCount > MultisampleCount.None)
                depthStencilViewDescription.ViewDimension = DsvDimension.DsvDimensionTexture2D;

            if (IsDepthStencilReadOnly)
            {
                if (!IsDepthStencilReadOnlySupported(GraphicsDevice))
                    throw new NotSupportedException("Cannot instantiate ReadOnly DepthStencilBuffer. Not supported on this device.");

                // Create a Depth stencil view on this texture2D
                depthStencilViewDescription.Flags = (uint)DsvFlag.DsvReadOnlyDepth;
                if (HasStencil)
                    depthStencilViewDescription.Flags |= (uint)DsvFlag.DsvReadOnlyStencil;
            }

            ID3D11DepthStencilView* ptr = null;
            SilkMarshal.ThrowHResult(GraphicsDevice.NativeDevice->CreateDepthStencilView(
                NativeResource, ref depthStencilViewDescription, ref ptr));

            return ptr;
        }

        internal static BindFlag GetBindFlagsFromTextureFlags(TextureFlags flags)
        {
            BindFlag result = 0;
            if ((flags & TextureFlags.ShaderResource) != 0)
                result |= BindFlag.BindShaderResource;
            if ((flags & TextureFlags.RenderTarget) != 0)
                result |= BindFlag.BindRenderTarget;
            if ((flags & TextureFlags.UnorderedAccess) != 0)
                result |= BindFlag.BindUnorderedAccess;
            if ((flags & TextureFlags.DepthStencil) != 0)
                result |= BindFlag.BindDepthStencil;

            return result;
        }

        internal static unsafe SubresourceData* ConvertDataBoxes(DataBox[] dataBoxes)
        {
            if (dataBoxes == null || dataBoxes.Length == 0)
                return null;

            var sharpDXDataBoxes = new SubresourceData[dataBoxes.Length];
            fixed (void* pDataBoxes = sharpDXDataBoxes)
            {
                Utilities.Write((IntPtr)pDataBoxes, dataBoxes, 0, dataBoxes.Length);

                return (SubresourceData*)pDataBoxes;
            }
        }

        private bool IsFlipped()
        {
            return false;
        }

        private Texture1DDesc ConvertToNativeDescription1D()
        {
            var desc = new Texture1DDesc
            {
                Width = (uint)textureDescription.Width,
                ArraySize = 1,
                BindFlags = (uint)GetBindFlagsFromTextureFlags(textureDescription.Flags),
                Format = (Format)textureDescription.Format,
                MipLevels = (uint)textureDescription.MipLevels,
                Usage = (Usage)textureDescription.Usage,
                CPUAccessFlags = (uint)GetCpuAccessFlagsFromUsage(textureDescription.Usage),
                MiscFlags = (uint)textureDescription.Options,
            };
            return desc;
        }

        private Format ComputeShaderResourceViewFormat()
        {
            // Special case for DepthStencil ShaderResourceView that are bound as Float
            var viewFormat = (Format)ViewFormat;
            if (IsDepthStencil)
            {
                viewFormat = (Format)ComputeShaderResourceFormatFromDepthFormat(ViewFormat);
            }

            return viewFormat;
        }

        private static TextureDescription ConvertFromNativeDescription(Texture2DDesc description)
        {
            var desc = new TextureDescription()
            {
                Dimension = TextureDimension.Texture2D,
                Width = (int)description.Width,
                Height = (int)description.Height,
                Depth = 1,
                MultisampleCount = (MultisampleCount)description.SampleDesc.Count,
                Format = (PixelFormat)description.Format,
                MipLevels = (int)description.MipLevels,
                Usage = (GraphicsResourceUsage)description.Usage,
                ArraySize = (int)description.ArraySize,
                Flags = TextureFlags.None,
                Options = TextureOptions.None
            };

            if ((description.BindFlags & (uint)BindFlag.BindRenderTarget) != 0)
                desc.Flags |= TextureFlags.RenderTarget;
            if ((description.BindFlags & (uint)BindFlag.BindUnorderedAccess) != 0)
                desc.Flags |= TextureFlags.UnorderedAccess;
            if ((description.BindFlags & (uint)BindFlag.BindDepthStencil) != 0)
                desc.Flags |= TextureFlags.DepthStencil;
            if ((description.BindFlags & (uint)BindFlag.BindShaderResource) != 0)
                desc.Flags |= TextureFlags.ShaderResource;

            if ((description.MiscFlags & (uint)ResourceMiscFlag.ResourceMiscShared) != 0)
                desc.Options |= TextureOptions.Shared;
#if STRIDE_GRAPHICS_API_DIRECT3D11
            if ((description.MiscFlags & (uint)ResourceMiscFlag.ResourceMiscSharedKeyedmutex) != 0)
                desc.Options |= TextureOptions.SharedKeyedmutex;
            if ((description.MiscFlags & (uint)ResourceMiscFlag.ResourceMiscSharedNthandle) != 0)
                desc.Options |= TextureOptions.SharedNthandle;
#endif
            return desc;
        }

        private Texture2DDesc ConvertToNativeDescription2D()
        {
            var format = (Format)textureDescription.Format;
            var flags = textureDescription.Flags;

            // If the texture is going to be bound on the depth stencil, for to use TypeLess format
            if (IsDepthStencil)
            {
                if (IsShaderResource && GraphicsDevice.Features.CurrentProfile < GraphicsProfile.Level_10_0)
                {
                    throw new NotSupportedException($"ShaderResourceView for DepthStencil Textures are not supported for Graphics profile < 10.0 (Current: [{GraphicsDevice.Features.CurrentProfile}])");
                }
                else
                {
                    // Determine TypeLess Format and ShaderResourceView Format
                    if (GraphicsDevice.Features.CurrentProfile < GraphicsProfile.Level_10_0)
                    {
                        switch (textureDescription.Format)
                        {
                            case PixelFormat.D16_UNorm:
                                format = Silk.NET.DXGI.Format.FormatD16Unorm;
                                break;
                            case PixelFormat.D32_Float:
                                format = Silk.NET.DXGI.Format.FormatD32Float;
                                break;
                            case PixelFormat.D24_UNorm_S8_UInt:
                                format = Silk.NET.DXGI.Format.FormatD24UnormS8Uint;
                                break;
                            case PixelFormat.D32_Float_S8X24_UInt:
                                format = Silk.NET.DXGI.Format.FormatD32FloatS8X24Uint;
                                break;
                            default:
                                throw new NotSupportedException($"Unsupported DepthFormat [{textureDescription.Format}] for depth buffer");
                        }
                    }
                    else
                    {
                        switch (textureDescription.Format)
                        {
                            case PixelFormat.D16_UNorm:
                                format = Silk.NET.DXGI.Format.FormatR16Typeless;
                                break;
                            case PixelFormat.D32_Float:
                                format = Silk.NET.DXGI.Format.FormatR32Typeless;
                                break;
                            case PixelFormat.D24_UNorm_S8_UInt:
                                //format = SharpDX.DXGI.Format.D24_UNorm_S8_UInt;
                                format = Silk.NET.DXGI.Format.FormatR24G8Typeless;
                                break;
                            case PixelFormat.D32_Float_S8X24_UInt:
                                format = Silk.NET.DXGI.Format.FormatR32G8X24Typeless;
                                break;
                            default:
                                throw new NotSupportedException($"Unsupported DepthFormat [{textureDescription.Format}] for depth buffer");
                        }
                    }
                }
            }

            int quality = 0;
            if (GraphicsDevice.Features.CurrentProfile >= GraphicsProfile.Level_10_1 && textureDescription.IsMultisample)
                quality = (int)StandardMultisampleQualityLevels.StandardMultisamplePattern;

            var desc = new Texture2DDesc()
            {
                Width = (uint)textureDescription.Width,
                Height = (uint)textureDescription.Height,
                ArraySize = (uint)textureDescription.ArraySize,
                SampleDesc = new SampleDesc((uint)textureDescription.MultisampleCount, (uint)quality),
                BindFlags = (uint)GetBindFlagsFromTextureFlags(flags),
                Format = format,
                MipLevels = (uint)textureDescription.MipLevels,
                Usage = (Usage)textureDescription.Usage,
                CPUAccessFlags = (uint)GetCpuAccessFlagsFromUsage(textureDescription.Usage),
                MiscFlags = (uint)textureDescription.Options,
            };

            if (textureDescription.Dimension == TextureDimension.TextureCube)
                desc.MiscFlags = (uint)ResourceMiscFlag.ResourceMiscTexturecube;

            return desc;
        }

        internal static PixelFormat ComputeShaderResourceFormatFromDepthFormat(PixelFormat format)
        {
            PixelFormat viewFormat;

            // Determine TypeLess Format and ShaderResourceView Format
            switch (format)
            {
                case PixelFormat.R16_Typeless:
                case PixelFormat.D16_UNorm:
                    viewFormat = PixelFormat.R16_Float;
                    break;
                case PixelFormat.R32_Typeless:
                case PixelFormat.D32_Float:
                    viewFormat = PixelFormat.R32_Float;
                    break;
                case PixelFormat.R24G8_Typeless:
                case PixelFormat.D24_UNorm_S8_UInt:
                    viewFormat = PixelFormat.R24_UNorm_X8_Typeless;
                    break;
                case PixelFormat.R32_Float_X8X24_Typeless:
                case PixelFormat.D32_Float_S8X24_UInt:
                    viewFormat = PixelFormat.R32_Float_X8X24_Typeless;
                    break;
                default:
                    viewFormat = PixelFormat.None;
                    break;
            }

            return viewFormat;
        }

        internal static Format ComputeDepthViewFormatFromTextureFormat(PixelFormat format)
        {
            Format viewFormat;

            switch (format)
            {
                case PixelFormat.R16_Typeless:
                case PixelFormat.D16_UNorm:
                    viewFormat = Silk.NET.DXGI.Format.FormatD16Unorm;
                    break;
                case PixelFormat.R32_Typeless:
                case PixelFormat.D32_Float:
                    viewFormat = Silk.NET.DXGI.Format.FormatD32Float;
                    break;
                case PixelFormat.R24G8_Typeless:
                case PixelFormat.D24_UNorm_S8_UInt:
                    viewFormat = Silk.NET.DXGI.Format.FormatD24UnormS8Uint;
                    break;
                case PixelFormat.R32G8X24_Typeless:
                case PixelFormat.D32_Float_S8X24_UInt:
                    viewFormat = Silk.NET.DXGI.Format.FormatD32FloatS8X24Uint;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported depth format [{format}]");
            }

            return viewFormat;
        }

        private Texture3DDesc ConvertToNativeDescription3D()
        {
            var desc = new Texture3DDesc()
            {
                Width = (uint)textureDescription.Width,
                Height = (uint)textureDescription.Height,
                Depth = (uint)textureDescription.Depth,
                BindFlags = (uint)GetBindFlagsFromTextureFlags(textureDescription.Flags),
                Format = (Format)textureDescription.Format,
                MipLevels = (uint)textureDescription.MipLevels,
                Usage = (Usage)textureDescription.Usage,
                CPUAccessFlags = (uint)GetCpuAccessFlagsFromUsage(textureDescription.Usage),
                MiscFlags = (uint)textureDescription.Options,
            };
            return desc;
        }

        /// <summary>
        /// Check and modify if necessary the mipmap levels of the image (Troubles with DXT images whose resolution in less than 4x4 in DX9.x).
        /// </summary>
        /// <param name="device">The graphics device.</param>
        /// <param name="description">The texture description.</param>
        /// <returns>The updated texture description.</returns>
        private static TextureDescription CheckMipLevels(GraphicsDevice device, ref TextureDescription description)
        {
            if (device.Features.CurrentProfile < GraphicsProfile.Level_10_0 && (description.Flags & TextureFlags.DepthStencil) == 0 && description.Format.IsCompressed())
            {
                description.MipLevels = Math.Min(CalculateMipCount(description.Width, description.Height), description.MipLevels);
            }
            return description;
        }

        /// <summary>
        /// Calculates the mip level from a specified size.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <param name="minimumSizeLastMip">The minimum size of the last mip.</param>
        /// <returns>The mip level.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">Value must be > 0;size</exception>
        private static int CalculateMipCountFromSize(int size, int minimumSizeLastMip = 4)
        {
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException("Value must be > 0", "size");
            }

            if (minimumSizeLastMip <= 0)
            {
                throw new ArgumentOutOfRangeException("Value must be > 0", "minimumSizeLastMip");
            }

            int level = 1;
            while ((size / 2) >= minimumSizeLastMip)
            {
                size = Math.Max(1, size / 2);
                level++;
            }
            return level;
        }

        /// <summary>
        /// Calculates the mip level from a specified width,height,depth.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="minimumSizeLastMip">The minimum size of the last mip.</param>
        /// <returns>The mip level.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">Value must be &gt; 0;size</exception>
        private static int CalculateMipCount(int width, int height, int minimumSizeLastMip = 4)
        {
            return Math.Min(CalculateMipCountFromSize(width, minimumSizeLastMip), CalculateMipCountFromSize(height, minimumSizeLastMip));
        }

        internal static bool IsStencilFormat(PixelFormat format)
        {
            switch (format)
            {
                case PixelFormat.R24G8_Typeless:
                case PixelFormat.D24_UNorm_S8_UInt:
                case PixelFormat.R32G8X24_Typeless:
                case PixelFormat.D32_Float_S8X24_UInt:
                    return true;
            }

            return false;
        }
    }
}
#endif
