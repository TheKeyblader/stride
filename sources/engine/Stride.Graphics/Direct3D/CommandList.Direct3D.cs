// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if STRIDE_GRAPHICS_API_DIRECT3D11
using System;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Shaders;

using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Core.Native;
using System.Runtime.CompilerServices;
using System.Linq;

namespace Stride.Graphics
{
    public partial class CommandList
    {
        private const int ConstantBufferCount = D3D11.CommonshaderConstantBufferApiSlotCount; // 14 actually
        private const int SamplerStateCount = D3D11.CommonshaderSamplerSlotCount;
        private const int ShaderResourceViewCount = D3D11.CommonshaderInputResourceSlotCount;
        private const int SimultaneousRenderTargetCount = D3D11.SimultaneousRenderTargetCount;
        private const int StageCount = 6;
        private const int UnorderedAcccesViewCount = D3D11.PSCSUavRegisterCount;

        private unsafe ID3D11DeviceContext* nativeDeviceContext;
        private unsafe ID3D11DeviceContext1* nativeDeviceContext1;
        private unsafe ID3DUserDefinedAnnotation* nativeDeviceProfiler;

        private unsafe readonly ID3D11RenderTargetView*[] currentRenderTargetViews = new ID3D11RenderTargetView*[SimultaneousRenderTargetCount];
        private int currentRenderTargetViewsActiveCount = 0;
        private unsafe readonly ID3D11UnorderedAccessView*[] currentUARenderTargetViews = new ID3D11UnorderedAccessView*[SimultaneousRenderTargetCount];
        private unsafe readonly CommonShaderStage[] shaderStages = new CommonShaderStage[StageCount];
        private readonly Buffer[] constantBuffers = new Buffer[StageCount * ConstantBufferCount];
        private readonly SamplerState[] samplerStates = new SamplerState[StageCount * SamplerStateCount];
        private unsafe readonly ID3D11UnorderedAccessView*[] unorderedAccessViews = new ID3D11UnorderedAccessView*[UnorderedAcccesViewCount]; // Only CS

        private PipelineState currentPipelineState;

        public static CommandList New(GraphicsDevice device)
        {
            throw new InvalidOperationException("Can't create multiple command lists with D3D11");
        }

        internal unsafe CommandList(GraphicsDevice device) : base(device)
        {
            nativeDeviceContext = device.NativeDeviceContext;
            NativeDeviceChild = (ID3D11Resource*)nativeDeviceContext;

            nativeDeviceContext1 = (ID3D11DeviceContext1*)nativeDeviceContext;
            nativeDeviceProfiler = null;
            if (device.IsDebugMode)
            {
                fixed (ID3DUserDefinedAnnotation** temp = &nativeDeviceProfiler)
                    SilkMarshal.ThrowHResult
                    (
                        nativeDeviceContext->QueryInterface(ref SilkMarshal.GuidOf<ID3DUserDefinedAnnotation>(), (void**)temp)
                    );
            }

            InitializeStages();

            ClearState();
        }

        /// <summary>
        /// Gets the native device context.
        /// </summary>
        /// <value>The native device context.</value>
        internal unsafe ID3D11DeviceContext* NativeDeviceContext => nativeDeviceContext;

        /// <inheritdoc/>
        protected internal unsafe override void OnDestroyed()
        {
            if (nativeDeviceProfiler != null)
            {
                nativeDeviceProfiler->Release();
                nativeDeviceProfiler = null;
            }

            base.OnDestroyed();
        }

        public void Reset()
        {
        }

        public void Flush()
        {
        }

        public CompiledCommandList Close()
        {
            return default(CompiledCommandList);
        }

        private unsafe void ClearStateImpl()
        {
            NativeDeviceContext->ClearState();

            for (int i = 0; i < samplerStates.Length; ++i)
                samplerStates[i] = null;
            for (int i = 0; i < constantBuffers.Length; ++i)
                constantBuffers[i] = null;
            for (int i = 0; i < unorderedAccessViews.Length; ++i)
                unorderedAccessViews[i] = null;
            for (int i = 0; i < currentRenderTargetViews.Length; i++)
                currentRenderTargetViews[i] = null;
            for (int i = 0; i < currentUARenderTargetViews.Length; i++)
                currentUARenderTargetViews[i] = null;

            // Since nothing can be drawn in default state, no need to set anything (another SetPipelineState should happen before)
            currentPipelineState = GraphicsDevice.DefaultPipelineState;
        }

        /// <summary>
        /// Unbinds all depth-stencil buffer and render targets from the output-merger stage.
        /// </summary>
        private unsafe void ResetTargetsImpl()
        {
            for (int i = 0; i < currentRenderTargetViews.Length; i++)
                currentRenderTargetViews[i] = null;
            for (int i = 0; i < currentUARenderTargetViews.Length; i++)
                currentUARenderTargetViews[i] = null;
            nativeDeviceContext->OMSetRenderTargets(0, null, null);
        }

