// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if STRIDE_GRAPHICS_API_DIRECT3D11
using System;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace Stride.Graphics
{
    public partial class QueryPool
    {
        internal unsafe ID3D11Query*[] NativeQueries;

        public bool TryGetData(long[] dataArray)
        {
            for (var index = 0; index < NativeQueries.Length; index++)
            {
                unsafe
                {
                    HResult result = GraphicsDevice.NativeDeviceContext->GetData((ID3D11Asynchronous*)NativeQueries[index], ref dataArray[index], sizeof(long), 0);
                    if (result.IsError) return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        protected internal unsafe override void OnDestroyed()
        {
            for (var i = 0; i < QueryCount; i++)
            {
                NativeQueries[i]->Release();
            }
            NativeQueries = null;

            base.OnDestroyed();
        }

        private unsafe void Recreate()
        {
            var queryDescription = new QueryDesc();

            switch (QueryType)
            {
                case QueryType.Timestamp:
                    queryDescription.Query = Query.QueryTimestamp;
                    break;

                default:
                    throw new NotImplementedException();
            }

            NativeQueries = new ID3D11Query*[QueryCount];
            for (var i = 0; i < QueryCount; i++)
            {
                SilkMarshal.ThrowHResult(NativeDevice->CreateQuery(ref queryDescription, ref NativeQueries[i]));
            }
        }
    }
}

#endif
