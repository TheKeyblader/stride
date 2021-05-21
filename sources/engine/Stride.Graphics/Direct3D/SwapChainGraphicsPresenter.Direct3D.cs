// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
//
// Copyright (c) 2010-2013 SharpDX - Alexandre Mutel
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

#if STRIDE_GRAPHICS_API_DIRECT3D
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Stride.Core.Collections;
#if STRIDE_GRAPHICS_API_DIRECT3D11
using BackBufferResourceType = Silk.NET.Direct3D11.ID3D11Texture2D;
#elif STRIDE_GRAPHICS_API_DIRECT3D12
using BackBufferResourceType = SharpDX.Direct3D12.Resource;
#endif

namespace Stride.Graphics
{
    /// <summary>
    /// Graphics presenter for SwapChain.
    /// </summary>
    public class SwapChainGraphicsPresenter : GraphicsPresenter
    {
        private unsafe IDXGISwapChain* swapChain;

        private readonly Texture backBuffer;

        private int bufferCount;

#if STRIDE_GRAPHICS_API_DIRECT3D12
        private int bufferSwapIndex;
#endif

        public SwapChainGraphicsPresenter(GraphicsDevice device, PresentationParameters presentationParameters)
            : base(device, presentationParameters)
        {
            PresentInterval = presentationParameters.PresentationInterval;

            unsafe
            {
                // Initialize the swap chain
                swapChain = CreateSwapChain();

                BackBufferResourceType* ptr = null;
                SilkMarshal.ThrowHResult(swapChain->GetBuffer(0, ref SilkMarshal.GuidOf<BackBufferResourceType>(), (void**)&ptr));
                backBuffer = new Texture(device).InitializeFromImpl(ptr, Description.BackBufferFormat.IsSRgb());
            }
            // Reload should get backbuffer from swapchain as well
            //backBufferTexture.Reload = graphicsResource => ((Texture)graphicsResource).Recreate(swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture>(0));
        }

        public override Texture BackBuffer => backBuffer;

        public override object NativePresenter { get { unsafe { return new IntPtr(swapChain); } } }

        public override bool IsFullScreen
        {
            get
            {
#if STRIDE_PLATFORM_UWP
                return false;
#else
                unsafe
                {
                    int result = 0;
                    SilkMarshal.ThrowHResult(swapChain->GetFullscreenState(&result, null));
                    return result != 0;
                }
#endif
            }

            set
            {
#if !STRIDE_PLATFORM_UWP
                unsafe
                {
                    if (swapChain == null)
                        return;

                    var outputIndex = Description.PreferredFullScreenOutputIndex;

                    // no outputs connected to the current graphics adapter
                    var output = GraphicsDevice.Adapter != null && outputIndex < GraphicsDevice.Adapter.Outputs.Length ? GraphicsDevice.Adapter.Outputs[outputIndex] : null;

                    IDXGIOutput* currentOutput = null;

                    try
                    {
                        int _isCurrentlyFullscreen = 0;
                        SilkMarshal.ThrowHResult(swapChain->GetFullscreenState(ref _isCurrentlyFullscreen, ref currentOutput));
                        bool isCurrentlyFullscreen = _isCurrentlyFullscreen != 0;
                        // check if the current fullscreen monitor is the same as new one
                        // If not fullscreen, currentOutput will be null but output won't be, so don't compare them
                        if (isCurrentlyFullscreen == value && (isCurrentlyFullscreen == false || (output != null && currentOutput != null && currentOutput == output.NativeOutput)))
                            return;
                    }
                    finally
                    {
                        if (currentOutput != null)
                            currentOutput->Release();
                    }

                    bool switchToFullScreen = value;
                    // If going to fullscreen mode: call 1) SwapChain.ResizeTarget 2) SwapChain.IsFullScreen
                    var description = new ModeDesc((uint)backBuffer.ViewWidth, (uint)backBuffer.ViewHeight, Description.RefreshRate.ToSilk(), (Format)Description.BackBufferFormat);
                    if (switchToFullScreen)
                    {
                        OnDestroyed();

                        Description.IsFullScreen = true;

                        OnRecreated();
                    }
                    else
                    {
                        Description.IsFullScreen = false;
                        SilkMarshal.ThrowHResult(swapChain->SetFullscreenState(0, null));

                        // call 1) SwapChain.IsFullScreen 2) SwapChain.Resize
                        Resize(backBuffer.ViewWidth, backBuffer.ViewHeight, backBuffer.ViewFormat);
                    }

                    // If going to window mode: 
                    if (!switchToFullScreen)
                    {
                        // call 1) SwapChain.IsFullScreen 2) SwapChain.Resize
                        description.RefreshRate = new Silk.NET.DXGI.Rational(0, 0);
                        SilkMarshal.ThrowHResult(swapChain->ResizeTarget(ref description));
                    }
                }
#endif
            }
        }

