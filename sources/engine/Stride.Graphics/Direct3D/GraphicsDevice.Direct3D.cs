// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

#if STRIDE_GRAPHICS_API_DIRECT3D11
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;


using Silk.NET.Core.Native;
using Silk.NET.DXGI;
using Silk.NET.Direct3D11;
using Stride.Core;
using System.Runtime.CompilerServices;

namespace Stride.Graphics
{
    public partial class GraphicsDevice
    {
        internal readonly int ConstantBufferDataPlacementAlignment = 16;

        private const GraphicsPlatform GraphicPlatform = GraphicsPlatform.Direct3D11;

        private bool simulateReset = false;
        private string rendererName;

        private unsafe ID3D11Device* nativeDevice;
        private unsafe ID3D11DeviceContext* nativeDeviceContext;
        private readonly Queue<ComPtr<ID3D11Query>> disjointQueries = new(4);
        private readonly Stack<ComPtr<ID3D11Query>> currentDisjointQueries = new(2);

        internal GraphicsProfile RequestedProfile;

        private CreateDeviceFlag creationFlags;

        /// <summary>
        /// The tick frquency of timestamp queries in Hertz.
        /// </summary>
        public long TimestampFrequency { get; private set; }

        /// <summary>
        ///     Gets the status of this device.
        /// </summary>
        /// <value>The graphics device status.</value>
        public GraphicsDeviceStatus GraphicsDeviceStatus
        {
            get
            {
                if (simulateReset)
                {
                    simulateReset = false;
                    return GraphicsDeviceStatus.Reset;
                }

                DXGIError result;
                unsafe
                {
                    result = (DXGIError)NativeDevice->GetDeviceRemovedReason();
                }
                if (result == DXGIError.DeviceRemoved)
                {
                    return GraphicsDeviceStatus.Removed;
                }

                if (result == DXGIError.DeviceReset)
                {
                    return GraphicsDeviceStatus.Reset;
                }

                if (result == DXGIError.DeviceHung)
                {
                    return GraphicsDeviceStatus.Hung;
                }

                if (result == DXGIError.DriverInternalError)
                {
                    return GraphicsDeviceStatus.InternalError;
                }

                if (result == DXGIError.InvalidCall)
                {
                    return GraphicsDeviceStatus.InvalidCall;
                }

                if (result < 0)
                {
                    return GraphicsDeviceStatus.Reset;
                }

                return GraphicsDeviceStatus.Normal;
            }
        }

        /// <summary>
        ///     Gets the native device.
        /// </summary>
        /// <value>The native device.</value>
        internal unsafe ID3D11Device* NativeDevice
        {
            get
            {
                return nativeDevice;
            }
        }

        /// <summary>
        /// Gets the native device context.
        /// </summary>
        /// <value>The native device context.</value>
        internal unsafe ID3D11DeviceContext* NativeDeviceContext
        {
            get
            {
                return nativeDeviceContext;
            }
        }

        /// <summary>
        ///     Marks context as active on the current thread.
        /// </summary>
        public void Begin()
        {
            FrameTriangleCount = 0;
            FrameDrawCalls = 0;

            using ComPtr<QueryDataTimestampDisjoint> result = null;

            ComPtr<ID3D11Query> currentDisjointQuery;
            unsafe
            {
                // Try to read back the oldest disjoint query and reuse it. If not ready, create a new one.
                if (disjointQueries.Count > 0 &&
                    ((HResult)NativeDeviceContext->GetData((ID3D11Asynchronous*)disjointQueries.Peek().Handle,
                                                           result.GetAddressOf(),
                                                           (uint)sizeof(QueryDataTimestampDisjoint),
                                                           0)
                    ).IsSuccess)
                {
                    TimestampFrequency = (long)result.Get().Frequency;
                    currentDisjointQuery = disjointQueries.Dequeue();
                }
                else
                {
                    var disjointQueryDiscription = new QueryDesc { Query = Query.QueryTimestampDisjoint };

                    ID3D11Query* query;
                    SilkMarshal.ThrowHResult(
                        NativeDevice->CreateQuery
                        (
                            &disjointQueryDiscription,
                            &query
                        )
                    );
                    currentDisjointQuery = new ComPtr<ID3D11Query>(query);
                }

                currentDisjointQueries.Push(currentDisjointQuery);
                NativeDeviceContext->Begin((ID3D11Asynchronous*)currentDisjointQuery.Handle);
            }
        }

        /// <summary>
        /// Enables profiling.
        /// </summary>
        /// <param name="enabledFlag">if set to <c>true</c> [enabled flag].</param>
        public void EnableProfile(bool enabledFlag)
        {
        }

        /// <summary>
        ///     Unmarks context as active on the current thread.
        /// </summary>
        public void End()
        {
            // If this fails, it means Begin()/End() don't match, something is very wrong
            var currentDisjointQuery = currentDisjointQueries.Pop();
            unsafe
            {
                NativeDeviceContext->End((ID3D11Asynchronous*)currentDisjointQuery.Handle);
            }
            disjointQueries.Enqueue(currentDisjointQuery);
        }

        /// <summary>
        /// Executes a deferred command list.
        /// </summary>
        /// <param name="commandList">The deferred command list.</param>
        public void ExecuteCommandList(CompiledCommandList commandList)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Executes multiple deferred command lists.
        /// </summary>
        /// <param name="commandLists">The deferred command lists.</param>
        public void ExecuteCommandLists(int count, CompiledCommandList[] commandLists)
        {
            throw new NotImplementedException();
        }

