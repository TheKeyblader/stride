// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if STRIDE_GRAPHICS_API_DIRECT3D11
using System;
using System.Collections.Generic;
using System.Linq;
using Stride.Core;
using Stride.Core.Storage;
using Stride.Shaders;

using Silk.NET.DXGI;
using Silk.NET.Direct3D11;
using Silk.NET.Core.Native;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Stride.Graphics
{
    public partial class PipelineState
    {
        // Effect
        private readonly RootSignature rootSignature;
        private readonly EffectBytecode effectBytecode;
        internal ResourceBinder ResourceBinder;

        private unsafe ID3D11VertexShader* vertexShader;
        private unsafe ID3D11GeometryShader* geometryShader;
        private unsafe ID3D11PixelShader* pixelShader;
        private unsafe ID3D11HullShader* hullShader;
        private unsafe ID3D11DomainShader* domainShader;
        private unsafe ID3D11ComputeShader* computeShader;
        private byte[] inputSignature;

        private readonly unsafe ID3D11BlendState* blendState;
        private readonly uint sampleMask;
        private readonly unsafe ID3D11RasterizerState* rasterizerState;
        private readonly unsafe ID3D11DepthStencilState* depthStencilState;

        private unsafe ID3D11InputLayout* inputLayout;

        private readonly D3DPrimitiveTopology primitiveTopology;
        // Note: no need to store RTV/DSV formats

        internal unsafe PipelineState(GraphicsDevice graphicsDevice, PipelineStateDescription pipelineStateDescription) : base(graphicsDevice)
        {
            // First time, build caches
            var pipelineStateCache = GetPipelineStateCache();

            // Effect
            this.rootSignature = pipelineStateDescription.RootSignature;
            this.effectBytecode = pipelineStateDescription.EffectBytecode;
            CreateShaders(pipelineStateCache);
            if (rootSignature != null && effectBytecode != null)
                ResourceBinder.Compile(graphicsDevice, rootSignature.EffectDescriptorSetReflection, this.effectBytecode);

            // TODO: Cache over Effect|RootSignature to create binding operations

            // States
            blendState = pipelineStateCache.BlendStateCache.Instantiate(pipelineStateDescription.BlendState);

            this.sampleMask = pipelineStateDescription.SampleMask;
            rasterizerState = pipelineStateCache.RasterizerStateCache.Instantiate(pipelineStateDescription.RasterizerState);
            depthStencilState = pipelineStateCache.DepthStencilStateCache.Instantiate(pipelineStateDescription.DepthStencilState);

            CreateInputLayout(pipelineStateDescription.InputElements);

            primitiveTopology = (D3DPrimitiveTopology)pipelineStateDescription.PrimitiveType;
        }

        internal unsafe void Apply(CommandList commandList, PipelineState previousPipeline)
        {
            var nativeDeviceContext = commandList.NativeDeviceContext;

            if (rootSignature != previousPipeline.rootSignature)
            {
                //rootSignature.Apply
            }

            if (effectBytecode != previousPipeline.effectBytecode)
            {
                if (computeShader != previousPipeline.computeShader)
                    nativeDeviceContext->CSSetShader(computeShader, null, 0);
                if (vertexShader != previousPipeline.vertexShader)
                    nativeDeviceContext->VSSetShader(vertexShader, null, 0);
                if (pixelShader != previousPipeline.pixelShader)
                    nativeDeviceContext->PSSetShader(pixelShader, null, 0);
                if (hullShader != previousPipeline.hullShader)
                    nativeDeviceContext->HSSetShader(hullShader, null, 0);
                if (domainShader != previousPipeline.domainShader)
                    nativeDeviceContext->DSSetShader(domainShader, null, 0);
                if (geometryShader != previousPipeline.geometryShader)
                    nativeDeviceContext->GSSetShader(geometryShader, null, 0);
            }

            if (blendState != previousPipeline.blendState || sampleMask != previousPipeline.sampleMask)
            {
                float blendFactor = 0;
                nativeDeviceContext->OMGetBlendState(null, &blendFactor, null);
                nativeDeviceContext->OMSetBlendState(blendState, &blendFactor, sampleMask);
            }

            if (rasterizerState != previousPipeline.rasterizerState)
            {
                nativeDeviceContext->RSSetState(rasterizerState);
            }

            if (depthStencilState != previousPipeline.depthStencilState)
            {
                uint stencilRef = 0;
                nativeDeviceContext->OMGetDepthStencilState(null, &stencilRef);
                nativeDeviceContext->OMSetDepthStencilState(depthStencilState, stencilRef);
            }

            if (inputLayout != previousPipeline.inputLayout)
            {
                nativeDeviceContext->IASetInputLayout(inputLayout);
            }

            if (primitiveTopology != previousPipeline.primitiveTopology)
            {
                nativeDeviceContext->IASetPrimitiveTopology(primitiveTopology);
            }
        }

        protected unsafe internal override void OnDestroyed()
        {
            var pipelineStateCache = GetPipelineStateCache();

            if (blendState != null)
                pipelineStateCache.BlendStateCache.Release(blendState);
            if (rasterizerState != null)
                pipelineStateCache.RasterizerStateCache.Release(rasterizerState);
            if (depthStencilState != null)
                pipelineStateCache.DepthStencilStateCache.Release(depthStencilState);

            if (vertexShader != null)
                pipelineStateCache.VertexShaderCache.Release(vertexShader);
            if (pixelShader != null)
                pipelineStateCache.PixelShaderCache.Release(pixelShader);
            if (geometryShader != null)
                pipelineStateCache.GeometryShaderCache.Release(geometryShader);
            if (hullShader != null)
                pipelineStateCache.HullShaderCache.Release(hullShader);
            if (domainShader != null)
                pipelineStateCache.DomainShaderCache.Release(domainShader);
            if (computeShader != null)
                pipelineStateCache.ComputeShaderCache.Release(computeShader);

            if (inputLayout != null)
                inputLayout->Release();

            base.OnDestroyed();
        }

        private unsafe void CreateInputLayout(InputElementDescription[] inputElements)
        {
            if (inputElements == null)
                return;

            var nativeInputElements = new InputElementDesc[inputElements.Length];
            for (int index = 0; index < inputElements.Length; index++)
            {
                var inputElement = inputElements[index];
                nativeInputElements[index] = new InputElementDesc
                {
                    InputSlot = (uint)inputElement.InputSlot,
                    SemanticName = (byte*)Marshal.StringToHGlobalAnsi(inputElement.SemanticName).ToPointer(),
                    SemanticIndex = (uint)inputElement.SemanticIndex,
                    AlignedByteOffset = (uint)inputElement.AlignedByteOffset,
                    Format = (Format)inputElement.Format,
                };
            }

            var _inputSignature = inputSignature.AsSpan();
            var _nativeInputElements = nativeInputElements.AsSpan();

            SilkMarshal.ThrowHResult(NativeDevice->CreateInputLayout(
                    ref _nativeInputElements.GetPinnableReference(),
                    (uint)_nativeInputElements.Length,
                    ref _inputSignature.GetPinnableReference(),
                    (nuint)_inputSignature.Length,
                    ref inputLayout
            ));
        }

        private unsafe void CreateShaders(DevicePipelineStateCache pipelineStateCache)
        {
            if (effectBytecode == null)
                return;

            foreach (var shaderBytecode in effectBytecode.Stages)
            {
                var reflection = effectBytecode.Reflection;

                // TODO CACHE Shaders with a bytecode hash
                switch (shaderBytecode.Stage)
                {
                    case ShaderStage.Vertex:
                        vertexShader = pipelineStateCache.VertexShaderCache.Instantiate(shaderBytecode);
                        // Note: input signature can be reused when reseting device since it only stores non-GPU data,
                        // so just keep it if it has already been created before.
                        if (inputSignature == null)
                            inputSignature = shaderBytecode;
                        break;
                    case ShaderStage.Domain:
                        domainShader = pipelineStateCache.DomainShaderCache.Instantiate(shaderBytecode);
                        break;
                    case ShaderStage.Hull:
                        hullShader = pipelineStateCache.HullShaderCache.Instantiate(shaderBytecode);
                        break;
                    case ShaderStage.Geometry:
                        if (reflection.ShaderStreamOutputDeclarations != null && reflection.ShaderStreamOutputDeclarations.Count > 0)
                        {
                            // stream out elements
                            var soElements = new SODeclarationEntry[reflection.ShaderStreamOutputDeclarations.Count];
                            int i = 0;
                            foreach (var streamOutputElement in reflection.ShaderStreamOutputDeclarations)
                            {
                                var soElem = new SODeclarationEntry
                                {
                                    Stream = (uint)streamOutputElement.Stream,
                                    SemanticIndex = (uint)streamOutputElement.SemanticIndex,
                                    SemanticName = (byte*)Marshal.StringToHGlobalAnsi(streamOutputElement.SemanticName).ToPointer(),
                                    StartComponent = streamOutputElement.StartComponent,
                                    ComponentCount = streamOutputElement.ComponentCount,
                                    OutputSlot = streamOutputElement.OutputSlot
                                };
                                soElements[i] = soElem;
                                i++;
                            }

                            var soElmentsSpan = soElements.AsSpan();
                            var stridesSpan = reflection.StreamOutputStrides.Cast<uint>().ToArray().AsSpan();

                            fixed (ID3D11GeometryShader** shader = &geometryShader)
                            fixed (byte* pShaderBytecode = shaderBytecode.Data)
                            {
                                SilkMarshal.ThrowHResult(GraphicsDevice.NativeDevice->CreateGeometryShaderWithStreamOutput(
                                    pShaderBytecode,
                                    (nuint)shaderBytecode.Data.Length,
                                    ref soElmentsSpan.GetPinnableReference(),
                                    (uint)soElmentsSpan.Length,
                                    ref stridesSpan.GetPinnableReference(),
                                    (uint)reflection.StreamOutputStrides.Length,
                                    (uint)reflection.StreamOutputRasterizedStream,
                                    null,
                                    shader)
                                );
                            }


                            // TODO GRAPHICS REFACTOR better cache
                        }
                        else
                        {
                            geometryShader = pipelineStateCache.GeometryShaderCache.Instantiate(shaderBytecode);
                        }
                        break;
                    case ShaderStage.Pixel:
                        pixelShader = pipelineStateCache.PixelShaderCache.Instantiate(shaderBytecode);
                        break;
                    case ShaderStage.Compute:
                        computeShader = pipelineStateCache.ComputeShaderCache.Instantiate(shaderBytecode);
                        break;
                }
            }
        }

        // Small helper to cache SharpDX graphics objects
        private class GraphicsCache<TSource, TKey, TValue> : IDisposable where TValue : IDisposable
        {
            private object lockObject = new object();

            // Store instantiated objects
            private readonly Dictionary<TKey, TValue> storage = new Dictionary<TKey, TValue>();
            // Used for quick removal
            private readonly Dictionary<TValue, TKey> reverse = new Dictionary<TValue, TKey>();

            private readonly Dictionary<TValue, int> counter = new Dictionary<TValue, int>();

            private readonly Func<TSource, TKey> computeKey;
            private readonly Func<TSource, TValue> computeValue;

            public GraphicsCache(Func<TSource, TKey> computeKey, Func<TSource, TValue> computeValue)
            {
                this.computeKey = computeKey;
                this.computeValue = computeValue;
            }

            public TValue Instantiate(TSource source)
            {
                lock (lockObject)
                {
                    TValue value;
                    var key = computeKey(source);
                    if (!storage.TryGetValue(key, out value))
                    {
                        value = computeValue(source);
                        storage.Add(key, value);
                        reverse.Add(value, key);
                        counter.Add(value, 1);
                    }
                    else
                    {
                        counter[value] = counter[value] + 1;
                    }

                    return value;
                }
            }

            public void Release(TValue value)
            {
                // Should we remove it from the cache?
                lock (lockObject)
                {
                    int refCount;
                    if (!counter.TryGetValue(value, out refCount))
                        return;

                    counter[value] = --refCount;
                    if (refCount == 0)
                    {
                        counter.Remove(value);
                        reverse.Remove(value);
                        TKey key;
                        if (reverse.TryGetValue(value, out key))
                        {
                            storage.Remove(key);
                        }

                        value.Dispose();
                    }
                }
            }

            public void Dispose()
            {
                lock (lockObject)
                {
                    // Release everything
                    foreach (var entry in reverse)
                    {
                        entry.Key.Dispose();
                    }

                    reverse.Clear();
                    storage.Clear();
                    counter.Clear();
                }
            }
        }

        private DevicePipelineStateCache GetPipelineStateCache()
        {
            return GraphicsDevice.GetOrCreateSharedData(typeof(DevicePipelineStateCache), device => new DevicePipelineStateCache(device));
        }

        // Caches
        private class DevicePipelineStateCache : IDisposable
        {
            public readonly GraphicsCache<ShaderBytecode, ObjectId, ComPtr<ID3D11VertexShader>> VertexShaderCache;
            public readonly GraphicsCache<ShaderBytecode, ObjectId, ComPtr<ID3D11PixelShader>> PixelShaderCache;
            public readonly GraphicsCache<ShaderBytecode, ObjectId, ComPtr<ID3D11GeometryShader>> GeometryShaderCache;
            public readonly GraphicsCache<ShaderBytecode, ObjectId, ComPtr<ID3D11HullShader>> HullShaderCache;
            public readonly GraphicsCache<ShaderBytecode, ObjectId, ComPtr<ID3D11DomainShader>> DomainShaderCache;
            public readonly GraphicsCache<ShaderBytecode, ObjectId, ComPtr<ID3D11ComputeShader>> ComputeShaderCache;
            public readonly GraphicsCache<BlendStateDescription, BlendStateDescription, ComPtr<ID3D11BlendState>> BlendStateCache;
            public readonly GraphicsCache<RasterizerStateDescription, RasterizerStateDescription, ComPtr<ID3D11RasterizerState>> RasterizerStateCache;
            public readonly GraphicsCache<DepthStencilStateDescription, DepthStencilStateDescription, ComPtr<ID3D11DepthStencilState>> DepthStencilStateCache;

            public DevicePipelineStateCache(GraphicsDevice graphicsDevice)
            {
                #region ShaderCreator
                unsafe ComPtr<ID3D11VertexShader> CreateVertexShader(ShaderBytecode source)
                {
                    ComPtr<ID3D11VertexShader> shader = default;
                    fixed (byte* pShaderBytecode = source.Data)
                    {
                        SilkMarshal.ThrowHResult(graphicsDevice.NativeDevice->CreateVertexShader(pShaderBytecode, (nuint)source.Data.Length, null, shader.GetAddressOf()));
                    }
                    return shader;
                }

                unsafe ComPtr<ID3D11PixelShader> CreatePixelShader(ShaderBytecode source)
                {
                    ComPtr<ID3D11PixelShader> shader = default;
                    fixed (byte* pShaderBytecode = source.Data)
                    {
                        SilkMarshal.ThrowHResult(graphicsDevice.NativeDevice->CreatePixelShader(pShaderBytecode, (nuint)source.Data.Length, null, shader.GetAddressOf()));
                    }
                    return shader;
                }

                unsafe ComPtr<ID3D11GeometryShader> CreateGeometryShader(ShaderBytecode source)
                {
                    ComPtr<ID3D11GeometryShader> shader = default;
                    fixed (byte* pShaderBytecode = source.Data)
                    {
                        SilkMarshal.ThrowHResult(graphicsDevice.NativeDevice->CreateGeometryShader(pShaderBytecode, (nuint)source.Data.Length, null, shader.GetAddressOf()));
                    }
                    return shader;
                }

                unsafe ComPtr<ID3D11HullShader> CreateHullShader(ShaderBytecode source)
                {
                    ComPtr<ID3D11HullShader> shader = default;
                    fixed (byte* pShaderBytecode = source.Data)
                    {
                        SilkMarshal.ThrowHResult(graphicsDevice.NativeDevice->CreateHullShader(pShaderBytecode, (nuint)source.Data.Length, null, shader.GetAddressOf()));
                    }
                    return shader;
                }

                unsafe ComPtr<ID3D11DomainShader> CreateDomainShader(ShaderBytecode source)
                {
                    ComPtr<ID3D11DomainShader> shader = default;
                    fixed (byte* pShaderBytecode = source.Data)
                    {
                        SilkMarshal.ThrowHResult(graphicsDevice.NativeDevice->CreateDomainShader(pShaderBytecode, (nuint)source.Data.Length, null, shader.GetAddressOf()));
                    }
                    return shader;
                }

                unsafe ComPtr<ID3D11ComputeShader> CreateComputeShader(ShaderBytecode source)
                {
                    ComPtr<ID3D11ComputeShader> shader = default;
                    fixed (byte* pShaderBytecode = source.Data)
                    {
                        SilkMarshal.ThrowHResult(graphicsDevice.NativeDevice->CreateComputeShader(pShaderBytecode, (nuint)source.Data.Length, null, shader.GetAddressOf()));
                    }
                    return shader;
                }

                unsafe ComPtr<ID3D11BlendState> InitBlendState(BlendStateDescription source)
                {
                    ComPtr<ID3D11BlendState> state = default;
                    var desc = CreateBlendState(source);
                    SilkMarshal.ThrowHResult(graphicsDevice.NativeDevice->CreateBlendState(&desc, state.GetAddressOf()));

                    return state;
                }

                unsafe ComPtr<ID3D11RasterizerState> InitRasterizerState(RasterizerStateDescription source)
                {
                    ComPtr<ID3D11RasterizerState> state = default;
                    var desc = CreateRasterizerState(source);
                    SilkMarshal.ThrowHResult(graphicsDevice.NativeDevice->CreateRasterizerState(&desc, state.GetAddressOf()));

                    return state;
                }

                unsafe ComPtr<ID3D11DepthStencilState> InitDepthStencilState(DepthStencilStateDescription source)
                {
                    ComPtr<ID3D11DepthStencilState> state = default;
                    var desc = CreateDepthStencilState(source);
                    SilkMarshal.ThrowHResult(graphicsDevice.NativeDevice->CreateDepthStencilState(&desc, state.GetAddressOf()));

                    return state;
                }
                #endregion

                // Shaders
                VertexShaderCache = new GraphicsCache<ShaderBytecode, ObjectId, ComPtr<ID3D11VertexShader>>(source => source.Id, CreateVertexShader);
                PixelShaderCache = new GraphicsCache<ShaderBytecode, ObjectId, ComPtr<ID3D11PixelShader>>(source => source.Id, CreatePixelShader);
                GeometryShaderCache = new GraphicsCache<ShaderBytecode, ObjectId, ComPtr<ID3D11GeometryShader>>(source => source.Id, CreateGeometryShader);
                HullShaderCache = new GraphicsCache<ShaderBytecode, ObjectId, ComPtr<ID3D11HullShader>>(source => source.Id, CreateHullShader);
                DomainShaderCache = new GraphicsCache<ShaderBytecode, ObjectId, ComPtr<ID3D11DomainShader>>(source => source.Id, CreateDomainShader);
                ComputeShaderCache = new GraphicsCache<ShaderBytecode, ObjectId, ComPtr<ID3D11ComputeShader>>(source => source.Id, CreateComputeShader);

                // States
                BlendStateCache = new GraphicsCache<BlendStateDescription, BlendStateDescription, ComPtr<ID3D11BlendState>>(source => source, InitBlendState);
                RasterizerStateCache = new GraphicsCache<RasterizerStateDescription, RasterizerStateDescription, ComPtr<ID3D11RasterizerState>>(source => source, InitRasterizerState);
                DepthStencilStateCache = new GraphicsCache<DepthStencilStateDescription, DepthStencilStateDescription, ComPtr<ID3D11DepthStencilState>>(source => source, InitDepthStencilState);
            }

            private unsafe BlendDesc CreateBlendState(BlendStateDescription description)
            {
                var nativeDescription = new BlendDesc();

                nativeDescription.AlphaToCoverageEnable = Convert.ToInt32(description.AlphaToCoverageEnable);
                nativeDescription.IndependentBlendEnable = Convert.ToInt32(description.IndependentBlendEnable);

                var renderTargets = &description.RenderTarget0;
                for (int i = 0; i < 8; i++)
                {
                    ref var renderTarget = ref renderTargets[i];
                    ref var nativeRenderTarget = ref nativeDescription.RenderTarget[i];
                    nativeRenderTarget.BlendEnable = Convert.ToInt32(renderTarget.BlendEnable);
                    nativeRenderTarget.SrcBlend = (Silk.NET.Direct3D11.Blend)renderTarget.ColorSourceBlend;
                    nativeRenderTarget.DestBlend = (Silk.NET.Direct3D11.Blend)renderTarget.ColorDestinationBlend;
                    nativeRenderTarget.BlendOp = (BlendOp)renderTarget.ColorBlendFunction;
                    nativeRenderTarget.SrcBlendAlpha = (Silk.NET.Direct3D11.Blend)renderTarget.AlphaSourceBlend;
                    nativeRenderTarget.DestBlendAlpha = (Silk.NET.Direct3D11.Blend)renderTarget.AlphaDestinationBlend;
                    nativeRenderTarget.BlendOpAlpha = (BlendOp)renderTarget.AlphaBlendFunction;
                    nativeRenderTarget.RenderTargetWriteMask = (byte)renderTarget.ColorWriteChannels;
                }

                return nativeDescription;
            }

            private RasterizerDesc CreateRasterizerState(RasterizerStateDescription description)
            {
                RasterizerDesc nativeDescription;

                nativeDescription.CullMode = (Silk.NET.Direct3D11.CullMode)description.CullMode;
                nativeDescription.FillMode = (Silk.NET.Direct3D11.FillMode)description.FillMode;
                nativeDescription.FrontCounterClockwise = Convert.ToInt32(description.FrontFaceCounterClockwise);
                nativeDescription.DepthBias = description.DepthBias;
                nativeDescription.SlopeScaledDepthBias = description.SlopeScaleDepthBias;
                nativeDescription.DepthBiasClamp = description.DepthBiasClamp;
                nativeDescription.DepthClipEnable = Convert.ToInt32(description.DepthClipEnable);
                nativeDescription.ScissorEnable = Convert.ToInt32(description.ScissorTestEnable);
                nativeDescription.MultisampleEnable = Convert.ToInt32(description.MultisampleCount > MultisampleCount.None);
                nativeDescription.AntialiasedLineEnable = Convert.ToInt32(description.MultisampleAntiAliasLine);

                return nativeDescription;
            }

            private DepthStencilDesc CreateDepthStencilState(DepthStencilStateDescription description)
            {
                DepthStencilDesc nativeDescription;

                nativeDescription.DepthEnable = Convert.ToInt32(description.DepthBufferEnable);
                nativeDescription.DepthFunc = (ComparisonFunc)description.DepthBufferFunction;
                nativeDescription.DepthWriteMask = description.DepthBufferWriteEnable ? DepthWriteMask.DepthWriteMaskAll : DepthWriteMask.DepthWriteMaskZero;

                nativeDescription.StencilEnable = Convert.ToInt32(description.StencilEnable);
                nativeDescription.StencilReadMask = description.StencilMask;
                nativeDescription.StencilWriteMask = description.StencilWriteMask;

                nativeDescription.FrontFace.StencilFailOp = (StencilOp)description.FrontFace.StencilFail;
                nativeDescription.FrontFace.StencilPassOp = (StencilOp)description.FrontFace.StencilPass;
                nativeDescription.FrontFace.StencilDepthFailOp = (StencilOp)description.FrontFace.StencilDepthBufferFail;
                nativeDescription.FrontFace.StencilFunc = (ComparisonFunc)description.FrontFace.StencilFunction;

                nativeDescription.BackFace.StencilFailOp = (StencilOp)description.BackFace.StencilFail;
                nativeDescription.BackFace.StencilPassOp = (StencilOp)description.BackFace.StencilPass;
                nativeDescription.BackFace.StencilDepthFailOp = (StencilOp)description.BackFace.StencilDepthBufferFail;
                nativeDescription.BackFace.StencilFunc = (ComparisonFunc)description.BackFace.StencilFunction;

                return nativeDescription;
            }

            public void Dispose()
            {
                VertexShaderCache.Dispose();
                PixelShaderCache.Dispose();
                GeometryShaderCache.Dispose();
                HullShaderCache.Dispose();
                DomainShaderCache.Dispose();
                ComputeShaderCache.Dispose();
                BlendStateCache.Dispose();
                RasterizerStateCache.Dispose();
                DepthStencilStateCache.Dispose();
            }
        }
    }
}
#endif