        /// <summary>
        /// Binds a depth-stencil buffer and a set of render targets to the output-merger stage. See <see cref="Textures+and+render+targets"/> to learn how to use it.
        /// </summary>
        /// <param name="depthStencilBuffer">The depth stencil buffer.</param>
        /// <param name="renderTargetCount">The number of render targets.</param>
        /// <param name="renderTargets">The render targets.</param>
        /// <exception cref="System.ArgumentNullException">renderTargetViews</exception>
        private unsafe void SetRenderTargetsImpl(Texture depthStencilBuffer, int renderTargetCount, Texture[] renderTargets)
        {
            currentRenderTargetViewsActiveCount = renderTargetCount;

            for (int i = 0; i < renderTargetCount; i++)
                currentRenderTargetViews[i] = renderTargets[i].NativeRenderTargetView;

            var localArr = new ID3D11RenderTargetView[currentRenderTargetViews.Length];
            for (int i = 0; i < renderTargetCount; i++)
                localArr[i] = *currentRenderTargetViews[i];

            var localSpan = localArr.AsSpan();
            fixed (ID3D11RenderTargetView* localPtr = localSpan)
            {
                NativeDeviceContext->OMSetRenderTargets((uint)currentRenderTargetViews.Length, &localPtr, depthStencilBuffer.NativeDepthStencilView);
            }
        }

        unsafe partial void SetScissorRectangleImpl(ref Rectangle scissorRectangle)
        {
            var local = Unsafe.As<Rectangle, Silk.NET.Maths.Rectangle<int>>(ref scissorRectangle);
            NativeDeviceContext->RSSetScissorRects(1, &local);
        }

        unsafe partial void SetScissorRectanglesImpl(int scissorCount, Rectangle[] scissorRectangles)
        {
            if (scissorRectangles == null) throw new ArgumentNullException("scissorRectangles");
            var _scissorRectangles = scissorRectangles.AsSpan();
            fixed (Rectangle* ptr = _scissorRectangles)
            {
                NativeDeviceContext->RSSetScissorRects((uint)scissorRectangles.Length, (Silk.NET.Maths.Rectangle<int>*)ptr);
            }
        }

        /// <summary>
        /// Sets the stream targets.
        /// </summary>
        /// <param name="buffers">The buffers.</param>
        public unsafe void SetStreamTargets(params Buffer[] buffers)
        {
            Span<ID3D11Buffer> streamOutputBufferBindings;

            if (buffers != null)
            {
                streamOutputBufferBindings = new ID3D11Buffer[buffers.Length];
                for (int i = 0; i < buffers.Length; ++i)
                    streamOutputBufferBindings[i] = *buffers[i].NativeBuffer;
            }
            else
            {
                streamOutputBufferBindings = null;
            }

            fixed (ID3D11Buffer* ptr = streamOutputBufferBindings)
            {
                uint offsets = 0;
                NativeDeviceContext->SOSetTargets((uint)streamOutputBufferBindings.Length, &ptr, ref offsets);
            }
        }

        /// <summary>
        ///     Gets or sets the 1st viewport. See <see cref="Render+states"/> to learn how to use it.
        /// </summary>
        /// <value>The viewport.</value>
        private unsafe void SetViewportImpl()
        {
            if (!viewportDirty)
                return;

            viewportDirty = false;

            fixed (Viewport* viewportsPtr = viewports)
            {
                nativeDeviceContext->RSSetViewports((uint)(renderTargetCount > 0 ? renderTargetCount : 1), (Silk.NET.Direct3D11.Viewport*)viewportsPtr);
            }
        }

        /// <summary>
        /// Unsets the render targets.
        /// </summary>
        public unsafe void UnsetRenderTargets()
        {
            nativeDeviceContext->OMSetRenderTargets(0, null, null);
        }

        /// <summary>
        ///     Sets a constant buffer to the shader pipeline.
        /// </summary>
        /// <param name="stage">The shader stage.</param>
        /// <param name="slot">The binding slot.</param>
        /// <param name="buffer">The constant buffer to set.</param>
        internal unsafe void SetConstantBuffer(ShaderStage stage, int slot, Buffer buffer)
        {
            if (stage == ShaderStage.None)
                throw new ArgumentException("Cannot use Stage.None", "stage");

            int stageIndex = (int)stage - 1;

            int slotIndex = stageIndex * ConstantBufferCount + slot;
            if (constantBuffers[slotIndex] != buffer)
            {
                constantBuffers[slotIndex] = buffer;
                shaderStages[stageIndex].SetConstantBuffer(slot, buffer != null ? buffer.NativeBuffer : null);
            }
        }

