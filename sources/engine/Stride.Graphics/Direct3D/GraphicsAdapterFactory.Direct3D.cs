// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if STRIDE_GRAPHICS_API_DIRECT3D
using System.Collections.Generic;
using Silk.NET.Core.Native;
using Silk.NET.DXGI;

namespace Stride.Graphics
{
    public static partial class GraphicsAdapterFactory
    {
#if STRIDE_PLATFORM_UWP || DIRECTX11_1
        internal static Factory2 NativeFactory;
#else
        internal unsafe static IDXGIFactory1* NativeFactory;
#endif

        /// <summary>
        /// Initializes all adapters with the specified factory.
        /// </summary>
        internal unsafe static void InitializeInternal()
        {
            staticCollector.Dispose();
            var api = DXGI.GetApi();

#if DIRECTX11_1
            using (var factory = new Factory1())
            NativeFactory = factory.QueryInterface<Factory2>();
#elif STRIDE_PLATFORM_UWP
            // Maybe this will become default code for everybody if we switch to DX 11.1/11.2 SharpDX dll?
            NativeFactory = new Factory2();
#else
            fixed (IDXGIFactory1** temp = &NativeFactory)
                SilkMarshal.ThrowHResult(api.CreateDXGIFactory1(ref SilkMarshal.GuidOf<IDXGIFactory1>(), (void**)temp));
#endif

            staticCollector.Add(new ComPtr<IDXGIFactory1>(NativeFactory));

            var adapterList = new List<GraphicsAdapter>();
            uint i = 0;
            IDXGIAdapter1* pAdapter = null;
            while (NativeFactory->EnumAdapters1(i, &pAdapter) != unchecked((int)DXGIError.NotFound))
            {
                var adapter = new GraphicsAdapter(NativeFactory, (int)i);
                staticCollector.Add(adapter);
                adapterList.Add(adapter);
            }

            defaultAdapter = adapterList.Count > 0 ? adapterList[0] : null;
            adapters = adapterList.ToArray();
        }

        /// <summary>
        /// Gets the <see cref="Factory1"/> used by all GraphicsAdapter.
        /// </summary>
        internal unsafe static IDXGIFactory1* Factory
        {
            get
            {
                lock (StaticLock)
                {
                    Initialize();
                    return NativeFactory;
                }
            }
        }
    }
}
#endif 