        public override void BeginDraw(CommandList commandList)
        {
        }

        public override void EndDraw(CommandList commandList, bool present)
        {
        }

        public override void Present()
        {
            try
            {
                var presentInterval = GraphicsDevice.Tags.Get(ForcedPresentInterval) ?? PresentInterval;
                unsafe
                {
                    SilkMarshal.ThrowHResult(swapChain->Present((uint)presentInterval, 0));
                }
#if STRIDE_GRAPHICS_API_DIRECT3D12
                // Manually swap back buffer
                backBuffer.NativeResource.Dispose();
                backBuffer.InitializeFromImpl(swapChain.GetBackBuffer<BackBufferResourceType>((++bufferSwapIndex) % bufferCount), Description.BackBufferFormat.IsSRgb());
#endif
            }
            catch (Exception sharpDxException)
            {
                var deviceStatus = GraphicsDevice.GraphicsDeviceStatus;
                throw new GraphicsException($"Unexpected error on Present (device status: {deviceStatus})", sharpDxException, deviceStatus);
            }
        }

        protected override unsafe void OnNameChanged()
        {
            base.OnNameChanged();
            if (Name != null && GraphicsDevice != null && GraphicsDevice.IsDebugMode && swapChain != null)
            {
                ((IDXGIObject*)swapChain)->SetDebugName(Name);
            }
        }

        protected internal unsafe override void OnDestroyed()
        {
            // Manually update back buffer texture
            backBuffer.OnDestroyed();
            backBuffer.LifetimeState = GraphicsResourceLifetimeState.Destroyed;

            swapChain->Release();
            swapChain = null;

            base.OnDestroyed();
        }

        public override void OnRecreated()
        {
            base.OnRecreated();

            unsafe
            {
                // Recreate swap chain
                swapChain = CreateSwapChain();

                // Get newly created native texture
                BackBufferResourceType* backBufferTexture = null;
                SilkMarshal.ThrowHResult(swapChain->GetBuffer(0, ref SilkMarshal.GuidOf<BackBufferResourceType>(), (void**)&backBufferTexture));

                // Put it in our back buffer texture
                // TODO: Update new size
                backBuffer.InitializeFromImpl(backBufferTexture, Description.BackBufferFormat.IsSRgb());
                backBuffer.LifetimeState = GraphicsResourceLifetimeState.Active;
            }
        }

        protected override unsafe void ResizeBackBuffer(int width, int height, PixelFormat format)
        {
            // Manually update back buffer texture
            backBuffer.OnDestroyed();

            // Manually update all children textures
            var fastList = DestroyChildrenTextures(backBuffer);

#if STRIDE_PLATFORM_UWP
            var swapChainPanel = Description.DeviceWindowHandle.NativeWindow as Windows.UI.Xaml.Controls.SwapChainPanel;
            if (swapChainPanel != null)
            {
                var swapChain2 = swapChain.QueryInterface<SwapChain2>();
                if (swapChain2 != null)
                {
                    swapChain2.MatrixTransform = new RawMatrix3x2 { M11 = 1f / swapChainPanel.CompositionScaleX, M22 = 1f / swapChainPanel.CompositionScaleY };
                    swapChain2.Dispose();
                }
            }
#endif

            // If format is same as before, using Unknown (None) will keep the current
            // We do that because on Win10/RT, actual format might be the non-srgb one and we don't want to switch to srgb one by mistake (or need #ifdef)
            if (format == backBuffer.Format)
                format = PixelFormat.None;

            SilkMarshal.ThrowHResult(swapChain->ResizeBuffers((uint)bufferCount, (uint)width, (uint)height, (Format)format, 0));

            // Get newly created native texture
            BackBufferResourceType* backBufferTexture = null;
            SilkMarshal.ThrowHResult(swapChain->GetBuffer(0, ref SilkMarshal.GuidOf<BackBufferResourceType>(), (void**)&backBufferTexture));

            // Put it in our back buffer texture
            backBuffer.InitializeFromImpl(backBufferTexture, Description.BackBufferFormat.IsSRgb());

            foreach (var texture in fastList)
            {
                texture.InitializeFrom(backBuffer, texture.ViewDescription);
            }
        }