        /// <summary>
        ///     Sets a constant buffer to the shader pipeline.
        /// </summary>
        /// <param name="stage">The shader stage.</param>
        /// <param name="slot">The binding slot.</param>
        /// <param name="buffer">The constant buffer to set.</param>
        internal unsafe void SetConstantBuffer(ShaderStage stage, int slot, int offset, Buffer buffer)
        {
            if (stage == ShaderStage.None)
                throw new ArgumentException("Cannot use Stage.None", "stage");

            int stageIndex = (int)stage - 1;

            int slotIndex = stageIndex * ConstantBufferCount + slot;
            if (constantBuffers[slotIndex] != buffer)
            {
                constantBuffers[slotIndex] = buffer;
                shaderStages[stageIndex].SetConstantBuffer(slot, buffer != null ? buffer.NativeBuffer : null);
            }
        }

        /// <summary>
        ///     Sets a sampler state to the shader pipeline.
        /// </summary>
        /// <param name="stage">The shader stage.</param>
        /// <param name="slot">The binding slot.</param>
        /// <param name="samplerState">The sampler state to set.</param>
        internal unsafe void SetSamplerState(ShaderStage stage, int slot, SamplerState samplerState)
        {
            if (stage == ShaderStage.None)
                throw new ArgumentException("Cannot use Stage.None", "stage");
            int stageIndex = (int)stage - 1;

            int slotIndex = stageIndex * SamplerStateCount + slot;
            if (samplerStates[slotIndex] != samplerState)
            {
                samplerStates[slotIndex] = samplerState;
                shaderStages[stageIndex].SetSampler(slot, samplerState != null ? (ID3D11SamplerState*)samplerState.NativeDeviceChild : null);
            }
        }

        /// <summary>
        ///     Sets a shader resource view to the shader pipeline.
        /// </summary>
        /// <param name="stage">The shader stage.</param>
        /// <param name="slot">The binding slot.</param>
        /// <param name="shaderResourceView">The shader resource view.</param>
        internal unsafe void SetShaderResourceView(ShaderStage stage, int slot, GraphicsResource shaderResourceView)
        {
            shaderStages[(int)stage - 1].SetShaderResource(slot, shaderResourceView != null ? shaderResourceView.NativeShaderResourceView : null);
        }

        /// <summary>
        ///     Sets an unordered access view to the shader pipeline, without affecting ones that are already set.
        /// </summary>
        /// <param name="slot">The binding slot.</param>
        /// <param name="shaderResourceView">The shader resource view.</param>
        /// <param name="view">The native unordered access view.</param>
        /// <param name="uavInitialOffset">The Append/Consume buffer offset. See SetUnorderedAccessView for more details.</param>
        internal unsafe void OMSetSingleUnorderedAccessView(int slot, ID3D11UnorderedAccessView* view, uint uavInitialOffset)
        {
            currentUARenderTargetViews[slot] = view;

            int remainingSlots = currentUARenderTargetViews.Length - currentRenderTargetViewsActiveCount;

            var uavs = new ID3D11UnorderedAccessView[remainingSlots];
            Array.Copy(currentUARenderTargetViews, currentRenderTargetViewsActiveCount, uavs, 0, remainingSlots);

            var uavInitialCounts = new int[remainingSlots];
            for (int i = 0; i < remainingSlots; i++)
                uavInitialCounts[i] = -1;
            uavInitialCounts[slot - currentRenderTargetViewsActiveCount] = (int)uavInitialOffset;

            var uavsSpan = uavs.AsSpan();
            var uavInitialCountsSpan = uavInitialCounts.Cast<uint>().ToArray().AsSpan();

            fixed (ID3D11UnorderedAccessView* _uavs = &uavsSpan.GetPinnableReference())
            {

                NativeDeviceContext->OMSetRenderTargetsAndUnorderedAccessViews(
                    D3D11.KeepRenderTargetsAndDepthStencil,
                    null,
                    null,
                    (uint)currentRenderTargetViewsActiveCount,
                    (uint)uavs.Length,
                     &_uavs,
                     ref uavInitialCountsSpan.GetPinnableReference());
            }
        }

