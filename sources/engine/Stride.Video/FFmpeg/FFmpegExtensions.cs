#if STRIDE_GRAPHICS_API_DIRECT3D11 && STRIDE_VIDEO_FFMPEG
using System;
using FFmpeg.AutoGen;
using Silk.NET.Direct3D11;

namespace Stride.Video.FFmpeg
{
    public static class FFmpegExtensions
    {
        public static _GUID ToGUID(this Guid guid)
        {
            var bytes = guid.ToByteArray();

            var data4 = new byte_array8();
            for (int i = 0; i < 8; ++i)
                data4[(uint)i] = bytes[8 + i];

            return new _GUID
            {
                Data1 = (((ulong)bytes[0]) << 24) + (((ulong)bytes[1]) << 16) + (((ulong)bytes[2]) << 8) + bytes[3],
                Data2 = (ushort)((bytes[4] << 8) + bytes[5]),
                Data3 = (ushort)((bytes[6] << 8) + bytes[7]),
                Data4 = data4,
            };
        }

        public static D3D11_VIDEO_DECODER_CONFIG ToFFmpegDecoderConfig(this VideoDecoderConfig configuration)
        {
            return new D3D11_VIDEO_DECODER_CONFIG
            {
                guidConfigBitstreamEncryption = configuration.GuidConfigBitstreamEncryption.ToGUID(),
                Config4GroupedCoefs = configuration.Config4GroupedCoefs,
                ConfigSpecificIDCT = configuration.ConfigSpecificIDCT,
                ConfigHostInverseScan = configuration.ConfigHostInverseScan,
                ConfigResidDiffAccelerator = configuration.ConfigResidDiffAccelerator,
                ConfigIntraResidUnsigned = configuration.ConfigIntraResidUnsigned,
                ConfigSpatialResidInterleaved = configuration.ConfigSpatialResidInterleaved,
                ConfigMinRenderTargetBuffCount = configuration.ConfigMinRenderTargetBuffCount,
                ConfigSpatialHost8or9Clipping = configuration.ConfigSpatialHost8or9Clipping,
                ConfigSpatialResid8 = configuration.ConfigSpatialResid8,
                ConfigResidDiffHost = configuration.ConfigResidDiffHost,
                ConfigMBcontrolRasterOrder = configuration.ConfigMBcontrolRasterOrder,
                ConfigBitstreamRaw = configuration.ConfigBitstreamRaw,
                guidConfigResidDiffEncryption = configuration.GuidConfigResidDiffEncryption.ToGUID(),
                guidConfigMBcontrolEncryption = configuration.GuidConfigMBcontrolEncryption.ToGUID(),
                ConfigResid8Subtraction = configuration.ConfigResid8Subtraction,
                ConfigDecoderSpecific = configuration.ConfigDecoderSpecific
            };
        }
    }
}
#endif