        protected override void ResizeDepthStencilBuffer(int width, int height, PixelFormat format)
        {
            var newTextureDescription = DepthStencilBuffer.Description;
            newTextureDescription.Width = width;
            newTextureDescription.Height = height;

            // Manually update the texture
            DepthStencilBuffer.OnDestroyed();

            // Manually update all children textures
            var fastList = DestroyChildrenTextures(DepthStencilBuffer);

            // Put it in our back buffer texture
            DepthStencilBuffer.InitializeFrom(newTextureDescription);

            foreach (var texture in fastList)
            {
                texture.InitializeFrom(DepthStencilBuffer, texture.ViewDescription);
            }
        }

        /// <summary>
        /// Calls <see cref="Texture.OnDestroyed"/> for all children of the specified texture
        /// </summary>
        /// <param name="parentTexture">Specified parent texture</param>
        /// <returns>A list of the children textures which were destroyed</returns>
        private FastList<Texture> DestroyChildrenTextures(Texture parentTexture)
        {
            var fastList = new FastList<Texture>();
            foreach (var resource in GraphicsDevice.Resources)
            {
                var texture = resource as Texture;
                if (texture != null && texture.ParentTexture == parentTexture)
                {
                    texture.OnDestroyed();
                    fastList.Add(texture);
                }
            }

            return fastList;
        }

        private unsafe IDXGISwapChain* CreateSwapChain()
        {
            // Check for Window Handle parameter
            if (Description.DeviceWindowHandle == null)
            {
                throw new ArgumentException("DeviceWindowHandle cannot be null");
            }

#if STRIDE_PLATFORM_UWP
            return CreateSwapChainForUWP();
#else
            return CreateSwapChainForWindows();
#endif
        }

#if STRIDE_PLATFORM_UWP
        private SwapChain CreateSwapChainForUWP()
        {
            bufferCount = 2;
            var description = new SwapChainDescription1
            {
                // Automatic sizing
                Width = Description.BackBufferWidth,
                Height = Description.BackBufferHeight,
                Format = (SharpDX.DXGI.Format)Description.BackBufferFormat.ToNonSRgb(),
                Stereo = false,
                SampleDescription = new SharpDX.DXGI.SampleDescription((int)Description.MultisampleCount, 0),
                Usage = Usage.BackBuffer | Usage.RenderTargetOutput,
                // Use two buffers to enable flip effect.
                BufferCount = bufferCount,
                Scaling = SharpDX.DXGI.Scaling.Stretch,
                SwapEffect = SharpDX.DXGI.SwapEffect.FlipSequential,
            };

            SwapChain swapChain = null;
            switch (Description.DeviceWindowHandle.Context)
            {
                case Games.AppContextType.UWPXaml:
                {
                    var nativePanel = ComObject.As<ISwapChainPanelNative>(Description.DeviceWindowHandle.NativeWindow);

                    // Creates the swap chain for XAML composition
                    swapChain = new SwapChain1(GraphicsAdapterFactory.NativeFactory, GraphicsDevice.NativeDevice, ref description);

                    // Associate the SwapChainPanel with the swap chain
                    nativePanel.SwapChain = swapChain;

                    break;
                }

                case Games.AppContextType.UWPCoreWindow:
                {
                    using (var dxgiDevice = GraphicsDevice.NativeDevice.QueryInterface<SharpDX.DXGI.Device2>())
                    {
                        // Ensure that DXGI does not queue more than one frame at a time. This both reduces
                        // latency and ensures that the application will only render after each VSync, minimizing
                        // power consumption.
                        dxgiDevice.MaximumFrameLatency = 1;

                        // Next, get the parent factory from the DXGI Device.
                        using (var dxgiAdapter = dxgiDevice.Adapter)
                        using (var dxgiFactory = dxgiAdapter.GetParent<SharpDX.DXGI.Factory2>())
                            // Finally, create the swap chain.
                        using (var coreWindow = new SharpDX.ComObject(Description.DeviceWindowHandle.NativeWindow))
                        {
                            swapChain = new SharpDX.DXGI.SwapChain1(dxgiFactory
                                , GraphicsDevice.NativeDevice, coreWindow, ref description);
                        }
                    }

                    break;
                }
                default:
                    throw new NotSupportedException(string.Format("Window context [{0}] not supported while creating SwapChain", Description.DeviceWindowHandle.Context));
            }

            return swapChain;
        }
#else
        /// <summary>
        /// Create the SwapChain on Windows.
        /// </summary>
        /// <returns></returns>
        private unsafe IDXGISwapChain* CreateSwapChainForWindows()
        {
            var hwndPtr = Description.DeviceWindowHandle.Handle;
            if (hwndPtr != IntPtr.Zero)
            {
                return CreateSwapChainForDesktop(hwndPtr);
            }
            throw new InvalidOperationException($"The {nameof(WindowHandle)}.{nameof(WindowHandle.Handle)} must not be zero.");
        }