        /// <summary>
        /// Sets an unordered access view to the shader pipeline.
        /// </summary>
        /// <param name="stage">The stage.</param>
        /// <param name="slot">The slot.</param>
        /// <param name="unorderedAccessView">The unordered access view.</param>
        /// <param name="uavInitialOffset">The Append/Consume buffer offset. A value of -1 indicates the current offset
        ///     should be kept. Any other values set the hidden counter for that Appendable/Consumable
        ///     UAV. uavInitialCount is only relevant for UAVs which have the 'Append' or 'Counter' buffer
        ///     flag, otherwise the argument is ignored.</param>
        /// <exception cref="System.ArgumentException">Invalid stage.;stage</exception>
        internal unsafe void SetUnorderedAccessView(ShaderStage stage, int slot, GraphicsResource unorderedAccessView, uint uavInitialOffset)
        {
            if (stage != ShaderStage.Compute && stage != ShaderStage.Pixel)
                throw new ArgumentException("Invalid stage.", "stage");

            var view = unorderedAccessView.NativeUnorderedAccessView;
            if (stage == ShaderStage.Compute)
            {
                if (unorderedAccessViews[slot] != view)
                {
                    unorderedAccessViews[slot] = view;
                    NativeDeviceContext->CSSetUnorderedAccessViews((uint)slot, 1, ref view, ref uavInitialOffset);
                }
            }
            else
            {
                if (currentUARenderTargetViews[slot] != view)
                {
                    OMSetSingleUnorderedAccessView(slot, view, uavInitialOffset);
                }
            }
        }

        /// <summary>
        /// Unsets an unordered access view from the shader pipeline.
        /// </summary>
        /// <param name="unorderedAccessView">The unordered access view.</param>
        internal unsafe void UnsetUnorderedAccessView(GraphicsResource unorderedAccessView)
        {
            if (unorderedAccessView == null || unorderedAccessView.NativeUnorderedAccessView == null)
                return;

            var view = unorderedAccessView.NativeUnorderedAccessView;

            for (int slot = 0; slot < UnorderedAcccesViewCount; slot++)
            {
                if (unorderedAccessViews[slot] == view)
                {
                    unorderedAccessViews[slot] = null;
                    var views = (ID3D11UnorderedAccessView*)Unsafe.AsPointer(ref SilkMarshal.NullRef<ID3D11UnorderedAccessView>());
                    NativeDeviceContext->CSSetUnorderedAccessViews((uint)slot, 1, ref views, null);
                }
            }
            for (int slot = 0; slot < SimultaneousRenderTargetCount; slot++)
            {
                if (currentUARenderTargetViews[slot] == view)
                {
                    OMSetSingleUnorderedAccessView(slot, null, unchecked((uint)-1));
                }
            }
        }

        /// <summary>
        ///     Prepares a draw call. This method is called before each Draw() method to setup the correct Primitive, InputLayout and VertexBuffers.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Cannot GraphicsDevice.Draw*() without an effect being previously applied with Effect.Apply() method</exception>
        private void PrepareDraw()
        {
            SetViewportImpl();
        }

        public unsafe void SetStencilReference(int stencilReference)
        {
            ID3D11DepthStencilState* state = null;
            uint @ref = 0;
            nativeDeviceContext->OMGetDepthStencilState(ref state, ref @ref);
            nativeDeviceContext->OMSetDepthStencilState(state, (uint)stencilReference);
        }

        public unsafe void SetBlendFactor(Color4 blendFactor)
        {
            ID3D11BlendState* state = null;
            float* factor = null;
            uint mask = 0;
            nativeDeviceContext->OMGetBlendState(ref state, factor, ref mask);
            nativeDeviceContext->OMSetBlendState(state, ColorHelper.Convert(blendFactor), mask);
        }

        public void SetPipelineState(PipelineState pipelineState)
        {
            var newPipelineState = pipelineState ?? GraphicsDevice.DefaultPipelineState;

            // Pipeline state
            if (newPipelineState != currentPipelineState)
            {
                newPipelineState.Apply(this, currentPipelineState);
                currentPipelineState = newPipelineState;
            }
        }

        public unsafe void SetVertexBuffer(int index, Buffer buffer, int offset, int stride)
        {
            uint _offset = (uint)offset;
            uint _stride = (uint)stride;
            var _buffer = buffer.NativeBuffer;
            nativeDeviceContext->IASetVertexBuffers((uint)index, 1, ref _buffer, ref _stride, ref _offset);
        }

        public unsafe void SetIndexBuffer(Buffer buffer, int offset, bool is32bits)
        {
            nativeDeviceContext->IASetIndexBuffer(buffer != null ? buffer.NativeBuffer : null, is32bits ? Format.FormatR32Uint : Format.FormatR16Uint, (uint)offset);
        }

        public void ResourceBarrierTransition(GraphicsResource resource, GraphicsResourceState newState)
        {
            // Nothing to do
        }

        public void SetDescriptorSets(int index, DescriptorSet[] descriptorSets)
        {
            // Bind resources
            currentPipelineState?.ResourceBinder.BindResources(this, descriptorSets);
        }

        /// <inheritdoc />
        public unsafe void Dispatch(int threadCountX, int threadCountY, int threadCountZ)
        {
            PrepareDraw();

            NativeDeviceContext->Dispatch((uint)threadCountX, (uint)threadCountY, (uint)threadCountZ);
        }

