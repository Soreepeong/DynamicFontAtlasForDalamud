SamplerState FontSampler : register(s0);
Texture2D FontTexture : register(t0);

struct PSInput {
    float4 position : SV_POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD;
};

float4 main_ps(PSInput input) : SV_TARGET {
    const float4 color = FontTexture.Sample(FontSampler, input.uv);
    switch (floor(input.uv.x)) {
        case 1: return float4(input.color.xyz, color.r);
        case 2: return float4(input.color.xyz, color.g);
        case 3: return float4(input.color.xyz, color.b);
        case 4: return float4(input.color.xyz, color.a);
        default: return input.color * color;
    }
}
