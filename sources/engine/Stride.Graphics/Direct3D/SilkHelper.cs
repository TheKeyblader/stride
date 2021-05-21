#if STRIDE_GRAPHICS_API_DIRECT3D11

using Silk.NET.DXGI;
using Silk.NET.Direct3D11;
using Silk.NET.Core.Native;
using System;

namespace Stride.Graphics
{
    public enum DXGIError : long
    {
        AccessDenied = 0x887A002B,
        AccessLost = 0x887A0026,
        AlreadyExists = 0x887A0036L,
        CannotProtectContent = 0x887A002A,
        DeviceHung = 0x887A0006,
        DeviceRemoved = 0x887A0005,
        DeviceReset = 0x887A0007,
        DriverInternalError = 0x887A0020,
        FrameStatisticsDisjoint = 0x887A000B,
        GraphicsVidpnSourceInUse = 0x887A000C,
        InvalidCall = 0x887A0001,
        MoreData = 0x887A0003,
        NameAlreadyExists = 0x887A002C,
        NonExclusive = 0x887A0021,
        NotCurrentlyAvailable = 0x887A0022,
        NotFound = 0x887A0002,
        RemoteClientDisconnected = 0x887A0023,
        RemoteOutOfMemory = 0x887A0024,
        RestrictToOutputStale = 0x887A0029,
        SdkComponentMissing = 0x887A002D,
        SessionDisconnected = 0x887A0028,
        Unsupported = 0x887A0004,
        WaitTimeout = 0x887A0027,
        WasStillDrawing = 0x887A000A
    }

    public static class SilkHelper
    {
        public static readonly Guid WKPDID_D3DDebugObjectName = new(1117490210, 37256, 19212, 135, 66, 172, 176, 191, 133, 194, 0);

        public static unsafe bool IsSupportedFeatureLevel(IDXGIAdapter* adapter, D3DFeatureLevel featureLevel)
        {
            var api = D3D11.GetApi();

            Span<D3DFeatureLevel> outputLevel = new();
            using ComPtr<ID3D11Device> device = null;
            using ComPtr<ID3D11DeviceContext> context = null;
            HResult result = api.CreateDevice
            (
                adapter,
                D3DDriverType.D3DDriverTypeUnknown,
                0,
                0,
                new Span<D3DFeatureLevel>(new[] { featureLevel }),
                1,
                D3D11.SdkVersion,
                &device.Handle,
                outputLevel,
                &context.Handle
            );
            return result.IsSuccess && outputLevel[0] == featureLevel;

        }

        public static unsafe void SetDebugName(this ID3D11DeviceChild device, string name)
        {
            fixed (char* str = name)
            {
                var guid = WKPDID_D3DDebugObjectName;
                device.SetPrivateData(ref guid, (uint)(sizeof(char) * name.Length), str);
            }
        }

        public static unsafe void SetDebugName(this IDXGIObject device, string name)
        {
            fixed (char* str = name)
            {
                var guid = WKPDID_D3DDebugObjectName;
                device.SetPrivateData(ref guid, (uint)(sizeof(char) * name.Length), str);
            }
        }
    }
}
#endif