        /// <summary>
        /// Dispatches the specified indirect buffer.
        /// </summary>
        /// <param name="indirectBuffer">The indirect buffer.</param>
        /// <param name="offsetInBytes">The offset information bytes.</param>
        public unsafe void Dispatch(Buffer indirectBuffer, int offsetInBytes)
        {
            PrepareDraw();

            if (indirectBuffer == null) throw new ArgumentNullException("indirectBuffer");
            NativeDeviceContext->DispatchIndirect(indirectBuffer.NativeBuffer, (uint)offsetInBytes);
        }

        /// <summary>
        /// Draw non-indexed, non-instanced primitives.
        /// </summary>
        /// <param name="vertexCount">Number of vertices to draw.</param>
        /// <param name="startVertexLocation">Index of the first vertex, which is usually an offset in a vertex buffer; it could also be used as the first vertex id generated for a shader parameter marked with the <strong>SV_TargetId</strong> system-value semantic.</param>
        public unsafe void Draw(int vertexCount, int startVertexLocation = 0)
        {
            PrepareDraw();

            NativeDeviceContext->Draw((uint)vertexCount, (uint)startVertexLocation);

            GraphicsDevice.FrameTriangleCount += (uint)vertexCount;
            GraphicsDevice.FrameDrawCalls++;
        }

        /// <summary>
        /// Draw geometry of an unknown size.
        /// </summary>
        public unsafe void DrawAuto()
        {
            PrepareDraw();

            NativeDeviceContext->DrawAuto();

            GraphicsDevice.FrameDrawCalls++;
        }

        /// <summary>
        /// Draw indexed, non-instanced primitives.
        /// </summary>
        /// <param name="indexCount">Number of indices to draw.</param>
        /// <param name="startIndexLocation">The location of the first index read by the GPU from the index buffer.</param>
        /// <param name="baseVertexLocation">A value added to each index before reading a vertex from the vertex buffer.</param>
        public unsafe void DrawIndexed(int indexCount, int startIndexLocation = 0, int baseVertexLocation = 0)
        {
            PrepareDraw();

            NativeDeviceContext->DrawIndexed((uint)indexCount, (uint)startIndexLocation, baseVertexLocation);

            GraphicsDevice.FrameDrawCalls++;
            GraphicsDevice.FrameTriangleCount += (uint)indexCount;
        }

        /// <summary>
        /// Draw indexed, instanced primitives.
        /// </summary>
        /// <param name="indexCountPerInstance">Number of indices read from the index buffer for each instance.</param>
        /// <param name="instanceCount">Number of instances to draw.</param>
        /// <param name="startIndexLocation">The location of the first index read by the GPU from the index buffer.</param>
        /// <param name="baseVertexLocation">A value added to each index before reading a vertex from the vertex buffer.</param>
        /// <param name="startInstanceLocation">A value added to each index before reading per-instance data from a vertex buffer.</param>
        public unsafe void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation = 0, int baseVertexLocation = 0, int startInstanceLocation = 0)
        {
            PrepareDraw();

            NativeDeviceContext->DrawIndexedInstanced((uint)indexCountPerInstance, (uint)instanceCount, (uint)startIndexLocation, baseVertexLocation, (uint)startInstanceLocation);

            GraphicsDevice.FrameDrawCalls++;
            GraphicsDevice.FrameTriangleCount += (uint)(indexCountPerInstance * instanceCount);
        }

        /// <summary>
        /// Draw indexed, instanced, GPU-generated primitives.
        /// </summary>
        /// <param name="argumentsBuffer">A buffer containing the GPU generated primitives.</param>
        /// <param name="alignedByteOffsetForArgs">Offset in <em>pBufferForArgs</em> to the start of the GPU generated primitives.</param>
        public unsafe void DrawIndexedInstanced(Buffer argumentsBuffer, int alignedByteOffsetForArgs = 0)
        {
            if (argumentsBuffer == null) throw new ArgumentNullException("argumentsBuffer");

            PrepareDraw();

            NativeDeviceContext->DrawIndexedInstancedIndirect(argumentsBuffer.NativeBuffer, (uint)alignedByteOffsetForArgs);

            GraphicsDevice.FrameDrawCalls++;
        }

        /// <summary>
        /// Draw non-indexed, instanced primitives.
        /// </summary>
        /// <param name="vertexCountPerInstance">Number of vertices to draw.</param>
        /// <param name="instanceCount">Number of instances to draw.</param>
        /// <param name="startVertexLocation">Index of the first vertex.</param>
        /// <param name="startInstanceLocation">A value added to each index before reading per-instance data from a vertex buffer.</param>
        public unsafe void DrawInstanced(int vertexCountPerInstance, int instanceCount, int startVertexLocation = 0, int startInstanceLocation = 0)
        {
            PrepareDraw();

            NativeDeviceContext->DrawInstanced((uint)vertexCountPerInstance, (uint)instanceCount, (uint)startVertexLocation, (uint)startInstanceLocation);

            GraphicsDevice.FrameDrawCalls++;
            GraphicsDevice.FrameTriangleCount += (uint)(vertexCountPerInstance * instanceCount);
        }