        private unsafe IDXGISwapChain* CreateSwapChainForDesktop(IntPtr handle)
        {
            bufferCount = 1;
            var backbufferFormat = Description.BackBufferFormat;
#if STRIDE_GRAPHICS_API_DIRECT3D12
            // TODO D3D12 (check if this setting make sense on D3D11 too?)
            backbufferFormat = backbufferFormat.ToNonSRgb();
            // TODO D3D12 Can we make it work with something else after?
            bufferCount = 2;
#endif
            var description = new SwapChainDesc
            {
                BufferDesc = new ModeDesc((uint)Description.BackBufferWidth, (uint)Description.BackBufferHeight, Description.RefreshRate.ToSilk(), (Format)backbufferFormat),
                BufferCount = (uint)bufferCount, // TODO: Do we really need this to be configurable by the user?
                OutputWindow = handle,
                SampleDesc = new SampleDesc((uint)Description.MultisampleCount, 0),
#if STRIDE_GRAPHICS_API_DIRECT3D11
                SwapEffect = SwapEffect.SwapEffectDiscard,
#elif STRIDE_GRAPHICS_API_DIRECT3D12
                    SwapEffect = SwapEffect.FlipDiscard,
#endif
                BufferUsage = DXGI.UsageBackBuffer | DXGI.UsageRenderTargetOutput,
                Windowed = 1,
                Flags = Description.IsFullScreen ? (uint)SwapChainFlag.SwapChainFlagAllowModeSwitch : 0,
            };

#if STRIDE_GRAPHICS_API_DIRECT3D11
            IDXGISwapChain* newSwapChain = null;
            SilkMarshal.ThrowHResult(GraphicsAdapterFactory.NativeFactory->CreateSwapChain(
                (IUnknown*)GraphicsDevice.NativeDevice, ref description, ref newSwapChain));
#elif STRIDE_GRAPHICS_API_DIRECT3D12
            var newSwapChain = new SwapChain(GraphicsAdapterFactory.NativeFactory, GraphicsDevice.NativeCommandQueue, description);
#endif

            //prevent normal alt-tab
            SilkMarshal.ThrowHResult(GraphicsAdapterFactory.NativeFactory->MakeWindowAssociation(handle, 0x1));

            if (Description.IsFullScreen)
            {
                // Before fullscreen switch
                SilkMarshal.ThrowHResult(newSwapChain->ResizeTarget(ref description.BufferDesc));

                // Switch to full screen
                SilkMarshal.ThrowHResult(newSwapChain->SetFullscreenState(1, null));

                // This is really important to call ResizeBuffers AFTER switching to IsFullScreen
                SilkMarshal.ThrowHResult(newSwapChain->ResizeBuffers(
                    (uint)bufferCount,
                    (uint)Description.BackBufferWidth,
                    (uint)Description.BackBufferHeight,
                    (Format)Description.BackBufferFormat,
                    (uint)SwapChainFlag.SwapChainFlagAllowModeSwitch));
            }

            return newSwapChain;
        }
#endif
    }
}
#endif