        public void SimulateReset()
        {
            simulateReset = true;
        }

        private void InitializePostFeatures()
        {
            // Create the main command list
            InternalMainCommandList = new CommandList(this);
        }

        private string GetRendererName()
        {
            return rendererName;
        }

        /// <summary>
        ///     Initializes the specified device.
        /// </summary>
        /// <param name="graphicsProfiles">The graphics profiles.</param>
        /// <param name="deviceCreationFlags">The device creation flags.</param>
        /// <param name="windowHandle">The window handle.</param>
        private unsafe void InitializePlatformDevice(GraphicsProfile[] graphicsProfiles, DeviceCreationFlags deviceCreationFlags, object windowHandle)
        {
            if (nativeDevice != null)
            {
                // Destroy previous device
                ReleaseDevice();
            }

            AdapterDesc desc = default;
            SilkMarshal.ThrowHResult(Adapter.NativeAdapter->GetDesc(ref desc));
            rendererName = new(desc.Description);

            // Profiling is supported through pix markers
            IsProfilingSupported = true;

            // Map GraphicsProfile to D3D11 FeatureLevel
            creationFlags = (CreateDeviceFlag)deviceCreationFlags;

            // Create Device D3D11 with feature Level based on profile
            for (int index = 0; index < graphicsProfiles.Length; index++)
            {
                var graphicsProfile = graphicsProfiles[index];
                try
                {
                    // D3D12 supports only feature level 11+
                    var level = graphicsProfile.ToFeatureLevel();

                    // INTEL workaround: it seems Intel driver doesn't support properly feature level 9.x. Fallback to 10.
                    if (Adapter.VendorId == 0x8086)
                    {
                        if (level < D3DFeatureLevel.D3DFeatureLevel100)
                            level = D3DFeatureLevel.D3DFeatureLevel100;
                    }

                    if (Core.Platform.Type == PlatformType.Windows
                        && GetModuleHandle("renderdoc.dll") != IntPtr.Zero)
                    {
                        if (level < D3DFeatureLevel.D3DFeatureLevel110)
                            level = D3DFeatureLevel.D3DFeatureLevel110;
                    }

                    fixed (D3DFeatureLevel* levels = new[] { level })
                    fixed (ID3D11Device** temp = &nativeDevice)
                    {
                        var api = D3D11.GetApi();
                        SilkMarshal.ThrowHResult
                        (
                            api.CreateDevice
                            (
                                (IDXGIAdapter*)Adapter.NativeAdapter,
                                D3DDriverType.D3DDriverTypeUnknown,
                                0,
                                (uint)creationFlags,
                                levels,
                                1,
                                D3D11.SdkVersion,
                                temp,
                                null,
                                null
                            )
                        );
                    }

                    // INTEL workaround: force ShaderProfile to be 10+ as well
                    if (Adapter.VendorId == 0x8086)
                    {
                        if (graphicsProfile < GraphicsProfile.Level_10_0 && (!ShaderProfile.HasValue || ShaderProfile.Value < GraphicsProfile.Level_10_0))
                            ShaderProfile = GraphicsProfile.Level_10_0;
                    }

                    RequestedProfile = graphicsProfile;
                    break;
                }
                catch (Exception)
                {
                    if (index == graphicsProfiles.Length - 1)
                        throw;
                }
            }

            nativeDevice->GetImmediateContext(ref nativeDeviceContext);
            // We keep one reference so that it doesn't disappear with InternalMainCommandList
            ((IUnknown*)nativeDeviceContext)->AddRef();
            if (IsDebugMode)
            {
                GraphicsResourceBase.SetDebugName(this, (ID3D11DeviceChild*)nativeDeviceContext, "ImmediateContext");
            }
        }

        private void AdjustDefaultPipelineStateDescription(ref PipelineStateDescription pipelineStateDescription)
        {
            // On D3D, default state is Less instead of our LessEqual
            // Let's update default pipeline state so that it correspond to D3D state after a "ClearState()"
            pipelineStateDescription.DepthStencilState.DepthBufferFunction = CompareFunction.Less;
        }

        protected void DestroyPlatformDevice()
        {
            ReleaseDevice();
        }

        private unsafe void ReleaseDevice()
        {
            foreach (var query in disjointQueries)
            {
                query.Dispose();
            }
            disjointQueries.Clear();

            // Display D3D11 ref counting info
            ID3D11DeviceContext* immediate = null;
            NativeDevice->GetImmediateContext(ref immediate);
            immediate->ClearState();
            immediate->Flush();

            if (IsDebugMode)
            {
                ID3D11Debug* debugDevice = null;
                SilkMarshal.ThrowHResult(NativeDevice->QueryInterface(ref SilkMarshal.GuidOf<ID3D11Debug>(), (void**)debugDevice));
                if (debugDevice != null)
                {
                    SilkMarshal.ThrowHResult(debugDevice->ReportLiveDeviceObjects(RldoFlags.RldoDetail));
                    debugDevice->Release();
                }
            }

            nativeDevice->Release();
            nativeDevice = null;
        }

        internal void OnDestroyed()
        {
        }

        internal void TagResource(GraphicsResourceLink resourceLink)
        {
            if (resourceLink.Resource is GraphicsResource resource)
                resource.DiscardNextMap = true;
        }

        [DllImport("kernel32.dll", EntryPoint = "GetModuleHandle", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
#endif