        /// <summary>
        /// Draw instanced, GPU-generated primitives.
        /// </summary>
        /// <param name="argumentsBuffer">An arguments buffer</param>
        /// <param name="alignedByteOffsetForArgs">Offset in <em>pBufferForArgs</em> to the start of the GPU generated primitives.</param>
        public unsafe void DrawInstanced(Buffer argumentsBuffer, int alignedByteOffsetForArgs = 0)
        {
            if (argumentsBuffer == null) throw new ArgumentNullException("argumentsBuffer");

            PrepareDraw();

            NativeDeviceContext->DrawIndexedInstancedIndirect(argumentsBuffer.NativeBuffer, (uint)alignedByteOffsetForArgs);

            GraphicsDevice.FrameDrawCalls++;
        }

        /// <summary>
        /// Submits a GPU timestamp query.
        /// </summary>
        /// <param name="queryPool">The <see cref="QueryPool"/> owning the query.</param>
        /// <param name="index">The query index.</param>
        public unsafe void WriteTimestamp(QueryPool queryPool, int index)
        {
            nativeDeviceContext->End((ID3D11Asynchronous*)queryPool.NativeQueries[index]);
        }

        /// <summary>
        /// Begins debug event.
        /// </summary>
        /// <param name="profileColor">Color of the profile.</param>
        /// <param name="name">The name.</param>
        public unsafe void BeginProfile(Color4 profileColor, string name)
        {
            if (nativeDeviceProfiler != null)
                nativeDeviceProfiler->BeginEvent(name);
        }

        /// <summary>
        /// Ends debug event.
        /// </summary>
        public unsafe void EndProfile()
        {
            if (nativeDeviceProfiler != null)
                nativeDeviceProfiler->EndEvent();
        }

        /// <summary>
        /// Clears the specified depth stencil buffer. See <see cref="Textures+and+render+targets"/> to learn how to use it.
        /// </summary>
        /// <param name="depthStencilBuffer">The depth stencil buffer.</param>
        /// <param name="options">The options.</param>
        /// <param name="depth">The depth.</param>
        /// <param name="stencil">The stencil.</param>
        /// <exception cref="System.InvalidOperationException"></exception>
        public unsafe void Clear(Texture depthStencilBuffer, DepthStencilClearOptions options, float depth = 1, byte stencil = 0)
        {
            if (depthStencilBuffer == null) throw new ArgumentNullException("depthStencilBuffer");

            var flags = ((options & DepthStencilClearOptions.DepthBuffer) != 0) ? ClearFlag.ClearDepth : 0;

            // Check that the DepthStencilBuffer has a Stencil if Clear Stencil is requested
            if ((options & DepthStencilClearOptions.Stencil) != 0)
            {
                if (!depthStencilBuffer.HasStencil)
                    throw new InvalidOperationException(string.Format(FrameworkResources.NoStencilBufferForDepthFormat, depthStencilBuffer.ViewFormat));
                flags |= ClearFlag.ClearStencil;
            }

            NativeDeviceContext->ClearDepthStencilView(depthStencilBuffer.NativeDepthStencilView, (uint)flags, depth, stencil);
        }

        /// <summary>
        /// Clears the specified render target. See <see cref="Textures+and+render+targets"/> to learn how to use it.
        /// </summary>
        /// <param name="renderTarget">The render target.</param>
        /// <param name="color">The color.</param>
        /// <exception cref="System.ArgumentNullException">renderTarget</exception>
        public unsafe void Clear(Texture renderTarget, Color4 color)
        {
            if (renderTarget == null) throw new ArgumentNullException("renderTarget");

            NativeDeviceContext->ClearRenderTargetView(renderTarget.NativeRenderTargetView, (float*)&color);
        }

        /// <summary>
        /// Clears a read-write Buffer. This buffer must have been created with read-write/unordered access.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="System.ArgumentNullException">buffer</exception>
        /// <exception cref="System.ArgumentException">Expecting buffer supporting UAV;buffer</exception>
        public unsafe void ClearReadWrite(Buffer buffer, Vector4 value)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (buffer.NativeUnorderedAccessView == null) throw new ArgumentException("Expecting buffer supporting UAV", nameof(buffer));

