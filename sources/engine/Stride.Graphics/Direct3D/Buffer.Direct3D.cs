// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if STRIDE_GRAPHICS_API_DIRECT3D11
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace Stride.Graphics
{
    public partial class Buffer
    {
        private BufferDesc nativeDescription;

        internal unsafe ID3D11Buffer* NativeBuffer
        {
            get
            {
                return (ID3D11Buffer*)NativeDeviceChild;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer" /> class.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <param name="viewFlags">Type of the buffer.</param>
        /// <param name="viewFormat">The view format.</param>
        /// <param name="dataPointer">The data pointer.</param>
        protected unsafe Buffer InitializeFromImpl(BufferDescription description, BufferFlags viewFlags, PixelFormat viewFormat, IntPtr dataPointer)
        {
            bufferDescription = description;
            nativeDescription = ConvertToNativeDescription(Description);
            ViewFlags = viewFlags;
            InitCountAndViewFormat(out this.elementCount, ref viewFormat);
            ViewFormat = viewFormat;

            SilkMarshal.ThrowHResult
            (
                GraphicsDevice.NativeDevice->CreateBuffer
                (
                ref nativeDescription,
                (SubresourceData*)dataPointer.ToPointer(),
                (ID3D11Buffer**)NativeBuffer->LpVtbl
                )
            );

            // Staging resource don't have any views
            if (nativeDescription.Usage != Silk.NET.Direct3D11.Usage.UsageStaging)
                this.InitializeViews();

            if (GraphicsDevice != null)
            {
                GraphicsDevice.RegisterBufferMemoryUsage(SizeInBytes);
            }

            return this;
        }

        /// <inheritdoc/>
        protected internal override void OnDestroyed()
        {
            if (GraphicsDevice != null)
            {
                GraphicsDevice.RegisterBufferMemoryUsage(-SizeInBytes);
            }

            base.OnDestroyed();
        }

        /// <inheritdoc/>
        protected internal unsafe override bool OnRecreate()
        {
            base.OnRecreate();

            if (Description.Usage == GraphicsResourceUsage.Immutable
                || Description.Usage == GraphicsResourceUsage.Default)
                return false;

            SilkMarshal.ThrowHResult
            (
                GraphicsDevice.NativeDevice->CreateBuffer
                (
                    ref nativeDescription,
                    null,
                    (ID3D11Buffer**)NativeBuffer->LpVtbl
                )
            );

            // Staging resource don't have any views
            if (nativeDescription.Usage != Silk.NET.Direct3D11.Usage.UsageStaging)
                this.InitializeViews();

            return true;
        }

        /// <summary>
        /// Explicitly recreate buffer with given data. Usually called after a <see cref="GraphicsDevice"/> reset.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dataPointer"></param>
        public void Recreate(IntPtr dataPointer)
        {
            unsafe
            {
                SilkMarshal.ThrowHResult
                (
                    GraphicsDevice.NativeDevice->CreateBuffer
                    (
                        ref nativeDescription,
                        (SubresourceData*)dataPointer.ToPointer(),
                        (ID3D11Buffer**)NativeBuffer->LpVtbl
                    )
                );
            }

            // Staging resource don't have any views
            if (nativeDescription.Usage != Silk.NET.Direct3D11.Usage.UsageStaging)
                this.InitializeViews();
        }

        /// <summary>
        /// Gets a <see cref="ShaderResourceView"/> for a particular <see cref="PixelFormat"/>.
        /// </summary>
        /// <param name="viewFormat">The view format.</param>
        /// <returns>A <see cref="ShaderResourceView"/> for the particular view format.</returns>
        /// <remarks>
        /// The buffer must have been declared with <see cref="Graphics.BufferFlags.ShaderResource"/>. 
        /// The ShaderResourceView instance is kept by this buffer and will be disposed when this buffer is disposed.
        /// </remarks>
        internal unsafe ID3D11ShaderResourceView* GetShaderResourceView(PixelFormat viewFormat)
        {
            ID3D11ShaderResourceView* srv = null;
            if ((nativeDescription.BindFlags & (uint)BindFlag.BindShaderResource) != 0)
            {
                var description = new ShaderResourceViewDesc
                {
                    Format = (Format)viewFormat,
                    ViewDimension = D3DSrvDimension.D3D11SrvDimensionBufferex,
                    BufferEx = new BufferexSrv
                    {
                        NumElements = (uint)ElementCount,
                        FirstElement = 0,
                        Flags = 0,
                    },
                };

                if (((ViewFlags & BufferFlags.RawBuffer) == BufferFlags.RawBuffer))
                    description.BufferEx = new BufferexSrv
                    {
                        NumElements = (uint)ElementCount,
                        FirstElement = 0,
                        Flags = (uint)BufferexSrvFlag.BufferexSrvFlagRaw
                    };

                SilkMarshal.ThrowHResult
                (
                    GraphicsDevice.NativeDevice->CreateShaderResourceView
                    (
                        NativeResource,
                        ref description,
                        ref srv
                    )
                );
            }
            return srv;
        }

        /// <summary>
        /// Gets a <see cref="RenderTargetView" /> for a particular <see cref="PixelFormat" />.
        /// </summary>
        /// <param name="pixelFormat">The view format.</param>
        /// <param name="width">The width in pixels of the render target.</param>
        /// <returns>A <see cref="RenderTargetView" /> for the particular view format.</returns>
        /// <remarks>The buffer must have been declared with <see cref="Graphics.BufferFlags.RenderTarget" />.
        /// The RenderTargetView instance is kept by this buffer and will be disposed when this buffer is disposed.</remarks>
        internal unsafe ID3D11RenderTargetView* GetRenderTargetView(PixelFormat pixelFormat, int width)
        {
            ID3D11RenderTargetView* srv = null;
            if ((nativeDescription.BindFlags & (uint)BindFlag.BindRenderTarget) != 0)
            {
                var description = new RenderTargetViewDesc()
                {
                    Format = (Format)pixelFormat,
                    ViewDimension = RtvDimension.RtvDimensionBuffer,
                    Buffer = new BufferRtv
                    {
                        ElementWidth = (uint)(pixelFormat.SizeInBytes() * width),
                        ElementOffset = 0,
                    },
                };

                SilkMarshal.ThrowHResult
                (
                    GraphicsDevice.NativeDevice->CreateRenderTargetView
                    (
                        NativeResource,
                        ref description,
                        ref srv
                    )
                );
            }
            return srv;
        }

        protected override unsafe void OnNameChanged()
        {
            base.OnNameChanged();
            if (GraphicsDevice != null && GraphicsDevice.IsDebugMode)
            {
                if (NativeShaderResourceView != null)
                    ((ID3D11DeviceChild*)NativeShaderResourceView)->SetDebugName(Name == null ? null : string.Format("{0} SRV", Name));

                if (NativeUnorderedAccessView != null)
                    ((ID3D11DeviceChild*)NativeUnorderedAccessView)->SetDebugName(Name == null ? null : string.Format("{0} UAV", Name));
            }
        }

        private void InitCountAndViewFormat(out int count, ref PixelFormat viewFormat)
        {
            if (Description.StructureByteStride == 0)
            {
                // TODO: The way to calculate the count is not always correct depending on the ViewFlags...etc.
                if ((ViewFlags & BufferFlags.RawBuffer) != 0)
                {
                    count = Description.SizeInBytes / sizeof(int);
                }
                else if ((ViewFlags & BufferFlags.ShaderResource) != 0)
                {
                    count = Description.SizeInBytes / viewFormat.SizeInBytes();
                }
                else
                {
                    count = 0;
                }
            }
            else
            {
                // For structured buffer
                count = Description.SizeInBytes / Description.StructureByteStride;
                viewFormat = PixelFormat.None;
            }
        }

        private static BufferDesc ConvertToNativeDescription(BufferDescription bufferDescription)
        {
            var bufferFlags = bufferDescription.BufferFlags;
            BindFlag newBindFlags = 0;
            ResourceMiscFlag newMiscFlags = 0;

            if ((bufferFlags & BufferFlags.ConstantBuffer) != 0)
                newBindFlags |= BindFlag.BindConstantBuffer;

            if ((bufferFlags & BufferFlags.IndexBuffer) != 0)
                newBindFlags |= BindFlag.BindIndexBuffer;

            if ((bufferFlags & BufferFlags.VertexBuffer) != 0)
                newBindFlags |= BindFlag.BindVertexBuffer;

            if ((bufferFlags & BufferFlags.RenderTarget) != 0)
                newBindFlags |= BindFlag.BindRenderTarget;

            if ((bufferFlags & BufferFlags.ShaderResource) != 0)
                newBindFlags |= BindFlag.BindShaderResource;

            if ((bufferFlags & BufferFlags.UnorderedAccess) != 0)
                newBindFlags |= BindFlag.BindUnorderedAccess;

            if ((bufferFlags & BufferFlags.StructuredBuffer) != 0)
            {
                newMiscFlags |= ResourceMiscFlag.ResourceMiscBufferStructured;
                if (bufferDescription.StructureByteStride <= 0)
                    throw new ArgumentException("Element size cannot be less or equal 0 for structured buffer");
            }

            if ((bufferFlags & BufferFlags.RawBuffer) == BufferFlags.RawBuffer)
                newMiscFlags |= ResourceMiscFlag.ResourceMiscBufferAllowRawViews;

            if ((bufferFlags & BufferFlags.ArgumentBuffer) == BufferFlags.ArgumentBuffer)
                newMiscFlags |= ResourceMiscFlag.ResourceMiscDrawindirectArgs;

            if ((bufferFlags & BufferFlags.StreamOutput) != 0)
                newBindFlags |= BindFlag.BindStreamOutput;

            return new BufferDesc()
            {
                ByteWidth = (uint)bufferDescription.SizeInBytes,
                StructureByteStride = (uint)bufferDescription.StructureByteStride,
                CPUAccessFlags = (uint)GetCpuAccessFlagsFromUsage(bufferDescription.Usage),
                BindFlags = (uint)newBindFlags,
                MiscFlags = (uint)newMiscFlags,
                Usage = (Usage)bufferDescription.Usage,
            };
        }

        /// <summary>
        /// Initializes the views.
        /// </summary>
        private unsafe void InitializeViews()
        {
            var bindFlags = (BindFlag)nativeDescription.BindFlags;

            var srvFormat = ViewFormat;
            var uavFormat = ViewFormat;

            if (((ViewFlags & BufferFlags.RawBuffer) != 0))
            {
                srvFormat = PixelFormat.R32_Typeless;
                uavFormat = PixelFormat.R32_Typeless;
            }

            if ((bindFlags & BindFlag.BindShaderResource) != 0)
            {
                NativeShaderResourceView = GetShaderResourceView(srvFormat);
            }

            if ((bindFlags & BindFlag.BindUnorderedAccess) != 0)
            {
                BufferUavFlag bufferFlags = 0;
                if (((ViewFlags & BufferFlags.RawBuffer) == BufferFlags.RawBuffer))
                    bufferFlags |= BufferUavFlag.BufferUavFlagRaw;

                if (((ViewFlags & BufferFlags.StructuredAppendBuffer) == BufferFlags.StructuredAppendBuffer))
                    bufferFlags |= BufferUavFlag.BufferUavFlagAppend;

                if (((ViewFlags & BufferFlags.StructuredCounterBuffer) == BufferFlags.StructuredCounterBuffer))
                    bufferFlags |= BufferUavFlag.BufferUavFlagCounter;

                var description = new UnorderedAccessViewDesc()
                {
                    Format = (Format)uavFormat,
                    ViewDimension = UavDimension.UavDimensionBuffer,
                    Buffer = new BufferUav
                    {
                        NumElements = (uint)ElementCount,
                        FirstElement = 0,
                        Flags = (uint)bufferFlags,
                    },
                };

                SilkMarshal.ThrowHResult
                (
                    GraphicsDevice.NativeDevice->CreateUnorderedAccessView
                    (
                        NativeResource,
                        ref description,
                        (ID3D11UnorderedAccessView**)NativeUnorderedAccessView->LpVtbl
                    )
                );
            }
        }
    }
}
#endif
