using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace Stride.Graphics
{
    internal unsafe abstract class CommonShaderStage
    {
        public abstract void SetConstantBuffer(int slot, ID3D11Buffer* buffer);
        public abstract void SetSampler(int slot, ID3D11SamplerState* samplerState);
        public abstract void SetShaderResource(int slot, ID3D11ShaderResourceView* resourceView);
    }

    internal unsafe class VertexShader : CommonShaderStage
    {
        private ID3D11DeviceContext* @this;
        public VertexShader(ID3D11DeviceContext** @this)
        {
            this.@this = *@this;
        }

        public override void SetConstantBuffer(int slot, ID3D11Buffer* buffer)
        {
            var arr = new[] { *buffer };
            fixed (ID3D11Buffer* temp = arr)
            {
                @this->VSSetConstantBuffers((uint)slot, 1, &temp);
            }
        }

        public override void SetSampler(int slot, ID3D11SamplerState* samplerState)
        {
            var arr = new[] { *samplerState };
            fixed (ID3D11SamplerState* temp = arr)
            {
                @this->VSSetSamplers((uint)slot, 1, &temp);
            }
        }

        public override void SetShaderResource(int slot, ID3D11ShaderResourceView* resourceView)
        {
            var arr = new[] { *resourceView };
            fixed (ID3D11ShaderResourceView* temp = arr)
            {
                @this->VSSetShaderResources((uint)slot, 1, &temp);
            }
        }
    }

    internal unsafe class HullShader : CommonShaderStage
    {
        private ID3D11DeviceContext* @this;
        public HullShader(ID3D11DeviceContext** @this)
        {
            this.@this = *@this;
        }

        public override void SetConstantBuffer(int slot, ID3D11Buffer* buffer)
        {
            var arr = new[] { *buffer };
            fixed (ID3D11Buffer* temp = arr)
            {
                @this->HSSetConstantBuffers((uint)slot, 1, &temp);
            }
        }

        public override void SetSampler(int slot, ID3D11SamplerState* samplerState)
        {
            var arr = new[] { *samplerState };
            fixed (ID3D11SamplerState* temp = arr)
            {
                @this->HSSetSamplers((uint)slot, 1, &temp);
            }
        }

        public override void SetShaderResource(int slot, ID3D11ShaderResourceView* resourceView)
        {
            var arr = new[] { *resourceView };
            fixed (ID3D11ShaderResourceView* temp = arr)
            {
                @this->HSSetShaderResources((uint)slot, 1, &temp);
            }
        }
    }

    internal unsafe class DomainShader : CommonShaderStage
    {
        private ID3D11DeviceContext* @this;
        public DomainShader(ID3D11DeviceContext** @this)
        {
            this.@this = *@this;
        }

        public override void SetConstantBuffer(int slot, ID3D11Buffer* buffer)
        {
            var arr = new[] { *buffer };
            fixed (ID3D11Buffer* temp = arr)
            {
                @this->DSSetConstantBuffers((uint)slot, 1, &temp);
            }
        }

        public override void SetSampler(int slot, ID3D11SamplerState* samplerState)
        {
            var arr = new[] { *samplerState };
            fixed (ID3D11SamplerState* temp = arr)
            {
                @this->DSSetSamplers((uint)slot, 1, &temp);
            }
        }

        public override void SetShaderResource(int slot, ID3D11ShaderResourceView* resourceView)
        {
            var arr = new[] { *resourceView };
            fixed (ID3D11ShaderResourceView* temp = arr)
            {
                @this->DSSetShaderResources((uint)slot, 1, &temp);
            }
        }
    }

    internal unsafe class GeometryShader : CommonShaderStage
    {
        private ID3D11DeviceContext* @this;
        public GeometryShader(ID3D11DeviceContext** @this)
        {
            this.@this = *@this;
        }

        public override void SetConstantBuffer(int slot, ID3D11Buffer* buffer)
        {
            var arr = new[] { *buffer };
            fixed (ID3D11Buffer* temp = arr)
            {
                @this->GSSetConstantBuffers((uint)slot, 1, &temp);
            }
        }

        public override void SetSampler(int slot, ID3D11SamplerState* samplerState)
        {
            var arr = new[] { *samplerState };
            fixed (ID3D11SamplerState* temp = arr)
            {
                @this->GSSetSamplers((uint)slot, 1, &temp);
            }
        }

        public override void SetShaderResource(int slot, ID3D11ShaderResourceView* resourceView)
        {
            var arr = new[] { *resourceView };
            fixed (ID3D11ShaderResourceView* temp = arr)
            {
                @this->GSSetShaderResources((uint)slot, 1, &temp);
            }
        }
    }

    internal unsafe class PixelShader : CommonShaderStage
    {
        private ID3D11DeviceContext* @this;
        public PixelShader(ID3D11DeviceContext** @this)
        {
            this.@this = *@this;
        }

        public override void SetConstantBuffer(int slot, ID3D11Buffer* buffer)
        {
            var arr = new[] { *buffer };
            fixed (ID3D11Buffer* temp = arr)
            {
                @this->PSSetConstantBuffers((uint)slot, 1, &temp);
            }
        }

        public override void SetSampler(int slot, ID3D11SamplerState* samplerState)
        {
            var arr = new[] { *samplerState };
            fixed (ID3D11SamplerState* temp = arr)
            {
                @this->PSSetSamplers((uint)slot, 1, &temp);
            }
        }

        public override void SetShaderResource(int slot, ID3D11ShaderResourceView* resourceView)
        {
            var arr = new[] { *resourceView };
            fixed (ID3D11ShaderResourceView* temp = arr)
            {
                @this->PSSetShaderResources((uint)slot, 1, &temp);
            }
        }
    }

    internal unsafe class ComputeShader : CommonShaderStage
    {
        private ID3D11DeviceContext* @this;
        public ComputeShader(ID3D11DeviceContext** @this)
        {
            this.@this = *@this;
        }

        public override void SetConstantBuffer(int slot, ID3D11Buffer* buffer)
        {
            var arr = new[] { *buffer };
            fixed (ID3D11Buffer* temp = arr)
            {
                @this->CSSetConstantBuffers((uint)slot, 1, &temp);
            }
        }

        public override void SetSampler(int slot, ID3D11SamplerState* samplerState)
        {
            var arr = new[] { *samplerState };
            fixed (ID3D11SamplerState* temp = arr)
            {
                @this->CSSetSamplers((uint)slot, 1, &temp);
            }
        }

        public override void SetShaderResource(int slot, ID3D11ShaderResourceView* resourceView)
        {
            var arr = new[] { *resourceView };
            fixed (ID3D11ShaderResourceView* temp = arr)
            {
                @this->CSSetShaderResources((uint)slot, 1, &temp);
            }
        }
    }
}