            NativeDeviceContext->ClearUnorderedAccessViewUint(buffer.NativeUnorderedAccessView, (uint*)&value);
        }

        /// <summary>
        /// Clears a read-write Buffer. This buffer must have been created with read-write/unordered access.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="System.ArgumentNullException">buffer</exception>
        /// <exception cref="System.ArgumentException">Expecting buffer supporting UAV;buffer</exception>
        public unsafe void ClearReadWrite(Buffer buffer, Int4 value)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (buffer.NativeUnorderedAccessView == null) throw new ArgumentException("Expecting buffer supporting UAV", nameof(buffer));

            NativeDeviceContext->ClearUnorderedAccessViewUint(buffer.NativeUnorderedAccessView, (uint*)&value);
        }

        /// <summary>
        /// Clears a read-write Buffer. This buffer must have been created with read-write/unordered access.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="System.ArgumentNullException">buffer</exception>
        /// <exception cref="System.ArgumentException">Expecting buffer supporting UAV;buffer</exception>
        public unsafe void ClearReadWrite(Buffer buffer, UInt4 value)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (buffer.NativeUnorderedAccessView == null) throw new ArgumentException("Expecting buffer supporting UAV", nameof(buffer));

            NativeDeviceContext->ClearUnorderedAccessViewUint(buffer.NativeUnorderedAccessView, (uint*)&value);
        }

        /// <summary>
        /// Clears a read-write Texture. This texture must have been created with read-write/unordered access.
        /// </summary>
        /// <param name="texture">The texture.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="System.ArgumentNullException">texture</exception>
        /// <exception cref="System.ArgumentException">Expecting texture supporting UAV;texture</exception>
        public unsafe void ClearReadWrite(Texture texture, Vector4 value)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (texture.NativeUnorderedAccessView == null) throw new ArgumentException("Expecting texture supporting UAV", nameof(texture));

            NativeDeviceContext->ClearUnorderedAccessViewUint(texture.NativeUnorderedAccessView, (uint*)&value);
        }

        /// <summary>
        /// Clears a read-write Texture. This texture must have been created with read-write/unordered access.
        /// </summary>
        /// <param name="texture">The texture.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="System.ArgumentNullException">texture</exception>
        /// <exception cref="System.ArgumentException">Expecting texture supporting UAV;texture</exception>
        public unsafe void ClearReadWrite(Texture texture, Int4 value)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (texture.NativeUnorderedAccessView == null) throw new ArgumentException("Expecting texture supporting UAV", nameof(texture));

            NativeDeviceContext->ClearUnorderedAccessViewUint(texture.NativeUnorderedAccessView, (uint*)&value);
        }

        /// <summary>
        /// Clears a read-write Texture. This texture must have been created with read-write/unordered access.
        /// </summary>
        /// <param name="texture">The texture.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="System.ArgumentNullException">texture</exception>
        /// <exception cref="System.ArgumentException">Expecting texture supporting UAV;texture</exception>
        public unsafe void ClearReadWrite(Texture texture, UInt4 value)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (texture.NativeUnorderedAccessView == null) throw new ArgumentException("Expecting texture supporting UAV", nameof(texture));

            NativeDeviceContext->ClearUnorderedAccessViewUint(texture.NativeUnorderedAccessView, (uint*)&value);
        }

        /// <summary>
        /// Copy a texture. View is ignored and full underlying texture is copied.
        /// </summary>
        /// <param name="source">The source texture.</param>
        /// <param name="destination">The destination texture.</param>
        public unsafe void Copy(GraphicsResource source, GraphicsResource destination)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (destination == null) throw new ArgumentNullException("destination");
            NativeDeviceContext->CopyResource(source.NativeResource, destination.NativeResource);
        }

        public unsafe void CopyMultisample(Texture sourceMultisampleTexture, int sourceSubResource, Texture destTexture, int destSubResource, PixelFormat format = PixelFormat.None)
        {
            if (sourceMultisampleTexture == null) throw new ArgumentNullException(nameof(sourceMultisampleTexture));
            if (destTexture == null) throw new ArgumentNullException("destTexture");
            if (!sourceMultisampleTexture.IsMultisample) throw new ArgumentOutOfRangeException(nameof(sourceMultisampleTexture), "Source texture is not a MSAA texture");

            NativeDeviceContext->ResolveSubresource(sourceMultisampleTexture.NativeResource, (uint)sourceSubResource, destTexture.NativeResource, (uint)destSubResource, (Format)(format == PixelFormat.None ? destTexture.Format : format));
        }

        public unsafe void CopyRegion(GraphicsResource source, int sourceSubresource, ResourceRegion? sourecRegion, GraphicsResource destination, int destinationSubResource, int dstX = 0, int dstY = 0, int dstZ = 0)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (destination == null) throw new ArgumentNullException("destination");

            Box* nullableSharpDxRegion = null;

            if (sourecRegion.HasValue)
            {
                var value = sourecRegion.Value;
                *nullableSharpDxRegion = new Box((uint)value.Left, (uint)value.Top, (uint)value.Front, (uint)value.Right, (uint)value.Bottom, (uint)value.Back);
            }

            NativeDeviceContext->CopySubresourceRegion(
                source.NativeResource,
                (uint)sourceSubresource,
                (uint)dstX,
                (uint)dstY,
                (uint)dstZ,
                destination.NativeResource,
                (uint)destinationSubResource,
                nullableSharpDxRegion);
        }

        /// <inheritdoc />
        public unsafe void CopyCount(Buffer sourceBuffer, Buffer destBuffer, int offsetInBytes)
        {
            if (sourceBuffer == null) throw new ArgumentNullException("sourceBuffer");
            if (destBuffer == null) throw new ArgumentNullException("destBuffer");
            NativeDeviceContext->CopyStructureCount(destBuffer.NativeBuffer, (uint)offsetInBytes, sourceBuffer.NativeUnorderedAccessView);
        }

        internal unsafe void UpdateSubresource(GraphicsResource resource, int subResourceIndex, DataBox databox)
        {
            if (resource == null) throw new ArgumentNullException("resource");
            throw new NotImplementedException();
            //NativeDeviceContext.UpdateSubresource(*(SharpDX.DataBox*)Interop.Cast(ref databox), resource.NativeResource, subResourceIndex);
        }

        internal unsafe void UpdateSubresource(GraphicsResource resource, int subResourceIndex, DataBox databox, ResourceRegion region)
        {
            if (resource == null) throw new ArgumentNullException("resource");
            throw new NotImplementedException();
            //NativeDeviceContext.UpdateSubresource(*(SharpDX.DataBox*)Interop.Cast(ref databox), resource.NativeResource, subResourceIndex, *(SharpDX.Direct3D11.ResourceRegion*)Interop.Cast(ref region));
        }

        // TODO GRAPHICS REFACTOR what should we do with this?
        /// <summary>
        /// Maps a subresource.
        /// </summary>
        /// <param name="resource">The resource.</param>
        /// <param name="subResourceIndex">Index of the sub resource.</param>
        /// <param name="mapMode">The map mode.</param>
        /// <param name="doNotWait">if set to <c>true</c> this method will return immediately if the resource is still being used by the GPU for writing. Default is false</param>
        /// <param name="offsetInBytes">The offset information in bytes.</param>
        /// <param name="lengthInBytes">The length information in bytes.</param>
        /// <returns>Pointer to the sub resource to map.</returns>
        public unsafe MappedResource MapSubresource(GraphicsResource resource, int subResourceIndex, MapMode mapMode, bool doNotWait = false, int offsetInBytes = 0, int lengthInBytes = 0)
        {
            if (resource == null) throw new ArgumentNullException("resource");

            // This resource has just been recycled by the GraphicsResourceAllocator, we force a rename to avoid GPU=>GPU sync point
            if (resource.DiscardNextMap && mapMode == MapMode.WriteNoOverwrite)
                mapMode = MapMode.WriteDiscard;

            MappedSubresource* dataBox = null;
            NativeDeviceContext->Map(resource.NativeResource, (uint)subResourceIndex, (Map)mapMode, doNotWait ? (uint)MapFlag.MapFlagDONotWait : 0, dataBox);
            var strideDatabox = new DataBox(IntPtr.Add(new IntPtr(dataBox->PData), offsetInBytes), (int)dataBox->RowPitch, (int)dataBox->DepthPitch);
            return new MappedResource(resource, subResourceIndex, strideDatabox);
        }

        // TODO GRAPHICS REFACTOR what should we do with this?
        public unsafe void UnmapSubresource(MappedResource unmapped)
        {
            NativeDeviceContext->Unmap(unmapped.Resource.NativeResource, (uint)unmapped.SubResourceIndex);
        }

        private unsafe void InitializeStages()
        {
            shaderStages[(int)ShaderStage.Vertex - 1] = new VertexShader((ID3D11DeviceContext**)nativeDeviceContext->LpVtbl);
            shaderStages[(int)ShaderStage.Hull - 1] = new HullShader((ID3D11DeviceContext**)nativeDeviceContext->LpVtbl);
            shaderStages[(int)ShaderStage.Domain - 1] = new DomainShader((ID3D11DeviceContext**)nativeDeviceContext->LpVtbl);
            shaderStages[(int)ShaderStage.Geometry - 1] = new GeometryShader((ID3D11DeviceContext**)nativeDeviceContext->LpVtbl);
            shaderStages[(int)ShaderStage.Pixel - 1] = new PixelShader((ID3D11DeviceContext**)nativeDeviceContext->LpVtbl);
            shaderStages[(int)ShaderStage.Compute - 1] = new ComputeShader((ID3D11DeviceContext**)nativeDeviceContext->LpVtbl);
        }
    }
}

#endif
