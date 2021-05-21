// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if STRIDE_GRAPHICS_API_DIRECT3D11
#pragma warning disable SA1405 // Debug.Assert must provide message text
using System;
using System.Diagnostics;

using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace Stride.Graphics
{
    /// <summary>
    /// GraphicsResource class
    /// </summary>
    public abstract partial class GraphicsResourceBase
    {
        private unsafe ID3D11DeviceChild* nativeDeviceChild;

        protected internal unsafe ID3D11Resource* NativeResource { get; private set; }

        private void Initialize()
        {
        }

        /// <summary>
        /// Gets or sets the device child.
        /// </summary>
        /// <value>The device child.</value>
        protected internal unsafe ID3D11Resource* NativeDeviceChild
        {
            get
            {
                return (ID3D11Resource*)nativeDeviceChild;
            }
            set
            {
                nativeDeviceChild = (ID3D11DeviceChild*)value;
                NativeResource = (ID3D11Resource*)nativeDeviceChild;
                // Associate PrivateData to this DeviceResource
                SetDebugName(GraphicsDevice, nativeDeviceChild, Name);
            }
        }

        /// <summary>
        /// Associates the private data to the device child, useful to get the name in PIX debugger.
        /// </summary>
        internal static unsafe void SetDebugName(GraphicsDevice graphicsDevice, ID3D11DeviceChild* deviceChild, string name)
        {
            if (graphicsDevice.IsDebugMode && deviceChild != null)
            {
                //deviceChild.DebugName = name;
            }
        }

        /// <summary>
        /// Called when graphics device has been detected to be internally destroyed.
        /// </summary>
        protected internal unsafe virtual void OnDestroyed()
        {
            Destroyed?.Invoke(this, EventArgs.Empty);

            ReleaseComObject((IUnknown*)nativeDeviceChild);
            NativeResource = null;
        }

        /// <summary>
        /// Called when graphics device has been recreated.
        /// </summary>
        /// <returns>True if item transitioned to a <see cref="GraphicsResourceLifetimeState.Active"/> state.</returns>
        protected internal virtual bool OnRecreate()
        {
            return false;
        }

        protected unsafe ID3D11Device* NativeDevice
        {
            get
            {
                return GraphicsDevice != null ? GraphicsDevice.NativeDevice : null;
            }
        }

        /// <summary>
        /// Gets the cpu access flags from resource usage.
        /// </summary>
        /// <param name="usage">The usage.</param>
        /// <returns></returns>
        internal static CpuAccessFlag GetCpuAccessFlagsFromUsage(GraphicsResourceUsage usage)
        {
            switch (usage)
            {
                case GraphicsResourceUsage.Dynamic:
                    return CpuAccessFlag.CpuAccessWrite;
                case GraphicsResourceUsage.Staging:
                    return CpuAccessFlag.CpuAccessRead | CpuAccessFlag.CpuAccessWrite;
            }
            return 0;
        }

        internal static unsafe void ReleaseComObject(IUnknown* @object)
        {
            // We can't put IUnknown as a constraint on the generic as it would break compilation (trying to import SharpDX in projects with InternalVisibleTo)
            if (@object != null)
            {
                var refCountResult = @object->Release();
                Debug.Assert(refCountResult >= 0);
                @object = null;
            }
        }
    }
}

#endif
